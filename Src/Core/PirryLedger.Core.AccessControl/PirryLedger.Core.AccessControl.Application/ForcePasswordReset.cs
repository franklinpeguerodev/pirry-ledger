using PirryLedger.Core.AccessControl.Domain;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class ForcePasswordReset
{
    private readonly IUserRepository _users;
    private readonly IRecoveryCodeRepository _codes;
    private readonly IEmailQueue _emailQueue;
    private readonly Autenticar _auth;
    private readonly IClock _clock;
    private readonly IPasswordHasher _hasher;
    private readonly TimeSpan _validity;

    public ForcePasswordReset(
        IUserRepository users,
        IRecoveryCodeRepository codes,
        IEmailQueue emailQueue,
        Autenticar auth,
        IClock clock,
        IPasswordHasher hasher,
        TimeSpan validity)
    {
        _users = users;
        _codes = codes;
        _emailQueue = emailQueue;
        _auth = auth;
        _clock = clock;
        _hasher = hasher;
        _validity = validity;
    }

    public async Task ExecuteAsync(string token, Guid userId, CancellationToken cancellationToken = default)
    {
        await _auth.EjecutarConRolAsync(token, ExigenciasDeRol.RolRequerido(Operacion.ForzarRestablecimiento), cancellationToken);
        var user = await _users.BuscarPorIdAsync(userId, cancellationToken)
            ?? throw new OperacionDeAdministracionRechazadaException("No existe ese usuario.");

        // Invalidate the old password immediately. The recovery code is the only
        // path that can establish the next usable password.
        user.CambiarContrasena(_hasher.Hash(OneTimeCodeGenerator.Generar()), _clock.UtcNow);
        await _users.GuardarAsync(user, cancellationToken);
        await _codes.InvalidarPendientesAsync(user.Id, cancellationToken);
        var plainCode = OneTimeCodeGenerator.Generar();
        await _codes.GuardarAsync(
            CodigoRecuperacion.Crear(user.Id, OneTimeCodeGenerator.CalcularHash(plainCode), _clock.UtcNow, _validity),
            cancellationToken);
        await _emailQueue.EnqueueAsync(
            new QueuedEmail(user.Correo, CorreoDeRecuperacion.Asunto, CorreoDeRecuperacion.ArmarCuerpo(plainCode)),
            cancellationToken);
    }
}
