using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-16: abrir el enlace de activacion activa la cuenta.
//
// El token es de un solo uso y caduca. Un enlace ya usado, uno vencido o uno que
// no existe producen la misma respuesta, porque distinguir entre ellos le
// diria a quien tiene el enlace si ese enlace existio alguna vez.
//
// La cuenta solo se activa si el token es valido, y activar una cuenta ya activa
// se rechaza en la entidad, no aqui (RD-04).
public sealed class ActivateAccount
{
    private readonly IUserRepository _users;
    private readonly IActivationTokenRepository _tokens;
    private readonly IClock _clock;

    // Sin IPasswordHasher: activar no toca la contrasena. Se recibia antes y no se
    // usaba, y una dependencia que no se usa hace creer que el caso hace algo con
    // el hash cuando no es asi.
    public ActivateAccount(
        IUserRepository users,
        IActivationTokenRepository tokens,
        IClock clock)
    {
        _users = users;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task ExecuteAsync(string plainToken, CancellationToken cancellationToken = default)
    {
        // RD-07: un enlace vacio o mal formado es un rechazo controlado, no una
        // excepcion sin manejar.
        if (string.IsNullOrWhiteSpace(plainToken))
        {
            throw new ActivationRejectedException();
        }

        var hash = OneTimeCodeGenerator.CalculateHash(plainToken.Trim());

        var token = await _tokens.FindByHashAsync(hash, cancellationToken);

        // Las tres salidas de aqui son la misma para quien llama.
        if (token is null || token.IsUsed || token.HasExpired(_clock.UtcNow))
        {
            throw new ActivationRejectedException();
        }

        var user = await _users.FindByIdAsync(token.UserId, cancellationToken);

        if (user is null)
        {
            throw new ActivationRejectedException();
        }

        // Un solo uso. MarkUsed falla si ya se uso, y aqui solo se llega si no
        // lo estaba, asi que el enlace no se puede reutilizar.
        token.MarkUsed(_clock.UtcNow);
        await _tokens.SaveAsync(token, cancellationToken);

        user.Activate();
        await _users.SaveAsync(user, cancellationToken);
    }
}
