using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.Contracts.Reports;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDeReportesDelNegocio
{
    [Fact]
    public async Task VentasUsanJornadaCerradaExcluyenEstadosYNoSumanAbonos()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();

        var rows = await fixture.Source.SalesAsync(fixture.Filter, SalesReportGrouping.Day, default);

        Assert.Equal(560m, rows.Sum(row => row.Sold));
        Assert.Equal(5, rows.Sum(row => row.InvoiceCount));
        Assert.All(rows, row => Assert.Equal(new DateOnly(2026, 11, 24), row.Period));
        Assert.Equal(new DateTime(2026, 11, 25).Date, fixture.Combo.IssuedAtUtc.Date);
        Assert.Equal(300m, rows.Sum(row => row.Methods!.Cash));
        Assert.Equal(100m, rows.Sum(row => row.Methods!.PendingTransfers));
        Assert.Equal(160m, rows.Sum(row => row.Methods!.Debt));
        Assert.All(fixture.Context.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
    }

    [Fact]
    public async Task SeleccionDeProductosSumaSoloLineasYCuentaFacturaUnaVez()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();
        var filter = fixture.Filter with { ProductNames = ["A", "B"] };

        var sales = await fixture.Source.SalesAsync(filter, SalesReportGrouping.Day, default);
        var products = await fixture.Source.ProductsAsync(filter, default);

        Assert.Equal(310m, sales.Sum(row => row.Sold));
        Assert.Equal(3, sales.Sum(row => row.InvoiceCount));
        Assert.All(sales, row => Assert.Null(row.Methods));
        Assert.Equal(3, products.InvoiceCounts.Sum(row => row.InvoiceCount));
        Assert.Equal(4, products.Rows.Sum(row => row.InvoiceCount));
        Assert.Equal(310m, products.Rows.Sum(row => row.Amount));
    }

    [Fact]
    public async Task CobrosSoloIncluyenFacturacionYSeparanTransferencias()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();

        var rows = await fixture.Source.MethodsAsync(fixture.Filter, default);

        Assert.Equal(300m, rows.Sum(row => row.Methods.Cash));
        Assert.Equal(0m, rows.Sum(row => row.Methods.ConfirmedTransfers));
        Assert.Equal(100m, rows.Sum(row => row.Methods.PendingTransfers));
        Assert.Equal(160m, rows.Sum(row => row.Methods.Debt));
    }

    [Fact]
    public async Task ExclusionesConservanCantidadImporteYDenominadorDeFacturas()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();

        var rows = await fixture.Source.ExclusionsAsync(fixture.Filter, default);

        Assert.Equal(8, rows.Sum(row => row.IssuedCount));
        Assert.Equal(1, rows.Sum(row => row.PendingCancellation.Count));
        Assert.Equal(60m, rows.Sum(row => row.PendingCancellation.Amount));
        Assert.Equal(70m, rows.Sum(row => row.Cancelled.Amount));
        Assert.Equal(50m, rows.Sum(row => row.NotDelivered.Amount));
    }

    [Fact]
    public async Task EmpleadoSeAgrupaPorIdentificadorYNombreEmitido()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();

        var rows = await fixture.Source.EmployeesAsync(fixture.Filter, default);

        Assert.Equal(2, rows.Count(row => row.UserId == fixture.Ana));
        Assert.Contains(rows, row => row.UserId == fixture.Ana && row.HistoricalName == "Ana nueva" && row.Sold == 150m);
        Assert.Contains(rows, row => row.UserId == fixture.Ana && row.HistoricalName == "Ana" && row.Sold == 260m);
        Assert.Equal(560m, rows.Sum(row => row.Sold));
        Assert.Equal(8, rows.Sum(row => row.IssuedCount));
    }

    [Fact]
    public async Task DeudasPendientesYSaldadasTienenConsultasYFechasSeparadas()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();

        var pending = await fixture.Source.DebtsAsync(fixture.Filter, false, new(), default);
        var settled = await fixture.Source.DebtsAsync(fixture.Filter, true, new(), default);

        Assert.Equal(fixture.Debt.Id, Assert.Single(pending.Items).InvoiceId);
        Assert.Equal(100m, pending.Outstanding);
        Assert.True(pending.Items[0].HasPendingTransfer);
        Assert.Equal(fixture.Settled.Id, Assert.Single(settled.Items).InvoiceId);
        Assert.Equal(SoporteDeReportes.Ahora, settled.Items[0].PaidAtUtc);
        Assert.Equal(new DateOnly(2026, 11, 24), settled.Items[0].BusinessDate);
        Assert.Equal(0m, settled.Outstanding);
    }

    [Fact]
    public async Task CierreReabiertoSeMuestraSinSumarACerrados()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();

        var result = await fixture.Source.ClosingsAsync(fixture.Filter, new(0, 1), default);

        Assert.Single(result.Items);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.ClosedCount);
        Assert.Equal(1, result.ReopenedCount);
        Assert.Equal(560m, result.ClosedTotals.Sold);
        var all = await fixture.Source.ClosingsAsync(fixture.Filter, new(), default);
        Assert.Contains(all.Items, row => row.BusinessDayId == fixture.Reopened.Id && row.IsReopened && row.Closing.Sold == 50m);
    }

    [Fact]
    public async Task JornadasAbiertasSoloMuestranVentaActualPorSucursal()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();

        var result = await fixture.Source.OpenSalesAsync([fixture.A, fixture.B], default);

        Assert.Equal(2, result.Count);
        Assert.Equal(150m, result.Sum(row => row.Sold));
        Assert.Equal(70m, result.Single(row => row.BranchId == fixture.B).Sold);
    }

    [Fact]
    public async Task SemanaYMesAgrupanDespuesDeFiltrarDias()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();
        var filter = fixture.Filter with { BranchIds = [fixture.A] };

        var weekly = await fixture.Source.SalesAsync(filter, SalesReportGrouping.Week, default);
        var monthly = await fixture.Source.SalesAsync(filter, SalesReportGrouping.Month, default);

        Assert.Equal(new DateOnly(2026, 11, 23), Assert.Single(weekly).Period);
        Assert.Equal(new DateOnly(2026, 11, 1), Assert.Single(monthly).Period);
        Assert.Equal(410m, weekly[0].Sold);
        Assert.Equal(410m, monthly[0].Sold);
    }
    [Fact]
    public async Task NombreDeProductoSeFiltraExactamenteSinCambiarMayusculas()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();

        var selected = await fixture.Source.SalesAsync(fixture.Filter with { ProductNames = ["A"] }, SalesReportGrouping.Day, default);
        var different = await fixture.Source.SalesAsync(fixture.Filter with { ProductNames = ["a"] }, SalesReportGrouping.Day, default);

        Assert.Equal(260m, selected.Sum(row => row.Sold));
        Assert.Equal(0m, different.Sum(row => row.Sold));
    }

    [Fact]
    public async Task RecuperacionActualizaVentaOriginalSinReescribirCierreGuardado()
    {
        await using var fixture = await SoporteDeReportes.CrearAsync();
        fixture.Undelivered.RecoverUndelivered([PirryLedger.Business.Domain.Entities.InvoicePayment.Cash(
            50m, null, SoporteDeReportes.Ahora, fixture.Ana)]);
        await fixture.Context.SaveChangesAsync();

        var sales = await fixture.Source.SalesAsync(fixture.Filter, SalesReportGrouping.Day, default);
        var closings = await fixture.Source.ClosingsAsync(fixture.Filter, new(), default);

        Assert.Equal(610m, sales.Sum(row => row.Sold));
        Assert.Equal(fixture.ClosedA.Id, fixture.Undelivered.BusinessDayId);
        Assert.Equal(560m, closings.ClosedTotals.Sold);
    }
}
