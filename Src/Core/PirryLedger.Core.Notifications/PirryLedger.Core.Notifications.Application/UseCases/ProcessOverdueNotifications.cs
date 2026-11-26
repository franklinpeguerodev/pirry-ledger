using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.Notifications.Application;

public sealed record OverdueProcessingResult(int Recorded, int Skipped, int Failures);

public sealed class ProcessOverdueNotifications(IEnumerable<IOverdueNotificationSource> sources,
    IOverdueNotificationStore store, INotificationPublisher publisher, IApplicationTransaction transaction, IClock clock)
{
    public const int MaximumNewCases = 50;

    public async Task<OverdueProcessingResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var candidates = new List<(IOverdueNotificationSource Source, OverdueNotificationCandidate Candidate)>();
        var failures = 0;
        foreach (var source in sources)
        {
            try
            {
                var found = await source.FindCandidatesAsync(now, cancellationToken);
                candidates.AddRange(found.Select(candidate => (source, candidate)));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { failures++; }
        }
        var recorded = 0;
        var skipped = 0;
        foreach (var (source, candidate) in candidates.OrderBy(item => item.Candidate.DueAtUtc)
            .ThenBy(item => item.Candidate.Kind).ThenBy(item => item.Candidate.SourceId))
        {
            if (recorded == MaximumNewCases) break;
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await store.IsRecordedAsync(candidate.Key, cancellationToken)) { skipped++; continue; }
                var added = await transaction.ExecuteAsync(async () =>
                {
                    var notificationEvent = await source.RevalidateAsync(candidate, now, cancellationToken);
                    if (notificationEvent is null) return false;
                    if (notificationEvent.Key != candidate.Key)
                        throw new InvalidOperationException("La revalidación no corresponde al caso de atraso.");
                    if (!await store.TryRecordAsync(candidate.Key, now, cancellationToken)) return false;
                    await publisher.PublishAsync(notificationEvent, cancellationToken);
                    return true;
                }, cancellationToken);
                if (added) recorded++; else skipped++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { failures++; }
        }
        return new(recorded, skipped, failures);
    }
}
