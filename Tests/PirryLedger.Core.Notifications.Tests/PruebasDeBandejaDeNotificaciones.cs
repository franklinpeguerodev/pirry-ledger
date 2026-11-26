using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class PruebasDeBandejaDeNotificaciones
{
    private static readonly DateTime Ahora = new(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ConsultarSoloDevuelveAvisosPropiosSinMarcarlosComoVistos()
    {
        var scenario = new Escenario();

        var result = await scenario.Inbox.ListAsync();

        Assert.Equal(scenario.Own.Id, Assert.Single(result).Id);
        Assert.Null(scenario.Own.ReadAtUtc);
        Assert.Null(scenario.Foreign.ReadAtUtc);
    }

    [Fact]
    public async Task MarcarUnAvisoAjenoSeRechazaSinCambiarNingunAviso()
    {
        var scenario = new Escenario();

        await Assert.ThrowsAsync<NotificationNotFoundException>(() => scenario.Inbox.MarkReadAsync(scenario.Foreign.Id));

        Assert.Null(scenario.Own.ReadAtUtc);
        Assert.Null(scenario.Foreign.ReadAtUtc);
    }

    [Fact]
    public async Task MarcarTodosSoloAfectaLosPropiosYRepetirNoCambiaSuFecha()
    {
        var scenario = new Escenario();

        var first = await scenario.Inbox.MarkAllReadAsync();
        scenario.Clock.UtcNow = Ahora.AddHours(1);
        var repeated = await scenario.Inbox.MarkAllReadAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, repeated);
        Assert.Equal(Ahora, scenario.Own.ReadAtUtc);
        Assert.Null(scenario.Foreign.ReadAtUtc);
    }

    [Fact]
    public async Task MarcarIndividualUsaElRelojYNoVuelveAMarcarUnoVisto()
    {
        var scenario = new Escenario();

        await scenario.Inbox.MarkReadAsync(scenario.Own.Id);
        scenario.Clock.UtcNow = Ahora.AddHours(1);
        await scenario.Inbox.MarkReadAsync(scenario.Own.Id);

        Assert.Equal(Ahora, scenario.Own.ReadAtUtc);
        Assert.Null(scenario.Foreign.ReadAtUtc);
    }

    [Fact]
    public async Task SinIdentidadNoSeConsultaNiModificaLaBandeja()
    {
        var scenario = new Escenario();
        scenario.Scope.UserId = null;

        await Assert.ThrowsAsync<NotificationSessionRequiredException>(() => scenario.Inbox.ListAsync());
        await Assert.ThrowsAsync<NotificationSessionRequiredException>(() => scenario.Inbox.MarkReadAsync(scenario.Own.Id));
        await Assert.ThrowsAsync<NotificationSessionRequiredException>(() => scenario.Inbox.MarkAllReadAsync());

        Assert.Null(scenario.Own.ReadAtUtc);
        Assert.Null(scenario.Foreign.ReadAtUtc);
    }

    [Fact]
    public void FechaDeLecturaNoUtcSeRechazaSinModificarAviso()
    {
        var notification = Notification.Create(Guid.NewGuid(), "Asunto", "Contenido", Ahora);

        Assert.Throws<ArgumentException>(() => notification.MarkRead(DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified)));

        Assert.Null(notification.ReadAtUtc);
    }

    private sealed class Escenario
    {
        public Alcance Scope { get; } = new() { UserId = Guid.NewGuid() };
        public Reloj Clock { get; } = new();
        public Notification Own { get; }
        public Notification Foreign { get; } = Notification.Create(Guid.NewGuid(), "Otro", "Contenido", Ahora);
        public ManageNotificationInbox Inbox { get; }
        public Escenario()
        {
            Own = Notification.Create(Scope.UserId!.Value, "Propio", "Contenido", Ahora);
            Inbox = new ManageNotificationInbox(new Memoria([Own, Foreign]), Scope, Clock);
        }
    }
    private sealed class Reloj : IClock { public DateTime UtcNow { get; set; } = Ahora; }
    private sealed class Alcance : IBranchScopeAccessor
    {
        public Guid? UserId { get; set; }
        public Guid? BranchId { get; set; }
        public bool IsGlobal { get; set; }
    }
    private sealed class Memoria(List<Notification> notifications) : INotificationInboxRepository
    {
        public Task<IReadOnlyList<Notification>> ListOwnAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Notification>>(notifications.Where(notification => notification.RecipientUserId == userId).ToArray());
        public Task<bool> MarkReadAsync(Guid userId, Guid id, DateTime at, CancellationToken cancellationToken)
        {
            var own = notifications.SingleOrDefault(notification => notification.Id == id && notification.RecipientUserId == userId);
            own?.MarkRead(at);
            return Task.FromResult(own is not null);
        }
        public Task<int> MarkAllReadAsync(Guid userId, DateTime at, CancellationToken cancellationToken)
        {
            var own = notifications.Where(notification => notification.RecipientUserId == userId && notification.ReadAtUtc is null).ToArray();
            foreach (var notification in own) notification.MarkRead(at);
            return Task.FromResult(own.Length);
        }
    }
}
