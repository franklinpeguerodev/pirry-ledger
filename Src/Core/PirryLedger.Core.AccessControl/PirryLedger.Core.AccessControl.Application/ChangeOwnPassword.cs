using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class ChangeOwnPassword
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly Autenticar _auth;
    private readonly IClock _clock;

    public ChangeOwnPassword(IUserRepository users, IPasswordHasher hasher, Autenticar auth, IClock clock)
    {
        _users = users;
        _hasher = hasher;
        _auth = auth;
        _clock = clock;
    }

    public async Task ExecuteAsync(
        string token,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var authenticated = await _auth.EjecutarAsync(token, cancellationToken);
        var policy = PasswordPolicy.Evaluar(newPassword);
        if (!policy.EsValida)
        {
            throw new RegistrationRejectedException(policy.Motivo!);
        }

        var user = await _users.BuscarPorIdAsync(authenticated.Id, cancellationToken)
            ?? throw new SesionInvalidaException();
        if (!_hasher.Verificar(currentPassword, user.HashDeContrasena))
        {
            throw new RegistrationRejectedException("La contrasena actual no es correcta.");
        }

        user.CambiarContrasena(_hasher.Hash(newPassword), _clock.UtcNow);
        await _users.GuardarAsync(user, cancellationToken);
    }
}
