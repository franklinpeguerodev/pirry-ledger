using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class PruebasDeRevalidacionDeCorreo
{
    private static readonly DateTime Ahora = new(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task CuentaInactivaRolEstandarOPreferenciaDesactivadaCancelanAntesDeSmtp(bool active, bool admin, bool enabled)
    {
        var user = Guid.NewGuid();
        var email = EmailQueueItem.ForNotification(Guid.NewGuid(), user, "prueba@ejemplo.com", "Aviso", "Contenido", Ahora);
        var transport = new Transporte();
        var processor = new ProcessEmailQueue(new Cola(email), transport, new Reloj(), new BloqueoDeCola(), new ElegibilidadDePrueba(),
            new Directorio(active ? new NotificationRecipient(user, email.Recipient, admin, null) : null), new Almacen(enabled));

        var result = await processor.ExecuteAsync();

        Assert.Equal(EmailStatus.Cancelled, email.Status);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(0, email.Attempts);
        Assert.Equal(0, result.Sent);
        Assert.Null(email.SentAtUtc);
    }

    [Fact]
    public async Task PreferenciaNoCancelaCorreoDeActivacionORecuperacion()
    {
        var email = EmailQueueItem.Create("prueba@ejemplo.com", "Activación", "Contenido", Ahora);
        var transport = new Transporte();
        var processor = new ProcessEmailQueue(new Cola(email), transport, new Reloj(), new BloqueoDeCola(), new ElegibilidadDePrueba(), new Directorio(null), new Almacen(false));

        var result = await processor.ExecuteAsync();

        Assert.Equal(EmailStatus.Sent, email.Status);
        Assert.Equal(1, transport.Calls);
        Assert.Equal(1, result.Sent);
    }

    [Theory]
    [InlineData(NotificationEventKind.TransferOverdue)]
    [InlineData(NotificationEventKind.BusinessDayOverdue)]
    public async Task AtrasoResueltoCancelaCorreoSinConsumirIntento(NotificationEventKind kind)
    {
        var user = Guid.NewGuid();
        var email = EmailQueueItem.ForNotification(Guid.NewGuid(), user, "prueba@ejemplo.com", "Aviso", "Contenido", Ahora);
        var transport = new Transporte();
        var source = new Origen(false);
        var store = new Control($"{kind}:{Guid.NewGuid():D}");
        var eligibility = new OverdueNotificationEmailEligibility(store, [source], new Reloj());
        var processor = new ProcessEmailQueue(new Cola(email), transport, new Reloj(), new BloqueoDeCola(), eligibility,
            new Directorio(new(user, email.Recipient, true, null)), new Almacen(true));

        var result = await processor.ExecuteAsync();

        Assert.Equal(EmailStatus.Cancelled, email.Status);
        Assert.Equal(0, email.Attempts);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(1, result.Cancelled);
        Assert.Equal(1, source.Checks);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ErrorDeComprobacionOProveedorAusenteConservaPendienteSinIntento(bool providerPresent)
    {
        var user = Guid.NewGuid();
        var email = EmailQueueItem.ForNotification(Guid.NewGuid(), user, "prueba@ejemplo.com", "Aviso", "Contenido", Ahora);
        var transport = new Transporte();
        var queue = new Cola(email);
        var eligibility = new OverdueNotificationEmailEligibility(new Control($"TransferOverdue:{Guid.NewGuid():D}"),
            providerPresent ? [new Origen(true, true)] : [], new Reloj());
        var processor = new ProcessEmailQueue(queue, transport, new Reloj(), new BloqueoDeCola(), eligibility,
            new Directorio(new(user, email.Recipient, true, null)), new Almacen(true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ExecuteAsync());

        Assert.Equal(EmailStatus.Pending, email.Status);
        Assert.Equal(0, email.Attempts);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(0, queue.SavedCount);
    }

    [Fact]
    public async Task OtroEventoNoNecesitaOrigenDeAtrasos()
    {
        var eligibility = new OverdueNotificationEmailEligibility(new Control($"PermissionRequested:{Guid.NewGuid():D}"), [], new Reloj());

        Assert.True(await eligibility.IsRelevantAsync(Guid.NewGuid()));
    }

    private sealed class Control(string key) : IOverdueNotificationStore
    {
        public Task<string?> FindNotificationEventKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<string?>(key);
        public Task<bool> IsRecordedAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryRecordAsync(string key, DateTime now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Origen(bool relevant, bool fail = false) : IOverdueNotificationSource
    {
        public int Checks { get; private set; }
        public bool Handles(NotificationEventKind kind) => true;
        public Task<bool> IsRelevantAsync(NotificationEventKind kind, Guid id, DateTime now, CancellationToken cancellationToken = default)
        {
            Checks++;
            if (fail) throw new InvalidOperationException("Fallo controlado de comprobación.");
            return Task.FromResult(relevant);
        }
        public Task<IReadOnlyList<OverdueNotificationCandidate>> FindCandidatesAsync(DateTime now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<NotificationEvent?> RevalidateAsync(OverdueNotificationCandidate candidate, DateTime now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Cola(EmailQueueItem email) : ColaDePrueba(email);

    private sealed class Transporte : IEmailTransporter
    {
        public int Calls { get; private set; }
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
    private sealed class Directorio(NotificationRecipient? recipient) : INotificationRecipientDirectory
    {
        public Task<NotificationRecipient?> FindActiveAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(recipient);
        public Task<IReadOnlyList<NotificationRecipient>> ListActiveAdministratorsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Almacen(bool enabled) : INotificationDeliveryStore
    {
        public Task<bool> EmailEnabledAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(enabled);
        public Task DeliverAsync(string key, NotificationRecipient recipient, string subject, string body, DateTime at, bool email, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetEmailPreferenceAsync(Guid userId, bool value, DateTime at, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class Reloj : IClock { public DateTime UtcNow => Ahora; }
}
