using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Notifications;

namespace PirryLedger.Core.AccessControl.Infrastructure;

public sealed class NotificationRecipientDirectory(IUserRepository users) : INotificationRecipientDirectory
{
    public async Task<NotificationRecipient?> FindActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindCurrentByIdAsync(userId, cancellationToken);
        return user is { Active: true } ? From(user) : null;
    }

    public async Task<IReadOnlyList<NotificationRecipient>> ListActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
        (await users.ListCurrentAsync(cancellationToken)).Where(user => user.Active && user.Role == Role.Administrator)
            .Select(From).ToArray();

    private static NotificationRecipient From(User user) =>
        new(user.Id, user.Email, user.Role == Role.Administrator, user.BranchId, user.Name);
}
