using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Ports;
using PirryLedger.Business.Infrastructure.Persistence;

namespace PirryLedger.Business.Tests.Persistencia;

// RD-09: la factura se guarda en sus propias tablas y se recupera con sus
// items, pagos e historial. La prueba usa EF InMemory: sin base real y sin
// SMTP (RD-12). El reinicio de la aplicacion se prueba en la fase 4, cuando
// exista una operacion que escriba.
public sealed class PruebasDePersistencia
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 19, 0, 0, DateTimeKind.Utc);

    private static readonly Guid Cajero = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DbContextOptions<BusinessDbContext> OpcionesEnMemoria() =>
        new DbContextOptionsBuilder<BusinessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task UnaFacturaGuardadaSeRecuperaConSusItemsYPagosYSuHistorial()
    {
        // Arrange
        var options = OpcionesEnMemoria();
        var factura = Invoice.Create();
        factura.Issue(new[] { InvoiceItem.Create("Combo", 500m, 1) }, isDelivery: false, Ahora);
        factura.EditItems(
            new[]
            {
                InvoiceItem.Create("Combo", 500m, 1),
                InvoiceItem.Create("Refresco", 75m, 1),
            },
            Ahora,
            Cajero,
            new NotifierNulo());
        factura.Collect(new[] { InvoicePayment.Cash(575m, 600m, Ahora, Cajero) });

        await using (var contexto = new BusinessDbContext(options))
        {
            var repositorio = new InvoiceRepository(contexto);
            await repositorio.AddAsync(factura);
            await repositorio.SaveChangesAsync();
        }

        // Act
        await using (var contexto = new BusinessDbContext(options))
        {
            var repositorio = new InvoiceRepository(contexto);
            var recuperada = await repositorio.GetByIdAsync(factura.Id);

            // Assert
            Assert.NotNull(recuperada);
            Assert.Equal(InvoiceState.Paid, recuperada!.State);
            Assert.Equal(2, recuperada.Items.Count);
            Assert.Equal(575m, recuperada.Total);
            var pago = Assert.Single(recuperada.Payments);
            Assert.Equal(575m, pago.Amount);
            Assert.Equal(600m, pago.ReceivedAmount);
            Assert.Equal(25m, pago.ChangeAmount);
            Assert.Single(recuperada.Edits);
            Assert.Equal(575m, recuperada.PaidAmount);
        }
    }

    // RF-NEG-02: agregar un pago a una factura que ya existe tiene que ser un
    // INSERT, no un UPDATE de una fila que no esta. La diferencia se ve contra
    // PostgreSQL (un UPDATE de cero filas lanza DbUpdateConcurrencyException);
    // InMemory no la ve, asi que aqui se comprueba el estado del cambio.
    [Fact]
    public async Task UnPagoNuevoDeUnaFacturaCargadaSeGuardaComoFilaNueva()
    {
        // Arrange: la factura se guarda primero y despues se carga en otro contexto.
        var options = OpcionesEnMemoria();
        var factura = Invoice.Create();
        factura.Issue(new[] { InvoiceItem.Create("Combo", 500m, 1) }, isDelivery: false, Ahora);

        await using (var contexto = new BusinessDbContext(options))
        {
            var repositorio = new InvoiceRepository(contexto);
            await repositorio.AddAsync(factura);
            await repositorio.SaveChangesAsync();
        }

        await using var segundo = new BusinessDbContext(options);
        var cargada = await new InvoiceRepository(segundo).GetByIdAsync(factura.Id);

        // Act
        cargada!.Collect(new[] { InvoicePayment.Cash(500m, 500m, Ahora, Cajero) });

        // Assert
        var estadoDelPago = segundo.ChangeTracker.Entries<InvoicePayment>().Single().State;
        Assert.Equal(EntityState.Added, estadoDelPago);
        Assert.Equal(EntityState.Modified, segundo.ChangeTracker.Entries<Invoice>().Single().State);
    }

    // Las claves las genera el dominio (Invoice.Create, InvoiceItem.Create...).
    // Si el modelo las declarara generadas por la base, EF tomaria una fila hija
    // nueva por una fila existente y la guardaria con UPDATE.
    [Theory]
    [InlineData(typeof(Invoice))]
    [InlineData(typeof(InvoiceItem))]
    [InlineData(typeof(InvoicePayment))]
    [InlineData(typeof(InvoiceEdit))]
    public void LasClavesLasGeneraElDominioYNoLaBase(Type tipoDeEntidad)
    {
        // Arrange: el modelo se construye sin abrir conexion a la base.
        var options = new DbContextOptionsBuilder<BusinessDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=pirry_ledger;Username=test;Password=test")
            .Options;

        using var contexto = new BusinessDbContext(options);

        // Act
        var clave = contexto.Model.FindEntityType(tipoDeEntidad)!.FindPrimaryKey()!.Properties.Single();

        // Assert
        Assert.Equal(ValueGenerated.Never, clave.ValueGenerated);
    }

    [Fact]
    public void ElEsquemaUsaNombresEnSnakeCase()
    {
        // Arrange: el modelo se construye sin abrir conexion a la base.
        var options = new DbContextOptionsBuilder<BusinessDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=pirry_ledger;Username=test;Password=test")
            .Options;

        using var contexto = new BusinessDbContext(options);
        var model = contexto.Model;

        // Assert
        Assert.Equal("bil_invoices", model.FindEntityType(typeof(Invoice))!.GetTableName());
        Assert.Equal("bil_invoice_items", model.FindEntityType(typeof(InvoiceItem))!.GetTableName());
        Assert.Equal("bil_invoice_payments", model.FindEntityType(typeof(InvoicePayment))!.GetTableName());
        Assert.Equal("bil_invoice_edits", model.FindEntityType(typeof(InvoiceEdit))!.GetTableName());

        var invoice = model.FindEntityType(typeof(Invoice))!;
        Assert.Equal("debtor_name", invoice.FindProperty(nameof(Invoice.DebtorName))!.GetColumnName());

        var item = model.FindEntityType(typeof(InvoiceItem))!;
        Assert.Equal("numeric(12,2)", item.FindProperty(nameof(InvoiceItem.UnitPrice))!.GetColumnType());

        var payment = model.FindEntityType(typeof(InvoicePayment))!;
        Assert.Equal("numeric(12,2)", payment.FindProperty(nameof(InvoicePayment.Amount))!.GetColumnType());
        Assert.True(payment.FindProperty(nameof(InvoicePayment.ReceivedAmount))!.IsNullable);
        Assert.Equal("registered_by_user_id", payment.FindProperty(nameof(InvoicePayment.RegisteredByUserId))!.GetColumnName());

        var edit = model.FindEntityType(typeof(InvoiceEdit))!;
        Assert.Equal("edited_by_user_id", edit.FindProperty(nameof(InvoiceEdit.EditedByUserId))!.GetColumnName());
    }

    private sealed class NotifierNulo : IInvoiceEditedNotifier
    {
        public void Notify(Invoice invoice, Guid editedByUserId, DateTime editedAtUtc)
        {
        }
    }
}
