using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// Quien esta autenticado, ya validado. Viaja hasta el endpoint, que solo lo usa
// para responder.
public sealed record AuthenticatedUser(Guid Id, string Name, string Email, Role Role);

// Excepcion unica de autenticacion. El endpoint la convierte en 401. El role
// insuficiente NO se lanza desde aqui: es un 403 y lo decide el punto de role.
public sealed class InvalidSessionException : Exception
{
    public InvalidSessionException() : base("Sesion no valida.")
    {
    }
}

// RF-CA-06: role insuficiente. Distinto de InvalidSessionException a proposito,
// porque el codigo tambien es distinto: 403 dice "tu sesion vale, tu role no".
public sealed class InsufficientRoleException : Exception
{
    public InsufficientRoleException() : base("No tiene permisos para esta operacion.")
    {
    }
}

// EL UNICO PUNTO DE VALIDACION.
//
// Las cinco condiciones de docs/adr/001-session-credential.md se comprueban aqui
// y en ningun otro sitio: la sesion existe, no esta cerrada, no vencio, su
// CredentialVersion coincide con la del usuario y el usuario sigue activo.
//
// Que sea uno solo no es estilo, es el requisito: "State transitions are resolved
// in a single component (RD-04)" y "Authorization is enforced on the server for
// every operation (RD-06)". Si cada endpoint comprobara la sesion por su cuenta,
// bastaria uno que se olvidara para abrir un agujero, y el rechazo dejaria de
// funcionar con peticiones armadas a mano contra la API.
public sealed class Authenticate
{
    private readonly ISessionRepository _sessions;
    private readonly IUserRepository _users;
    private readonly IClock _clock;
    private readonly IPermissionAuthority _permissions;

    public Authenticate(
        ISessionRepository sessions,
        IUserRepository users,
        IClock clock,
        IPermissionAuthority permissions)
    {
        _sessions = sessions;
        _users = users;
        _clock = clock;
        _permissions = permissions;
    }

    // Lanza InvalidSessionException si no hay sesion valida. Devuelve el usuario
    // ya validado: el endpoint no vuelve a comprobar nada.
    public async Task<AuthenticatedUser> ExecuteAsync(string plainToken, CancellationToken cancellationToken = default)
    {
        var user = await ValidateAsync(plainToken, cancellationToken);

        return new AuthenticatedUser(user.Id, user.Name, user.Email, user.Role);
    }

    // La misma validacion, pero devolviendo false en vez de lanzar. Para los
    // endpoints que solo necesitan saber si hay sesion.
    public async Task<bool> HasValidSessionAsync(string plainToken, CancellationToken cancellationToken = default)
    {
        try
        {
            await ValidateAsync(plainToken, cancellationToken);
            return true;
        }
        catch (InvalidSessionException)
        {
            return false;
        }
    }

    // El role tambien se comprueba aqui y no en el endpoint, por el mismo motivo
    // (RD-06): el rechazo tiene que ocurrir con peticiones hechas a mano.
    public async Task<AuthenticatedUser> ExecuteWithRoleAsync(
        string plainToken,
        Role requiredRole,
        CancellationToken cancellationToken = default)
    {
        var user = await ValidateAsync(plainToken, cancellationToken);

        if (user.Role != requiredRole)
        {
            throw new InsufficientRoleException();
        }

        return new AuthenticatedUser(user.Id, user.Name, user.Email, user.Role);
    }

    // RF-GP-05: la misma idea que ExecuteWithRoleAsync pero consultando
    // IPermissionAuthority. La pieza de Permissions registra la implementacion
    // en DI; AccessControl solo ve el contrato (en Core.Contracts).
    //
    // Si la sesion no es valida, lanza InvalidSessionException (401). Si el
    // usuario no tiene el permiso concedido, lanza InsufficientPermissionException
    // (403). Mismas dos capas de rechazo que el caso de role, mismo orden.
    public async Task<AuthenticatedUser> ExecuteWithPermissionAsync(
        string plainToken,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        var user = await ValidateAsync(plainToken, cancellationToken);

        if (!await _permissions.UserHasPermissionAsync(user.Id, permissionName, cancellationToken))
        {
            throw new InsufficientPermissionException();
        }

        return new AuthenticatedUser(user.Id, user.Name, user.Email, user.Role);
    }

    // El nucleo: las cinco condiciones, en un solo metodo. Todo lo demas lo llama.
    private async Task<User> ValidateAsync(string plainToken, CancellationToken cancellationToken)
    {
        // RD-07: una cabecera vacia o corrupta es un 401, no una excepcion sin
        // manejar.
        if (string.IsNullOrWhiteSpace(plainToken))
        {
            throw new InvalidSessionException();
        }

        var hash = SessionTokenGenerator.CalculateHash(plainToken.Trim());

        var session = await _sessions.FindByHashAsync(hash, cancellationToken);

        if (session is null)
        {
            throw new InvalidSessionException();
        }

        var user = await _users.FindByIdAsync(session.UserId, cancellationToken);

        if (user is null)
        {
            throw new InvalidSessionException();
        }

        // Las cuatro condiciones restantes viven en la entidad (RD-04). Aqui solo
        // se le pasa el usuario y el reloj.
        if (!session.IsValid(_clock.UtcNow, user))
        {
            throw new InvalidSessionException();
        }

        return user;
    }
}
