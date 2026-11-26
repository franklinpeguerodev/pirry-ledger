using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class PruebasDeAvisosDeAtraso
{
    [Fact]
    public async Task RepetirRevisionNoPublicaDosVecesElMismoCaso()
    {
        var source = new Origen();
        var store = new Almacen();
        var publisher = new Publicador();
        var processor = new ProcessOverdueNotifications([source], store, publisher, new Transaccion(store, publisher), new Reloj());

        await processor.ExecuteAsync();
        await processor.ExecuteAsync();

        Assert.Single(publisher.Events);
        Assert.Single(store.Keys);
    }

    [Fact]
    public async Task CincuentaNuevosPorRevisionSinQueLosAnterioresConsumanCupo()
    {
        var source = new Origen();
        source.Candidates = Enumerable.Range(0, 101).Select(index => new OverdueNotificationCandidate(
            NotificationEventKind.TransferOverdue, Guid.NewGuid(), new Reloj().UtcNow.AddMinutes(index - 200))).ToArray();
        var store = new Almacen();
        foreach (var candidate in source.Candidates.Take(50)) store.Keys.Add(candidate.Key);
        var publisher = new Publicador();
        var processor = new ProcessOverdueNotifications([source], store, publisher, new Transaccion(store, publisher), new Reloj());

        var first = await processor.ExecuteAsync();
        var second = await processor.ExecuteAsync();
        await processor.ExecuteAsync();

        Assert.Equal(50, first.Recorded);
        Assert.Equal(1, second.Recorded);
        Assert.Equal(51, publisher.Events.Count);
        Assert.Equal(101, store.Keys.Count);
        Assert.Equal(51, publisher.Events.Select(item => item.Key).Distinct().Count());
    }

    [Fact]
    public async Task CasoResueltoDuranteRevisionNoSeRegistraNiSePublica()
    {
        var source = new Origen { Relevant = false };
        var store = new Almacen();
        var publisher = new Publicador();
        var processor = new ProcessOverdueNotifications([source], store, publisher, new Transaccion(store, publisher), new Reloj());

        var result = await processor.ExecuteAsync();

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Recorded);
        Assert.Empty(store.Keys);
        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task FalloDeAvisoRevierteMarcaYContinuaSinDuplicarAlReintentar()
    {
        var source = new Origen();
        var broken = source.Candidates[0];
        source.Candidates = [broken, broken with { SourceId = Guid.NewGuid(), DueAtUtc = broken.DueAtUtc.AddSeconds(1) }];
        var store = new Almacen();
        var publisher = new Publicador { FailId = broken.SourceId };
        var processor = new ProcessOverdueNotifications([source], store, publisher, new Transaccion(store, publisher), new Reloj());

        var first = await processor.ExecuteAsync();
        Assert.Equal(1, first.Failures);
        Assert.Equal(1, first.Recorded);
        Assert.DoesNotContain(broken.Key, store.Keys);
        Assert.Single(publisher.Events);
        publisher.FailId = null;
        var retry = await processor.ExecuteAsync();
        await processor.ExecuteAsync();

        Assert.Equal(1, retry.Recorded);
        Assert.Equal(2, store.Keys.Count);
        Assert.Equal(2, publisher.Events.Count);
        Assert.Equal(2, publisher.Events.Select(item => item.Key).Distinct().Count());
    }

    [Fact]
    public async Task FalloDeUnOrigenNoBloqueaLosDemas()
    {
        var store = new Almacen();
        var publisher = new Publicador();
        var processor = new ProcessOverdueNotifications([new Origen { FailQuery = true }, new Origen()],
            store, publisher, new Transaccion(store, publisher), new Reloj());

        var result = await processor.ExecuteAsync();

        Assert.Equal(1, result.Failures);
        Assert.Equal(1, result.Recorded);
        Assert.Single(publisher.Events);
    }

    [Fact]
    public async Task CancelacionNoSeConvierteEnFalloIndividual()
    {
        var store = new Almacen();
        var publisher = new Publicador();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var processor = new ProcessOverdueNotifications([new Origen()], store, publisher, new Transaccion(store, publisher), new Reloj());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ExecuteAsync(cancellation.Token));

        Assert.Empty(store.Keys);
        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task FalloDeOrigenNoImpideLaFaseDeCorreoDelTrabajador()
    {
        var store = new Almacen();
        var publisher = new Publicador();
        var reminders = new ProcessOverdueNotifications([new Origen { FailQuery = true }], store, publisher,
            new Transaccion(store, publisher), new Reloj());
        var email = EmailQueueItem.Create("prueba@ejemplo.com", "Activación", "Contenido", new Reloj().UtcNow);
        var transport = new Transporte();
        var sender = new ProcessEmailQueue(new ColaDePrueba(email), transport, new Reloj(), new BloqueoDeCola(), new ElegibilidadDePrueba());

        var result = await new ProcessNotifications(reminders, sender).ExecuteAsync();

        Assert.Equal(1, result.Overdue.Failures);
        Assert.Equal(1, result.Email.Sent);
        Assert.Equal(1, transport.Calls);
        Assert.Empty(store.Keys);
        Assert.Empty(publisher.Events);
    }

    private sealed class Transporte : IEmailTransporter
    {
        public int Calls { get; private set; }
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class Reloj : IClock
    {
        public DateTime UtcNow => new(2026, 11, 25, 20, 0, 0, DateTimeKind.Utc);
    }
    private sealed class Transaccion(Almacen store, Publicador publisher) : IApplicationTransaction
    {
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        {
            var keys = store.Keys.ToArray();
            var events = publisher.Events.ToArray();
            try { return await action(); }
            catch
            {
                store.Keys.Clear(); foreach (var key in keys) store.Keys.Add(key);
                publisher.Events.Clear(); publisher.Events.AddRange(events);
                throw;
            }
        }
    }
    private sealed class Origen : IOverdueNotificationSource
    {
        public OverdueNotificationCandidate[] Candidates { get; set; } = [new(NotificationEventKind.TransferOverdue, Guid.NewGuid(), new Reloj().UtcNow.AddSeconds(-1))];
        public bool Relevant { get; init; } = true;
        public bool FailQuery { get; init; }
        public bool Handles(NotificationEventKind kind) => kind == NotificationEventKind.TransferOverdue;
        public Task<IReadOnlyList<OverdueNotificationCandidate>> FindCandidatesAsync(DateTime nowUtc, CancellationToken cancellationToken = default) =>
            FailQuery ? throw new InvalidOperationException("Fallo controlado del origen.") : Task.FromResult<IReadOnlyList<OverdueNotificationCandidate>>(Candidates);
        public Task<NotificationEvent?> RevalidateAsync(OverdueNotificationCandidate candidate, DateTime nowUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<NotificationEvent?>(Relevant ? new(candidate.Kind, candidate.SourceId, "Transferencia atrasada") : null);
        public Task<bool> IsRelevantAsync(NotificationEventKind kind, Guid sourceId, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult(Relevant);
    }
    private sealed class Almacen : IOverdueNotificationStore
    {
        public HashSet<string> Keys { get; } = new();
        public Task<bool> IsRecordedAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(Keys.Contains(key));
        public Task<bool> TryRecordAsync(string key, DateTime now, CancellationToken cancellationToken = default) => Task.FromResult(Keys.Add(key));
        public Task<string?> FindNotificationEventKeyAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
    private sealed class Publicador : INotificationPublisher
    {
        public List<NotificationEvent> Events { get; } = new();
        public Guid? FailId { get; set; }
        public Task PublishAsync(NotificationEvent item, CancellationToken cancellationToken = default)
        {
            Events.Add(item);
            if (item.SourceId == FailId) throw new InvalidOperationException("Fallo controlado después de guardar aviso.");
            return Task.CompletedTask;
        }
    }
}
