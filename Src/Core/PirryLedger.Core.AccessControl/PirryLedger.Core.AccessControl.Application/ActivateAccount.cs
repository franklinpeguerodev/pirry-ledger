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
    private readonly IUserRepository _usuarios;
    private readonly IActivationTokenRepository _tokens;
    private readonly IClock _reloj;

    // Sin IPasswordHasher: activar no toca la contrasena. Se recibia antes y no se
    // usaba, y una dependencia que no se usa hace creer que el caso hace algo con
    // el hash cuando no es asi.
    public ActivateAccount(
        IUserRepository usuarios,
        IActivationTokenRepository tokens,
        IClock reloj)
    {
        _usuarios = usuarios;
        _tokens = tokens;
        _reloj = reloj;
    }

    public async Task EjecutarAsync(string tokenEnClaro, CancellationToken cancellationToken = default)
    {
        // RD-07: un enlace vacio o mal formado es un rechazo controlado, no una
        // excepcion sin manejar.
        if (string.IsNullOrWhiteSpace(tokenEnClaro))
        {
            throw new ActivationRejectedException();
        }

        var hash = OneTimeCodeGenerator.CalcularHash(tokenEnClaro.Trim());

        var token = await _tokens.BuscarPorHashAsync(hash, cancellationToken);

        // Las tres salidas de aqui son la misma para quien llama.
        if (token is null || token.EstaUsado || token.HaVencido(_reloj.UtcNow))
        {
            throw new ActivationRejectedException();
        }

        var usuario = await _usuarios.BuscarPorIdAsync(token.UsuarioId, cancellationToken);

        if (usuario is null)
        {
            throw new ActivationRejectedException();
        }

        // Un solo uso. MarcarUsado falla si ya se uso, y aqui solo se llega si no
        // lo estaba, asi que el enlace no se puede reutilizar.
        token.MarcarUsado(_reloj.UtcNow);
        await _tokens.GuardarAsync(token, cancellationToken);

        usuario.Activar();
        await _usuarios.GuardarAsync(usuario, cancellationToken);
    }
}