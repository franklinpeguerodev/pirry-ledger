using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Core.Contracts.Notifications;

namespace PirryLedger.Business.Tests.Operaciones;

public sealed class PruebasDeEventosDeFacturacion
{
    private static readonly DateTime Ahora = new(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RechazarTransferenciaSinMotivoNoCambiaFacturaNiPagos()
    {
        var invoice = Factura();
        var payment = InvoicePayment.Transfer(100m, Ahora, invoice.IssuedByUserId);
        invoice.Collect([payment]);

        Assert.Throws<InvoiceRuleViolationException>(() => invoice.RejectTransfer("Cliente"));

        Assert.Equal(InvoiceState.PendingVerification, invoice.State);
        Assert.Equal(PaymentStatus.Registered, payment.Status);
        Assert.Null(payment.RejectionReason);
        Assert.Null(payment.ResolvedAtUtc);
    }

    [Fact]
    public async Task FacturaFiadaPublicaDeudaYTransferenciaParcialConIdentidadesDistintas()
    {
        var invoice = Factura();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var notifications = new Avisos();
        var service = new LeaveInvoiceAsDebt(repository, new FakeClock(Ahora), notifications);

        await service.ExecuteAsync(invoice.Id, "Cliente",
            [new InvoicePaymentInput(PaymentMethod.Cash, 40m, null), new InvoicePaymentInput(PaymentMethod.Transfer, 30m, null)],
            invoice.IssuedByUserId);

        Assert.Equal(InvoiceState.InDebt, invoice.State);
        Assert.Equal(2, notifications.Items.Count);
        Assert.Contains(notifications.Items, item => item.Kind == NotificationEventKind.DebtCreated && item.SourceId == invoice.Id);
        var transfer = invoice.Payments.Single(payment => payment.Method == PaymentMethod.Transfer);
        Assert.Contains(notifications.Items, item => item.Kind == NotificationEventKind.TransferRegistered && item.SourceId == transfer.Id);
    }

    [Fact]
    public async Task ConfirmacionDeVentaAvisaAlEmisorYConservaResolutorDelServidor()
    {
        var invoice = Factura();
        var payment = InvoicePayment.Transfer(100m, Ahora, invoice.IssuedByUserId);
        invoice.Collect([payment]);
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var notifications = new Avisos();
        var admin = new PruebasDeCasosDeJornada.Alcance { IsGlobal = true, UserId = Guid.NewGuid() };
        var service = new ConfirmInvoiceTransfer(repository, notifications, clock: new FakeClock(Ahora.AddHours(1)), scope: admin);

        await service.ExecuteAsync(invoice.Id);

        var published = Assert.Single(notifications.Items);
        Assert.Equal(NotificationEventKind.TransferConfirmed, published.Kind);
        Assert.Equal(invoice.IssuedByUserId, published.RecipientUserId);
        Assert.Equal(payment.Id, published.SourceId);
        Assert.Equal(admin.UserId, payment.ResolvedByUserId);
        Assert.Equal(Ahora.AddHours(1), payment.ResolvedAtUtc);
    }

    [Fact]
    public async Task RechazoSinMotivoNoPublicaYRechazoValidoConservaElMotivo()
    {
        var invoice = Factura();
        var payment = InvoicePayment.Transfer(100m, Ahora, invoice.IssuedByUserId);
        invoice.Collect([payment]);
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var notifications = new Avisos();
        var service = new RejectInvoiceTransfer(repository, notifications, clock: new FakeClock(Ahora));

        await Assert.ThrowsAsync<InvoiceRuleViolationException>(() => service.ExecuteAsync(invoice.Id, "Cliente", reason: " "));

        Assert.Equal(InvoiceState.PendingVerification, invoice.State);
        Assert.Equal(PaymentStatus.Registered, payment.Status);
        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(notifications.Items);
        await service.ExecuteAsync(invoice.Id, "Cliente", reason: "No llegó");
        Assert.Equal("No llegó", payment.RejectionReason);
        Assert.Contains("No llegó", Assert.Single(notifications.Items).Detail);
        Assert.Equal(NotificationEventKind.TransferRejected, notifications.Items[0].Kind);
    }

    [Fact]
    public async Task CerrarReabrirYCerrarPublicaDosCierresSinAvisoDeReapertura()
    {
        var invoice = Factura();
        invoice.MarkAsNotDelivered();
        var day = BusinessDay.Open(invoice.BranchId, Ahora);
        invoice.AssignBusinessDay(day.Id);
        var repository = new PruebasDeCasosDeJornada.Memoria();
        repository.Days.Add(day);
        repository.Invoices.Add(invoice);
        var notifications = new Avisos();
        var service = new ManageBusinessDays(repository,
            new PruebasDeCasosDeJornada.Alcance { BranchId = invoice.BranchId, UserId = invoice.IssuedByUserId },
            new PruebasDeCasosDeJornada.Pendientes(), new FakeClock(Ahora.AddHours(1)), notifications);

        await service.CloseAsync(invoice.BranchId, day.Id);
        var first = day.LastClosingEventId;
        await service.ReopenAsync(invoice.BranchId, day.Id);
        Assert.Single(notifications.Items);
        await service.CloseAsync(invoice.BranchId, day.Id);

        Assert.NotEqual(first, day.LastClosingEventId);
        Assert.Equal(2, notifications.Items.Count);
        Assert.All(notifications.Items, item => Assert.Equal(NotificationEventKind.BusinessDayClosed, item.Kind));
        Assert.NotEqual(notifications.Items[0].Key, notifications.Items[1].Key);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolucionDelAbonoAvisaAlRegistradorAunqueOtroEmitieraLaFactura(bool rechazo)
    {
        var invoice = Factura();
        invoice.LeaveAsDebt("Cliente", null);
        var registrar = Guid.NewGuid();
        var scope = new PruebasDeCasosDeJornada.Alcance { UserId = registrar, BranchId = invoice.BranchId };
        var notifications = new Avisos();
        var service = new ManageDebtPayments(new Deudas(invoice), scope,
            new PruebasDeCasosDeJornada.Pendientes(), new FakeClock(Ahora), notifications);
        await service.RegisterAsync(invoice.Id, 100m, PaymentMethod.Transfer);
        var payment = Assert.Single(invoice.Payments);
        scope.IsGlobal = true;
        scope.UserId = Guid.NewGuid();

        if (rechazo) await service.RejectAsync(invoice.Id, payment.Id, "No llegó");
        else await service.ConfirmAsync(invoice.Id, payment.Id);

        Assert.Equal(2, notifications.Items.Count);
        var resolved = notifications.Items[1];
        Assert.Equal(registrar, resolved.RecipientUserId);
        Assert.NotEqual(invoice.IssuedByUserId, resolved.RecipientUserId);
        Assert.Equal(payment.Id, resolved.SourceId);
        Assert.Equal(rechazo ? NotificationEventKind.DebtTransferRejected : NotificationEventKind.DebtTransferConfirmed, resolved.Kind);
    }

    private static Invoice Factura()
    {
        var invoice = Invoice.Create();
        invoice.Issue([InvoiceItem.Create("Combo", 100m, 1)], true, Ahora,
            Guid.NewGuid(), "Sucursal A", "Dirección", Guid.NewGuid(), "Ana");
        return invoice;
    }
    private sealed class Avisos : INotificationPublisher
    {
        public List<NotificationEvent> Items { get; } = new();
        public Task PublishAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken = default)
        {
            Items.Add(notificationEvent);
            return Task.CompletedTask;
        }
    }
    private sealed class Deudas(Invoice invoice) : IDebtRepository
    {
        public Task<Invoice?> FindAsync(Guid id, Guid? branchId, CancellationToken cancellationToken) => Task.FromResult<Invoice?>(invoice);
        public Task<IReadOnlyList<Invoice>> ListAsync(Guid? branchId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Invoice>>([invoice]);
        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<T> InInvoiceTransactionAsync<T>(Guid id, Guid? branchId, Func<Invoice, Task<T>> action, CancellationToken cancellationToken) => action(invoice);
    }
}
