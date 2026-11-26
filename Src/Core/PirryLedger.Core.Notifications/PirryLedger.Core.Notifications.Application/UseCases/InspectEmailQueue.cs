using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.Notifications.Application;

public sealed class InspectEmailQueue(IEmailQueueInspection inspection, IBranchScopeAccessor scope)
{
    public Task<EmailQueuePage> ExecuteAsync(EmailQueueFilter filter, CancellationToken cancellationToken = default)
    {
        if (!scope.IsGlobal || scope.UserId is null)
            throw new EmailQueueInspectionDeniedException();
        if (scope.UserId == Guid.Empty) throw new EmailQueueInspectionDeniedException();
        if (filter.Offset < 0 || filter.Limit is < 1 or > 100
            || filter.Status is { } status && !Enum.IsDefined(status)
            || filter.FromUtc is { Kind: not DateTimeKind.Utc } || filter.ToUtc is { Kind: not DateTimeKind.Utc }
            || filter.FromUtc > filter.ToUtc)
            throw new ArgumentException("Los filtros de cola requieren fechas UTC ordenadas, offset no negativo y limit entre 1 y 100.");
        return inspection.ListAsync(filter, cancellationToken);
    }
}

public sealed class EmailQueueInspectionDeniedException() : Exception("Solo un Administrador puede consultar la cola de correos.");
