using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// Los tres rechazos de este caso de uso. Son tipos distintos y no excepciones
// genericas porque cada uno lleva un message distinto al cliente, y aun asi
// ninguno revela si el Email existe.
public sealed class CredentialsRejectedException : Exception
{
    public CredentialsRejectedException(string message) : base(message)
    {
    }
}

public sealed class AccountBlockedException : Exception
{
    public AccountBlockedException(string message) : base(message)
    {
    }
}

public sealed class AccountNotActivatedException : Exception
{
    public AccountNotActivatedException(string message) : base(message)
    {
    }
}

// RF-CA-03 y RF-CA-19. Decision de credencial en docs/adr/001-session-credential.md.
//
// El orden de las comprobaciones es la parte importante de este archivo, y no es
// arbitrario:
//
// 1. Bloqueo. Se mira antes de Verify la contrasena porque RF-CA-19 exige que
//    el sexto intento se rechace AUN CON la contrasena correcta. Verify el hash
//    primero daria un `401` de credenciales y no de bloqueo, que es otra respuesta.
// 2. Hash senuelo. Si el Email no existe se verifica igualmente contra un hash
//    falso, para que el tiempo de respuesta no diga si el Email esta dado de
//    alta. Sin esto, un `401` en 5 ms delata que el Email no existe.
// 3. Verificacion real de la contrasena.
// 4. Cuenta inactiva. Solo se informa DESPUES de que la contrasena sea correcta,
//    porque si se dijera antes, bastaria con conocer un Email registrado para
//    enterarse de que existe, sin saber la contrasena.
public sealed class Login
{
    // Decision de Franklin. El message durante el bloqueo NO dice que la cuenta
    // esta bloqueada: confirmarlo seria decir que el Email existe, y con esto el
    // endpoint no podria distinguir "bloqueado" de "mal contrasena" ni de "no
    // existe". Se responde con el mismo message de credenciales incorrectas.
    private const string CredentialsMessage = "Email o contrasena incorrectos.";
    private const string LockoutMessage = "Email o contrasena incorrectos.";
    private const string AccountInactiveMessage = "La cuenta no esta activada.";

    private const int MaximumAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly TimeSpan _sessionDuration;

    public Login(
        IUserRepository users,
        ISessionRepository sessions,
        IPasswordHasher hasher,
        IClock clock,
        TimeSpan sessionDuration)
    {
        _users = users;
        _sessions = sessions;
        _hasher = hasher;
        _clock = clock;
        _sessionDuration = sessionDuration;
    }

    // Devuelve el token EN CLARO, que es lo unico que sale hacia el cliente y lo
    // unico que no se vuelve a guardar en ningun sitio.
    public async Task<string> ExecuteAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var utcNow = _clock.UtcNow;

        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(normalizedEmail, cancellationToken);

        // 1. Bloqueo (RF-CA-19). Se comprueba sobre el user real. Un Email que
        // no existe no puede estar bloqueado, y por eso cae en el hash senuelo
        // de abajo en lugar de recibir el message de bloqueo: si lo recibiera,
        // bastaria con five intentos fallidos para descubrir que el Email existe.
        if (user is not null && user.IsBlocked(utcNow))
        {
            throw new AccountBlockedException(LockoutMessage);
        }

        // 2. Hash senuelo. Se verifica SIEMPRE, exista o no el user, para que el
        // tiempo de respuesta sea el mismo en los dos casos.
        var hashToVerify = user?.PasswordHash ?? _hasher.CreateDummyHash();
        var passwordCorrect = _hasher.Verify(password ?? string.Empty, hashToVerify);

        // 3. Credenciales incorrectas. El message es el mismo tanto si el Email no
        // existia como si la contrasena estaba mal, para no revelar cual de las
        // dos cosas fallo (RF-CA-03).
        if (user is null || !passwordCorrect)
        {
            // RF-CA-19: el contador vive en el user, no en memoria del proceso.
            // Asi el bloqueo aguanta un reinicio de la aplicacion.
            if (user is not null)
            {
                user.RegisterFailedAttempt(MaximumAttempts, utcNow, LockoutDuration);
                await _users.SaveAsync(user, cancellationToken);
            }

            throw new CredentialsRejectedException(CredentialsMessage);
        }

        // 4. RF-CA-15: la cuenta tiene que estar activada. Este message SÍ es
        // distinto del anterior, y solo se alcanza con la contrasena correcta.
        if (!user.Active)
        {
            throw new AccountNotActivatedException(AccountInactiveMessage);
        }

        // RF-CA-19: un acierto pone el contador en cero.
        user.RegisterSuccessfulAttempt();

        // Decision de Franklin: caducidad ABSOLUTA de 8 horas, no deslizante.
        var plainToken = SessionTokenGenerator.Generate();

        var session = Session.Create(
            user.Id,
            SessionTokenGenerator.CalculateHash(plainToken),
            utcNow,
            _sessionDuration,
            user.CredentialVersion);

        await _sessions.SaveAsync(session, cancellationToken);
        await _users.SaveAsync(user, cancellationToken);

        return plainToken;
    }
}
