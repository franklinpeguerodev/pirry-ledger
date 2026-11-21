using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;
using PirryLedger.Core.Notifications.Domain.Errors;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class PruebasDelProcesadorDeCorreos
{
    private static readonly DateTime Ahora = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CincoEjecucionesFallidasAgotanIntentosYSextaNoEnvia()
    {
        var email = Correo();
        var repository = new ColaDePrueba(email);
        var transport = new Transporte { Failure = new EmailDeliveryException(EmailQueueErrors.Connection) };
        var gate = new BloqueoDeCola();
        var processor = new ProcessEmailQueue(repository, transport, new Reloj(), gate);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var result = await processor.ExecuteAsync();
            Assert.Equal(1, result.Pending);
            Assert.Equal(attempt == 5 ? 1 : 0, result.Failed);
            Assert.Equal(attempt, email.Attempts);
        }
        var last = await processor.ExecuteAsync();

        Assert.Equal(5, transport.Calls);
        Assert.Equal(0, last.Pending);
        Assert.Equal(EmailStatus.Failed, email.Status);
        Assert.Equal(Ahora, email.LastAttemptAtUtc);
        Assert.Equal(Ahora, email.StatusChangedAtUtc);
        Assert.Equal(EmailQueueErrors.Connection, email.LastError);
        Assert.Null(email.SentAtUtc);
        Assert.False(gate.Active);
    }

    [Fact]
    public async Task OtroEnviadorActivoImpideLeerColaOInvocarTransporte()
    {
        var email = Correo();
        var repository = new ColaDePrueba(email);
        var transport = new Transporte();
        var processor = new ProcessEmailQueue(repository, transport, new Reloj(), new BloqueoDeCola { Busy = true });

        var result = await processor.ExecuteAsync();

        Assert.True(result.AlreadyRunning);
        Assert.Equal(0, repository.Reads);
        Assert.Equal(0, repository.SavedCount);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(EmailStatus.Pending, email.Status);
        Assert.Equal(0, email.Attempts);
    }

    [Fact]
    public async Task ProcessingInterrumpidoQuedaInciertoYNoSeVuelveAEnviar()
    {
        var email = Correo();
        email.Claim(Ahora.AddMinutes(-1));
        var repository = new ColaDePrueba(email);
        var transport = new Transporte();
        var processor = new ProcessEmailQueue(repository, transport, new Reloj(), new BloqueoDeCola());

        var first = await processor.ExecuteAsync();
        var second = await processor.ExecuteAsync();

        Assert.Equal(1, first.Uncertain);
        Assert.Equal(0, second.Uncertain);
        Assert.Equal(EmailStatus.Uncertain, email.Status);
        Assert.Equal(1, email.Attempts);
        Assert.Equal(0, transport.Calls);
        Assert.Null(email.SentAtUtc);
        Assert.Equal(EmailQueueErrors.Uncertain, email.LastError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FalloDuranteEnvioSinConfirmacionQuedaInciertoSinExponerError(bool classified)
    {
        var email = Correo();
        var repository = new ColaDePrueba(email);
        var transport = new Transporte { Failure = classified
            ? new EmailDeliveryException("Error privado que no se expone", true)
            : new InvalidOperationException("Error privado que no se expone") };
        var processor = new ProcessEmailQueue(repository, transport, new Reloj(), new BloqueoDeCola());

        var result = await processor.ExecuteAsync();
        await processor.ExecuteAsync();

        Assert.Equal(1, result.Uncertain);
        Assert.Empty(result.Failures);
        Assert.Equal(1, transport.Calls);
        Assert.Equal(EmailStatus.Uncertain, email.Status);
        Assert.Equal(EmailQueueErrors.Uncertain, email.LastError);
        Assert.Null(email.SentAtUtc);
    }

    [Fact]
    public async Task FalloAlGuardarAceptacionAbortaSinEnviarElRestoDelLote()
    {
        var first = Correo();
        var second = Correo();
        var repository = new ColaDePrueba(first, second) { FailSave = true };
        var transport = new Transporte();
        var gate = new BloqueoDeCola();
        var processor = new ProcessEmailQueue(repository, transport, new Reloj(), gate);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ExecuteAsync());

        Assert.Equal(1, transport.Calls);
        Assert.Equal(EmailStatus.Sent, first.Status);
        Assert.Equal(EmailStatus.Pending, second.Status);
        Assert.Equal(0, second.Attempts);
        Assert.False(gate.Active);
    }

    [Fact]
    public async Task ErrorConocidoNoExponeContenidoArbitrarioEnResultadoNiCola()
    {
        var email = Correo();
        var transport = new Transporte { Failure = new EmailDeliveryException("Error privado que no se expone") };
        var processor = new ProcessEmailQueue(new ColaDePrueba(email), transport, new Reloj(), new BloqueoDeCola());

        var result = await processor.ExecuteAsync();

        Assert.Equal(EmailQueueErrors.Unknown, email.LastError);
        Assert.Equal(EmailQueueErrors.Unknown, Assert.Single(result.Failures).Error);
        Assert.Equal(EmailStatus.Pending, email.Status);
    }

    private static EmailQueueItem Correo() => EmailQueueItem.Create("prueba@ejemplo.com", "Aviso", "Contenido", Ahora);
    [Fact]
    public async Task PerderCoordinacionAbortaAntesDeReclamarOEnviarOtroCorreo()
    {
        var email = Correo();
        var repository = new ColaDePrueba(email);
        var transport = new Transporte();
        var gate = new BloqueoDeCola { MaximumVerifications = 1 };
        var processor = new ProcessEmailQueue(repository, transport, new Reloj(), gate);

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ExecuteAsync());

        Assert.Equal(0, transport.Calls);
        Assert.Equal(0, repository.SavedCount);
        Assert.Equal(EmailStatus.Pending, email.Status);
        Assert.Equal(0, email.Attempts);
        Assert.False(gate.Active);
    }
    private sealed class Reloj : IClock { public DateTime UtcNow => Ahora; }
    private sealed class Transporte : IEmailTransporter
    {
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Failure is not null) throw Failure;
            return Task.CompletedTask;
        }
    }
}
