using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Tests;

internal sealed class BloqueoDeCola : IEmailQueueProcessingLock
{
    public bool Active { get; private set; }
    public bool Busy { get; set; }
    public int MaximumVerifications { get; set; } = int.MaxValue;
    private int _verifications;
    public Task<IEmailQueueProcessingLease?> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        if (Busy || Active) return Task.FromResult<IEmailQueueProcessingLease?>(null);
        Active = true;
        return Task.FromResult<IEmailQueueProcessingLease?>(new Lease(this));
    }
    private sealed class Lease(BloqueoDeCola owner) : IEmailQueueProcessingLease
    {
        public Task VerifyAsync(CancellationToken cancellationToken = default)
        {
            if (++owner._verifications > owner.MaximumVerifications)
                throw new InvalidOperationException("Se perdió la coordinación del enviador.");
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() { owner.Active = false; return ValueTask.CompletedTask; }
    }
}

internal class ColaDePrueba(params EmailQueueItem[] emails) : IEmailQueueRepository
{
    public EmailQueueItem[] Rows { get; } = emails;
    public int SavedCount { get; private set; }
    public bool FailSave { get; set; }
    public int Reads { get; private set; }
    public Task<IReadOnlyList<EmailQueueItem>> ListPendingAsync(int limit, CancellationToken cancellationToken = default)
    {
        Reads++;
        return Task.FromResult<IReadOnlyList<EmailQueueItem>>(Rows.Where(email => email.Status == EmailStatus.Pending)
            .Take(limit).ToArray());
    }
    public Task<EmailQueueItem?> ClaimAsync(Guid id, DateTime attemptedAtUtc, CancellationToken cancellationToken = default)
    {
        var email = Rows.SingleOrDefault(email => email.Id == id && email.Status == EmailStatus.Pending);
        email?.Claim(attemptedAtUtc);
        return Task.FromResult(email);
    }
    public Task<int> RecoverInterruptedAsync(DateTime changedAtUtc, CancellationToken cancellationToken = default)
    {
        var rows = Rows.Where(email => email.Status == EmailStatus.Processing).ToArray();
        foreach (var email in rows) email.MarkUncertain(changedAtUtc);
        return Task.FromResult(rows.Length);
    }
    public Task CancelPendingNotificationAsync(Guid id, DateTime changedAtUtc, CancellationToken cancellationToken = default)
    {
        Rows.Single(email => email.Id == id).CancelNotification(changedAtUtc);
        return Task.CompletedTask;
    }
    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (FailSave) throw new InvalidOperationException("Fallo controlado al guardar.");
        SavedCount++;
        return Task.CompletedTask;
    }
    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Rows.Count(email => email.Status == EmailStatus.Pending));
}
