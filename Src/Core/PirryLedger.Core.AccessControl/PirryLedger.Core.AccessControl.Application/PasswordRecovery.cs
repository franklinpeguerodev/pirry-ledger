using PirryLedger.Core.AccessControl.Domain;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class RecoveryRejectedException : Exception
{
    public RecoveryRejectedException() : base("El codigo de recuperacion no es valido.")
    {
    }
}

public sealed class PasswordRecovery
{
    private readonly IUserRepository _users;
    private readonly IRecoveryCodeRepository _codes;
    private readonly IEmailQueue _emailQueue;
    private readonly IClock _clock;
    private readonly IPasswordHasher _hasher;
    private readonly TimeSpan _validity;

    public PasswordRecovery(
        IUserRepository users,
        IRecoveryCodeRepository codes,
        IEmailQueue emailQueue,
        IClock clock,
        IPasswordHasher hasher,
        TimeSpan validity)
    {
        _users = users;
        _codes = codes;
        _emailQueue = emailQueue;
        _clock = clock;
        _hasher = hasher;
        _validity = validity;
    }

    public async Task RequestAsync(string? email, CancellationToken cancellationToken = default)
    {
        if (!EmailValidator.Evaluar(email ?? string.Empty).EsValido)
        {
            return;
        }

        var user = await _users.BuscarPorCorreoAsync(email!.Trim(), cancellationToken);
        if (user is null)
        {
            return;
        }

        await _codes.InvalidarPendientesAsync(user.Id, cancellationToken);
        var plainCode = OneTimeCodeGenerator.Generar();
        var code = CodigoRecuperacion.Crear(
            user.Id,
            OneTimeCodeGenerator.CalcularHash(plainCode),
            _clock.UtcNow,
            _validity);

        await _codes.GuardarAsync(code, cancellationToken);
        await _emailQueue.EnqueueAsync(
            new QueuedEmail(user.Correo, CorreoDeRecuperacion.Asunto, CorreoDeRecuperacion.ArmarCuerpo(plainCode)),
            cancellationToken);
    }

    public async Task CompleteAsync(
        string? plainCode,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        var policy = PasswordPolicy.Evaluar(newPassword);
        if (!policy.EsValida)
        {
            throw new RegistrationRejectedException(policy.Motivo!);
        }

        var code = await FindValidCodeAsync(plainCode, cancellationToken);
        var user = code is null
            ? null
            : await _users.BuscarPorIdAsync(code.UsuarioId, cancellationToken);
        if (code is null || user is null)
        {
            throw new RecoveryRejectedException();
        }

        user.CambiarContrasena(_hasher.Hash(newPassword!), _clock.UtcNow);
        code.MarcarUsado(_clock.UtcNow);
        await _users.GuardarAsync(user, cancellationToken);
        await _codes.GuardarAsync(code, cancellationToken);
    }

    private async Task<CodigoRecuperacion?> FindValidCodeAsync(
        string? plainCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plainCode))
        {
            return null;
        }

        var code = await _codes.BuscarPorHashAsync(
            OneTimeCodeGenerator.CalcularHash(plainCode.Trim()),
            cancellationToken);
        return code is null || code.EstaUsado || code.HaVencido(_clock.UtcNow) ? null : code;
    }
}
