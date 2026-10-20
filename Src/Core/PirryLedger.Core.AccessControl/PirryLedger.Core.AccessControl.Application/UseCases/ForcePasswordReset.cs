using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class ForcePasswordReset
{
    private readonly IUserRepository _users;
    private readonly IRecoveryCodeRepository _codes;
    private readonly IEmailQueue _emailQueue;
    private readonly Authenticate _auth;
    private readonly IClock _clock;
    private readonly IPasswordHasher _hasher;
    private readonly TimeSpan _validity;

    public ForcePasswordReset(
        IUserRepository users,
        IRecoveryCodeRepository codes,
        IEmailQueue emailQueue,
        Authenticate auth,
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
        await _auth.ExecuteWithRoleAsync(token, RoleRequirements.RequiredRole(Operation.ForceReset), cancellationToken);
        var user = await _users.FindByIdAsync(userId, cancellationToken)
            ?? throw new AdministrationOperationRejectedException("No existe ese user.");

        // Invalidate the old password immediately. The recovery code is the only
        // path that can establish the next usable password.
        user.ChangePassword(_hasher.Hash(OneTimeCodeGenerator.Generate()), _clock.UtcNow);
        await _users.SaveAsync(user, cancellationToken);
        await _codes.InvalidatePendingAsync(user.Id, cancellationToken);
        var plainCode = OneTimeCodeGenerator.Generate();
        await _codes.SaveAsync(
            RecoveryCode.Create(user.Id, OneTimeCodeGenerator.CalculateHash(plainCode), _clock.UtcNow, _validity),
            cancellationToken);
        await _emailQueue.EnqueueAsync(
            new QueuedEmail(user.Email, RecoveryEmail.Subject, RecoveryEmail.BuildBody(plainCode)),
            cancellationToken);
    }
}
