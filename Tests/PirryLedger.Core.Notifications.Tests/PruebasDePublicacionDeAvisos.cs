using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Application;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class PruebasDePublicacionDeAvisos
{
    [Fact]
    public async Task RepetirEventoNoDuplicaAvisoNiCorreoPorAdministrador()
    {
        var scenario = new Escenario();
        var notificationEvent = new NotificationEvent(NotificationEventKind.PermissionRequested, Guid.NewGuid(), "Solicitud de prueba");

        await scenario.Publisher.PublishAsync(notificationEvent);
        await scenario.Publisher.PublishAsync(notificationEvent);

        Assert.Equal(2, scenario.Store.Notifications.Count);
        Assert.Equal(2, scenario.Store.Emails.Count);
        Assert.DoesNotContain(scenario.Store.Notifications, delivery => delivery.User == scenario.Standard.UserId);
    }

    [Fact]
    public async Task ReactivarCorreoSoloPermiteEventosNuevosSinPerderAvisosInternos()
    {
        var scenario = new Escenario();
        var admin = scenario.Admins[0].UserId;
        scenario.Store.Disabled.Add(admin);
        var first = new NotificationEvent(NotificationEventKind.PermissionRequested, Guid.NewGuid(), "Primera solicitud");
        await scenario.Publisher.PublishAsync(first);

        scenario.Store.Disabled.Remove(admin);
        await scenario.Publisher.PublishAsync(first);
        await scenario.Publisher.PublishAsync(first with { SourceId = Guid.NewGuid() });

        Assert.Equal(2, scenario.Store.Notifications.Count(delivery => delivery.User == admin));
        Assert.Single(scenario.Store.Emails, delivery => delivery.User == admin);
    }

    [Fact]
    public async Task ResolucionSoloAvisaAlDestinatarioActivoSinCorreo()
    {
        var scenario = new Escenario();

        await scenario.Publisher.PublishAsync(new NotificationEvent(NotificationEventKind.PermissionRejected,
            Guid.NewGuid(), "Motivo: no corresponde", scenario.Standard.UserId));
        await scenario.Publisher.PublishAsync(new NotificationEvent(NotificationEventKind.PermissionRejected,
            Guid.NewGuid(), "Motivo: no corresponde", Guid.NewGuid()));

        Assert.Equal(scenario.Standard.UserId, Assert.Single(scenario.Store.Notifications).User);
        Assert.Empty(scenario.Store.Emails);
    }

    [Fact]
    public async Task AbonoResueltoDeUnAdministradorTampocoGeneraCorreo()
    {
        var scenario = new Escenario();

        await scenario.Publisher.PublishAsync(new NotificationEvent(NotificationEventKind.DebtTransferConfirmed,
            Guid.NewGuid(), "Abono confirmado", scenario.Admins[0].UserId));

        Assert.Single(scenario.Store.Notifications);
        Assert.Empty(scenario.Store.Emails);
    }

    [Fact]
    public async Task DosCierresConIdentidadesDistintasGeneranDosAvisosPorAdministrador()
    {
        var scenario = new Escenario();

        await scenario.Publisher.PublishAsync(new NotificationEvent(NotificationEventKind.BusinessDayClosed, Guid.NewGuid(), "Sucursal A"));
        await scenario.Publisher.PublishAsync(new NotificationEvent(NotificationEventKind.BusinessDayClosed, Guid.NewGuid(), "Sucursal A"));

        Assert.Equal(4, scenario.Store.Notifications.Count);
        Assert.Empty(scenario.Store.Emails);
    }

    [Fact]
    public async Task EstandarNoPuedeModificarPreferenciaAdministrativa()
    {
        var scenario = new Escenario();
        var service = new ManageNotificationPreference(scenario.Store,
            new Alcance { UserId = scenario.Standard.UserId }, new Transaccion(), new Reloj());

        await Assert.ThrowsAsync<NotificationPreferenceDeniedException>(() => service.SetAsync(false));

        Assert.Empty(scenario.Store.Disabled);
    }

    [Theory]
    [InlineData(NotificationEventKind.PermissionRequested, true, true)]
    [InlineData(NotificationEventKind.PermissionApproved, false, false)]
    [InlineData(NotificationEventKind.PermissionRejected, false, false)]
    [InlineData(NotificationEventKind.TransferRegistered, true, false)]
    [InlineData(NotificationEventKind.TransferConfirmed, false, false)]
    [InlineData(NotificationEventKind.TransferRejected, false, false)]
    [InlineData(NotificationEventKind.DebtCreated, true, false)]
    [InlineData(NotificationEventKind.DebtCashPayment, true, false)]
    [InlineData(NotificationEventKind.DebtTransferConfirmed, false, false)]
    [InlineData(NotificationEventKind.DebtTransferRejected, false, false)]
    [InlineData(NotificationEventKind.BusinessDayClosed, true, false)]
    [InlineData(NotificationEventKind.TransferOverdue, true, true)]
    [InlineData(NotificationEventKind.BusinessDayOverdue, true, true)]
    public void LaMatrizCentralConservaDestinatariosYCanalesAprobados(NotificationEventKind kind, bool admins, bool email)
    {
        var policy = NotificationDeliveryPolicy.For(kind);

        Assert.Equal(admins, policy.Administrators);
        Assert.Equal(email, policy.Email);
    }

    private sealed class Escenario
    {
        public NotificationRecipient[] Admins { get; } = [new(Guid.NewGuid(), "a@ejemplo.com", true, null), new(Guid.NewGuid(), "b@ejemplo.com", true, null)];
        public NotificationRecipient Standard { get; } = new(Guid.NewGuid(), "c@ejemplo.com", false, Guid.NewGuid());
        public Almacen Store { get; } = new();
        public PublishNotification Publisher { get; }
        public Escenario() => Publisher = new PublishNotification(new Directorio(Admins, Standard), Store, new Transaccion(), new Reloj());
    }
    private sealed class Directorio(NotificationRecipient[] admins, NotificationRecipient standard) : INotificationRecipientDirectory
    {
        public Task<IReadOnlyList<NotificationRecipient>> ListActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NotificationRecipient>>(admins);
        public Task<NotificationRecipient?> FindActiveAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(admins.Append(standard).SingleOrDefault(recipient => recipient.UserId == id));
    }
    private sealed class Almacen : INotificationDeliveryStore
    {
        public HashSet<(string Event, Guid User)> Notifications { get; } = new();
        public HashSet<(string Event, Guid User)> Emails { get; } = new();
        public HashSet<Guid> Disabled { get; } = new();
        public Task<bool> EmailEnabledAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(!Disabled.Contains(userId));
        public Task DeliverAsync(string key, NotificationRecipient recipient, string subject, string body, DateTime at, bool email, CancellationToken cancellationToken)
        {
            if (Notifications.Add((key, recipient.UserId)) && email) Emails.Add((key, recipient.UserId));
            return Task.CompletedTask;
        }
        public Task SetEmailPreferenceAsync(Guid userId, bool enabled, DateTime at, CancellationToken cancellationToken)
        {
            if (enabled) Disabled.Remove(userId); else Disabled.Add(userId);
            return Task.CompletedTask;
        }
    }
    private sealed class Transaccion : IApplicationTransaction
    {
        public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    }
    private sealed class Reloj : IClock { public DateTime UtcNow => new(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc); }
    private sealed class Alcance : IBranchScopeAccessor
    {
        public Guid? UserId { get; set; }
        public Guid? BranchId { get; set; }
        public bool IsGlobal { get; set; }
    }
}
