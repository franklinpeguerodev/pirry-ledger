using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Business.Domain.StateMachines;
using PirryLedger.Core.Contracts.Permissions;

namespace PirryLedger.Business.Tests.Operaciones;

// RF-NEG-02 + RF-NEG-06: cada capacidad del negocio es un caso de uso que se
// prueba sin base de datos ni SMTP (RD-12). El rol lo declara RoleRequirements
// y lo aplica el filtro; aqui se prueba el comportamiento de la operacion.
public sealed class PruebasDeCasosDeUso
{
    private static readonly DateTime Ahora = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Cajero = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Administrador = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static Invoice Emitida(decimal total = 500m, bool isDelivery = false)
    {
        var invoice = Invoice.Create();
        invoice.Issue(new[] { InvoiceItem.Create("Combo", total, 1) }, isDelivery, Ahora);
        return invoice;
    }

    private static Invoice Pagada()
    {
        var invoice = Emitida();
        invoice.Collect(new[] { InvoicePayment.Cash(500m, 500m, Ahora, Cajero) });
        return invoice;
    }

    private static Invoice EnDeuda()
    {
        var invoice = Emitida();
        invoice.LeaveAsDebt("Ana Perez", payments: null);
        return invoice;
    }

    private static Invoice EnVerificacionPendiente()
    {
        var invoice = Emitida();
        invoice.Collect(new[] { InvoicePayment.Transfer(500m, Ahora, Cajero) });
        return invoice;
    }

    [Fact]
    public async Task EmitirUnaFacturaLaGuardaEmitidaConSuTotal()
    {
        // Arrange
        var repository = new FakeInvoiceRepository();
        var useCase = new IssueInvoice(repository, new FakeClock(Ahora));

        // Act
        var invoice = await useCase.ExecuteAsync(
            new[] { new InvoiceItemInput("Combo", 500m, 2) },
            isDelivery: false);

        // Assert
        Assert.Equal(InvoiceState.Issued, invoice.State);
        Assert.Equal(1000m, invoice.Total);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task EmitirSinItemsSeRechazaYNoGuardaNada()
    {
        // Arrange
        var repository = new FakeInvoiceRepository();
        var useCase = new IssueInvoice(repository, new FakeClock(Ahora));

        // Act + Assert
        await Assert.ThrowsAsync<InvoiceConditionNotMetException>(
            () => useCase.ExecuteAsync(Array.Empty<InvoiceItemInput>(), isDelivery: false));
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CobrarEnEfectivoDejaLaFacturaPagada()
    {
        // Arrange
        var invoice = Emitida();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var useCase = new CollectInvoicePayment(repository, new FakeClock(Ahora));

        // Act
        await useCase.ExecuteAsync(
            invoice.Id,
            new[] { new InvoicePaymentInput(PaymentMethod.Cash, 500m, 500m) },
            Cajero);

        // Assert
        Assert.Equal(InvoiceState.Paid, invoice.State);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task CobrarConTransferenciaDejaLaFacturaEnVerificacionPendiente()
    {
        // Arrange
        var invoice = Emitida();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var useCase = new CollectInvoicePayment(repository, new FakeClock(Ahora));

        // Act
        await useCase.ExecuteAsync(
            invoice.Id,
            new[] { new InvoicePaymentInput(PaymentMethod.Transfer, 500m, null) },
            Cajero);

        // Assert
        Assert.Equal(InvoiceState.PendingVerification, invoice.State);
    }

    [Fact]
    public async Task DejarComoDeudaGuardaElNombreYElEstado()
    {
        // Arrange
        var invoice = Emitida();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var useCase = new LeaveInvoiceAsDebt(repository, new FakeClock(Ahora));

        // Act
        await useCase.ExecuteAsync(invoice.Id, "Ana Perez", payments: null, Cajero);

        // Assert
        Assert.Equal(InvoiceState.InDebt, invoice.State);
        Assert.Equal("Ana Perez", invoice.DebtorName);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task ConfirmarUnaTransferenciaDejaLaFacturaPagada()
    {
        // Arrange
        var invoice = EnVerificacionPendiente();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var useCase = new ConfirmInvoiceTransfer(repository);

        // Act
        await useCase.ExecuteAsync(invoice.Id);

        // Assert
        Assert.Equal(InvoiceState.Paid, invoice.State);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task RechazarUnaTransferenciaDejaLaFacturaEnDeuda()
    {
        // Arrange
        var invoice = EnVerificacionPendiente();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var useCase = new RejectInvoiceTransfer(repository);

        // Act
        await useCase.ExecuteAsync(invoice.Id, "Ana Perez");

        // Assert
        Assert.Equal(InvoiceState.InDebt, invoice.State);
        Assert.Equal("Ana Perez", invoice.DebtorName);
    }

    [Fact]
    public async Task AbonarLaDeudaQueCubreElTotalDejaLaFacturaPagada()
    {
        // Arrange
        var invoice = EnDeuda();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var useCase = new RegisterDebtPayment(repository, new FakeClock(Ahora));

        // Act
        await useCase.ExecuteAsync(invoice.Id, 500m, Administrador);

        // Assert
        Assert.Equal(InvoiceState.Paid, invoice.State);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AbonarUnaFacturaQueNoEstaEnDeudaSeRechazaYNoGuarda()
    {
        // Arrange
        var invoice = Emitida();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var useCase = new RegisterDebtPayment(repository, new FakeClock(Ahora));

        // Act + Assert
        await Assert.ThrowsAsync<InvoiceRuleViolationException>(
            () => useCase.ExecuteAsync(invoice.Id, 100m, Administrador));
        Assert.Equal(InvoiceState.Issued, invoice.State);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AnularUnaFacturaEmitidaNoPidePermiso()
    {
        // Arrange: Issued -> Cancelled no exige permiso (decision 3).
        var invoice = Emitida();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var authority = new FakePermissionAuthority();
        var useCase = new CancelInvoice(repository, authority, new FakePermissionRequestGateway());

        // Act
        await useCase.ExecuteAsync(invoice.Id, Cajero);

        // Assert
        Assert.Equal(InvoiceState.Cancelled, invoice.State);
        Assert.Equal(0, authority.ConsumeCalls);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AnularUnaFacturaPagadaConsumeElPermisoYAnula()
    {
        // Arrange
        var invoice = Pagada();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var authority = new FakePermissionAuthority();
        authority.Grant(Cajero, PermissionNames.CancelInvoice(invoice.Id));
        var gateway = new FakePermissionRequestGateway();
        var useCase = new CancelInvoice(repository, authority, gateway);

        // Act
        await useCase.ExecuteAsync(invoice.Id, Cajero);

        // Assert
        Assert.Equal(InvoiceState.Cancelled, invoice.State);
        Assert.Equal(1, authority.ConsumeCalls);
        Assert.Equal(0, gateway.CreatedCount);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AnularSinPermisoCreaLaSolicitudYRechaza()
    {
        // Arrange
        var invoice = Pagada();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var authority = new FakePermissionAuthority();
        var gateway = new FakePermissionRequestGateway();
        var useCase = new CancelInvoice(repository, authority, gateway);

        // Act
        var error = await Assert.ThrowsAsync<CancellationPermissionRequiredException>(
            () => useCase.ExecuteAsync(invoice.Id, Cajero));

        // Assert: la factura no se guarda y la solicitud se creo.
        Assert.False(error.AlreadyPending);
        Assert.Equal(1, gateway.CreatedCount);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AnularSinPermisoConUnaSolicitudPendienteNoLaDuplicaYRechaza()
    {
        // Arrange
        var invoice = Pagada();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var authority = new FakePermissionAuthority();
        var gateway = new FakePermissionRequestGateway();
        gateway.SeedPending(Cajero, PermissionNames.CancelInvoice(invoice.Id));
        var useCase = new CancelInvoice(repository, authority, gateway);

        // Act
        var error = await Assert.ThrowsAsync<CancellationPermissionRequiredException>(
            () => useCase.ExecuteAsync(invoice.Id, Cajero));

        // Assert
        Assert.True(error.AlreadyPending);
        Assert.Equal(0, gateway.CreatedCount);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AnularUnaFacturaEnVerificacionPendienteSeRechaza()
    {
        // Arrange
        var invoice = EnVerificacionPendiente();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var useCase = new CancelInvoice(
            repository,
            new FakePermissionAuthority(),
            new FakePermissionRequestGateway());

        // Act + Assert
        await Assert.ThrowsAsync<InvalidInvoiceTransitionException>(
            () => useCase.ExecuteAsync(invoice.Id, Cajero));
        Assert.Equal(InvoiceState.PendingVerification, invoice.State);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task EditarLosItemsGuardaElHistorialYAvisa()
    {
        // Arrange
        var invoice = Emitida();
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var notifier = new FakeInvoiceEditedNotifier();
        var useCase = new EditInvoiceItems(repository, new FakeClock(Ahora), notifier);

        // Act
        await useCase.ExecuteAsync(
            invoice.Id,
            new[] { new InvoiceItemInput("Combo nuevo", 700m, 1) },
            Cajero);

        // Assert
        Assert.Single(invoice.Edits);
        Assert.Equal(1, notifier.Notifications);
        Assert.Equal(700m, invoice.Total);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task MarcarNoEntregadaUnaFacturaDeDeliveryLaDejaNoEntregada()
    {
        // Arrange
        var invoice = Emitida(isDelivery: true);
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var useCase = new MarkInvoiceAsNotDelivered(repository);

        // Act
        await useCase.ExecuteAsync(invoice.Id);

        // Assert
        Assert.Equal(InvoiceState.NotDelivered, invoice.State);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task ListarFacturasDevuelveLasGuardadas()
    {
        // Arrange
        var repository = new FakeInvoiceRepository();
        repository.Seed(Emitida());
        repository.Seed(Emitida());
        var useCase = new ListInvoices(repository);

        // Act
        var invoices = await useCase.ExecuteAsync();

        // Assert
        Assert.Equal(2, invoices.Count);
    }

    [Fact]
    public async Task ConsultarUnaFacturaInexistenteSeRechaza()
    {
        // Arrange
        var useCase = new GetInvoice(new FakeInvoiceRepository());

        // Act + Assert
        await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => useCase.ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListarElHistorialDeEdicionDevuelveLasEdiciones()
    {
        // Arrange
        var invoice = Emitida();
        invoice.EditItems(
            new[] { InvoiceItem.Create("Combo nuevo", 700m, 1) },
            Ahora,
            Cajero,
            new FakeInvoiceEditedNotifier());
        var repository = new FakeInvoiceRepository();
        repository.Seed(invoice);
        var useCase = new ListInvoiceEdits(repository);

        // Act
        var edits = await useCase.ExecuteAsync(invoice.Id);

        // Assert
        Assert.Single(edits);
    }
}
