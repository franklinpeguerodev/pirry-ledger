using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;
using PirryLedger.Core.Notifications.Domain.Errors;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class ProcessEmailQueueTests
{
    private static readonly DateTime UtcNow =
        new(2026, 10, 2, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Envia_los_pendientes_y_no_repite_un_correo_enviado()
    {
        var email = CreateEmail();
        var repository = new InMemoryRepository(email);
        var transporter = new InMemoryTransporter();
        var processor = CreateProcessor(repository, transporter);

        var firstRun = await processor.ExecuteAsync();
        var secondRun = await processor.ExecuteAsync();

        Assert.Equal(1, firstRun.Pending);
        Assert.Equal(1, firstRun.Sent);
        Assert.Empty(firstRun.Failures);
        Assert.Equal(0, secondRun.Pending);
        Assert.Equal(0, secondRun.Sent);
        Assert.Empty(secondRun.Failures);
        Assert.Single(transporter.Sent);
        Assert.Equal(EmailStatus.Sent, email.Status);
        Assert.Equal(1, email.Attempts);
        Assert.Equal(UtcNow, email.SentAtUtc);
        Assert.Equal(1, repository.SavedCount);
    }

    [Fact]
    public async Task Devuelve_a_pendiente_un_correo_que_falla_y_conserva_el_error_en_el_resultado()
    {
        var email = CreateEmail();
        var repository = new InMemoryRepository(email);
        var transporter = new InMemoryTransporter
        {
            Error = new EmailDeliveryException(EmailQueueErrors.Connection)
        };
        var processor = CreateProcessor(repository, transporter);

        var result = await processor.ExecuteAsync();

        var failure = Assert.Single(result.Failures);
        Assert.Equal(1, result.Pending);
        Assert.Equal(0, result.Sent);
        Assert.Equal("ana@example.com", failure.Recipient);
        Assert.Equal(EmailQueueErrors.Connection, failure.Error);
        Assert.Equal(EmailQueueErrors.Connection, email.LastError);
        Assert.Equal(EmailStatus.Pending, email.Status);
        Assert.Equal(1, email.Attempts);
        Assert.Null(email.SentAtUtc);
    }

    [Fact]
    public async Task Rechaza_un_lote_no_positivo()
    {
        var processor = CreateProcessor(
            new InMemoryRepository(),
            new InMemoryTransporter());

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => processor.ExecuteAsync(0));

        Assert.Equal("batch", exception.ParamName);
    }

    private static ProcessEmailQueue CreateProcessor(
        InMemoryRepository repository,
        InMemoryTransporter transporter) =>
        new(repository, transporter, new FixedClock(UtcNow), new BloqueoDeCola(), new ElegibilidadDePrueba());

    private static EmailQueueItem CreateEmail() =>
        EmailQueueItem.Create(
            "ana@example.com",
            "Prueba",
            "Body",
            UtcNow.AddMinutes(-1));

    private sealed class InMemoryRepository(params EmailQueueItem[] emails) : ColaDePrueba(emails);

    private sealed class InMemoryTransporter : IEmailTransporter
    {
        public List<(string Recipient, string Subject, string Body)> Sent { get; } = [];

        public Exception? Error { get; init; }

        public Task SendAsync(
            string recipient,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            if (Error is not null)
            {
                throw Error;
            }

            Sent.Add((recipient, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
