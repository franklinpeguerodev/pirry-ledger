using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Application.UseCases;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Tests.CasosDeUso;

public sealed class PruebasDeSolicitudYNotificacionAtomicas
{
    [Fact]
    public async Task SolicitudYPublicacionSeEjecutanDentroDelMismoCoordinador()
    {
        var scenario = new EscenarioDePermisos();
        scenario.Standard.AssignBranch(Guid.NewGuid());
        var token = await scenario.OpenStandardSessionAsync();
        var requests = new Solicitudes();
        var transaction = new Transaccion(requests);
        requests.IsActive = () => transaction.Active;
        var publisher = new Publicador(() => transaction.Active, false);
        var service = new CreatePermissionRequest(requests, scenario.Authority,
            scenario.Authenticate, scenario.Clock, publisher, transaction);

        var id = await service.ExecuteAsync(token, "ViewSalesSummary");

        Assert.Equal(1, transaction.Calls);
        Assert.Equal(1, publisher.Delivered);
        Assert.Equal(scenario.Standard.BranchId, requests.Rows[id].BranchId);
        Assert.Equal(id, publisher.LastEvent!.SourceId);
        Assert.Equal(NotificationEventKind.PermissionRequested, publisher.LastEvent.Kind);
    }

    [Fact]
    public async Task FalloDePublicacionNoDejaSolicitudNiAvisoDeExito()
    {
        var scenario = new EscenarioDePermisos();
        var token = await scenario.OpenStandardSessionAsync();
        var requests = new Solicitudes();
        var transaction = new Transaccion(requests);
        requests.IsActive = () => transaction.Active;
        var publisher = new Publicador(() => transaction.Active, true);
        var service = new CreatePermissionRequest(requests, scenario.Authority,
            scenario.Authenticate, scenario.Clock, publisher, transaction);

        await Assert.ThrowsAsync<FalloDeAviso>(() => service.ExecuteAsync(token, "ViewSalesSummary"));

        Assert.Empty(requests.Rows);
        Assert.Equal(0, publisher.Delivered);
        Assert.Equal(1, transaction.Calls);
        Assert.False(transaction.Active);
    }

    private sealed class FalloDeAviso : Exception { }
    private sealed class Publicador(Func<bool> isActive, bool fail) : INotificationPublisher
    {
        public int Delivered { get; private set; }
        public NotificationEvent? LastEvent { get; private set; }
        public Task PublishAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken = default)
        {
            Assert.True(isActive());
            if (fail) throw new FalloDeAviso();
            LastEvent = notificationEvent;
            Delivered++;
            return Task.CompletedTask;
        }
    }
    private sealed class Transaccion(Solicitudes requests) : IApplicationTransaction
    {
        public bool Active { get; private set; }
        public int Calls { get; private set; }
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        {
            Calls++;
            var snapshot = requests.Rows.ToDictionary(entry => entry.Key, entry => entry.Value);
            Active = true;
            try { return await action(); }
            catch
            {
                requests.Rows.Clear();
                foreach (var entry in snapshot) requests.Rows.Add(entry.Key, entry.Value);
                throw;
            }
            finally { Active = false; }
        }
    }
    private sealed class Solicitudes : IPermissionRequestRepository
    {
        public Dictionary<Guid, PermissionRequest> Rows { get; } = new();
        public Func<bool> IsActive { get; set; } = () => false;
        public Task SaveAsync(PermissionRequest request, CancellationToken cancellationToken = default)
        {
            Assert.True(IsActive());
            Rows[request.Id] = request;
            return Task.CompletedTask;
        }
        public Task<PermissionRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Rows.GetValueOrDefault(id));
        public Task<IReadOnlyList<PermissionRequest>> ListByStateAsync(PermissionRequestState state, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PermissionRequest>>(Rows.Values.Where(request => request.State == state).ToArray());
        public Task<IReadOnlyList<PermissionRequest>> ListByRequesterAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PermissionRequest>>(Rows.Values.Where(request => request.RequesterId == id).ToArray());
        public Task<bool> ExistsPendingAsync(Guid userId, string permission, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.Values.Any(request => request.RequesterId == userId && request.RequestedPermission == permission && request.State == PermissionRequestState.Pending));
    }
}
