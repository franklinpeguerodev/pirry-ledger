using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDePropiedadDeFactura
{
    private static readonly DateTime Ahora = new(2026, 11, 19, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListarNoExponeFacturasDeOtroEmisorDeLaMismaSucursal()
    {
        // Arrange
        await using var context = Context();
        var scope = new Alcance { BranchId = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var own = InvoiceFor(scope.BranchId.Value, scope.UserId.Value);
        context.AddRange(own, InvoiceFor(scope.BranchId.Value, Guid.NewGuid()),
            InvoiceFor(Guid.NewGuid(), scope.UserId.Value));
        await context.SaveChangesAsync();
        var useCase = new ListInvoices(new InvoiceRepository(context, scope));

        // Act
        var visible = await useCase.ExecuteAsync();

        // Assert
        Assert.Equal(own.Id, Assert.Single(visible).Id);
    }

    [Fact]
    public async Task NoSePuedeConcluirUnaFacturaAjenaAunqueSeConozcaSuId()
    {
        // Arrange
        await using var context = Context();
        var scope = new Alcance { BranchId = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var other = InvoiceFor(scope.BranchId.Value, Guid.NewGuid());
        context.Add(other);
        await context.SaveChangesAsync();
        var useCase = new MarkInvoiceAsNotDelivered(new InvoiceRepository(context, scope));

        // Act
        await Assert.ThrowsAsync<InvoiceNotFoundException>(() => useCase.ExecuteAsync(other.Id));

        // Assert
        Assert.Equal(InvoiceState.Issued, other.State);
        Assert.Empty(other.Payments);
        Assert.Empty(other.Edits);
    }

    [Fact]
    public async Task ElAdministradorConsultaTodosLosEmisoresYSucursales()
    {
        // Arrange
        await using var context = Context();
        context.AddRange(InvoiceFor(Guid.NewGuid(), Guid.NewGuid()),
            InvoiceFor(Guid.NewGuid(), Guid.NewGuid()));
        await context.SaveChangesAsync();
        var repository = new InvoiceRepository(context, new Alcance { IsGlobal = true });

        // Act
        var visible = await repository.ListAsync();

        // Assert
        Assert.Equal(2, visible.Count);
    }

    [Fact]
    public async Task SinIdentidadEstablecidaNoSeDevuelvenFacturas()
    {
        // Arrange
        await using var context = Context();
        var invoice = InvoiceFor(Guid.NewGuid(), Guid.NewGuid());
        context.Add(invoice);
        await context.SaveChangesAsync();
        var repository = new InvoiceRepository(context, new Alcance());

        // Act
        var found = await repository.GetByIdAsync(invoice.Id);

        // Assert
        Assert.Null(found);
    }

    private static BusinessDbContext Context() => new(new DbContextOptionsBuilder<BusinessDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Invoice InvoiceFor(Guid branch, Guid issuer)
    {
        var invoice = Invoice.Create();
        invoice.Issue([InvoiceItem.Create("Producto", 10m, 1)], true, Ahora,
            branch, "Sucursal", "Dirección", issuer);
        return invoice;
    }

    private sealed class Alcance : IBranchScopeAccessor
    {
        public Guid? BranchId { get; set; }
        public Guid? UserId { get; set; }
        public bool IsGlobal { get; set; }
    }
}
