using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
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
        if (!EmailValidator.Evaluate(email).IsValid)
        {
            return;
        }

        var user = await _users.FindByEmailAsync(email!.Trim(), cancellationToken);
        if (user is null)
        {
            return;
        }

        await _codes.InvalidatePendingAsync(user.Id, cancellationToken);
        var plainCode = OneTimeCodeGenerator.Generate();
        var code = RecoveryCode.Create(
            user.Id,
            OneTimeCodeGenerator.CalculateHash(plainCode),
            _clock.UtcNow,
            _validity);

        await _codes.SaveAsync(code, cancellationToken);
        await _emailQueue.EnqueueAsync(
            new QueuedEmail(user.Email, RecoveryEmail.Subject, RecoveryEmail.BuildBody(plainCode)),
            cancellationToken);
    }

    public async Task CompleteAsync(
        string? plainCode,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        var policy = PasswordPolicy.Evaluate(newPassword);
        if (!policy.IsValid)
        {
            throw new RegistrationRejectedException(policy.Reason!);
        }

        var code = await FindValidCodeAsync(plainCode, cancellationToken);
        var user = code is null
            ? null
            : await _users.FindByIdAsync(code.UserId, cancellationToken);
        if (code is null || user is null)
        {
            throw new RecoveryRejectedException();
        }

        user.ChangePassword(_hasher.Hash(newPassword!), _clock.UtcNow);
        code.MarkUsed(_clock.UtcNow);
        await _users.SaveAsync(user, cancellationToken);
        await _codes.SaveAsync(code, cancellationToken);
    }

    private async Task<RecoveryCode?> FindValidCodeAsync(
        string? plainCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plainCode))
        {
            return null;
        }

        var code = await _codes.FindByHashAsync(
            OneTimeCodeGenerator.CalculateHash(plainCode.Trim()),
            cancellationToken);
        return code is null || code.IsUsed || code.HasExpired(_clock.UtcNow) ? null : code;
    }
}
