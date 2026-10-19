using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

public sealed class ChangeOwnPassword
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly Authenticate _auth;
    private readonly IClock _clock;

    public ChangeOwnPassword(IUserRepository users, IPasswordHasher hasher, Authenticate auth, IClock clock)
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
        var authenticated = await _auth.ExecuteAsync(token, cancellationToken);
        var policy = PasswordPolicy.Evaluate(newPassword);
        if (!policy.IsValid)
        {
            throw new RegistrationRejectedException(policy.Reason!);
        }

        var user = await _users.FindByIdAsync(authenticated.Id, cancellationToken)
            ?? throw new InvalidSessionException();
        if (!_hasher.Verify(currentPassword, user.PasswordHash))
        {
            throw new RegistrationRejectedException("La contrasena actual no es correcta.");
        }

        user.ChangePassword(_hasher.Hash(newPassword), _clock.UtcNow);
        await _users.SaveAsync(user, cancellationToken);
    }
}
