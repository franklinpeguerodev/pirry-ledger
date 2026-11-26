using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class OverdueBillingRepository(BusinessDbContext context) : IOverdueBillingRepository
{
    private IQueryable<OverdueTransfer> Transfers(Guid? id = null, DateTime? beforeUtc = null) =>
        from payment in context.InvoicePayments.AsNoTracking()
        join invoice in context.Invoices.AsNoTracking() on payment.InvoiceId equals invoice.Id
        where (!id.HasValue || payment.Id == id.Value)
            && (!beforeUtc.HasValue || (payment.Method == PaymentMethod.Transfer && payment.Status == PaymentStatus.Registered
                && invoice.State != InvoiceState.Cancelled && payment.RegisteredAtUtc < beforeUtc.Value))
        select new OverdueTransfer(payment.Id, invoice.Id, invoice.BranchId, invoice.BranchName,
            payment.Amount, payment.Method, payment.Status, invoice.State, payment.RegisteredAtUtc);

    public async Task<IReadOnlyList<OverdueTransfer>> FindPendingTransfersAsync(DateTime beforeUtc, CancellationToken cancellationToken) =>
        await Transfers(beforeUtc: beforeUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OverdueBusinessDay>> FindOpenDaysAsync(DateOnly beforeDate, CancellationToken cancellationToken)
    {
        var days = await context.BusinessDays.AsNoTracking().Where(day => day.IsOpen && day.BusinessDate < beforeDate).ToListAsync(cancellationToken);
        var result = new List<OverdueBusinessDay>();
        foreach (var day in days) result.Add(new(day, await BranchNameAsync(day, cancellationToken)));
        return result;
    }

    public async Task<OverdueTransfer?> FindTransferAsync(Guid id, bool lockForProcessing, CancellationToken cancellationToken)
    {
        if (lockForProcessing)
        {
            RequireTransaction();
            // Mismo orden que abonos: factura primero, pago después.
            await context.Invoices.FromSqlInterpolated($"""
                SELECT * FROM bil_invoices WHERE id = (SELECT invoice_id FROM bil_invoice_payments WHERE id = {id}) FOR UPDATE
                """).AsNoTracking().ToListAsync(cancellationToken);
            await context.InvoicePayments.FromSqlInterpolated($"""
                SELECT * FROM bil_invoice_payments WHERE id = {id} FOR UPDATE
                """).AsNoTracking().ToListAsync(cancellationToken);
        }
        return await Transfers(id: id).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OverdueBusinessDay?> FindDayAsync(Guid id, bool lockForProcessing, CancellationToken cancellationToken)
    {
        var day = await context.BusinessDays.AsNoTracking().SingleOrDefaultAsync(day => day.Id == id, cancellationToken);
        if (day is null) return null;
        if (lockForProcessing)
        {
            RequireTransaction();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({day.BranchId.ToString()}, 530026))", cancellationToken);
            day = await context.BusinessDays.FromSqlInterpolated($"""
                SELECT * FROM bil_business_days WHERE id = {id} FOR UPDATE
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (day is null) return null;
        }
        return new(day, await BranchNameAsync(day, cancellationToken));
    }

    private async Task<string> BranchNameAsync(BusinessDay day, CancellationToken cancellationToken) =>
        await context.Invoices.AsNoTracking().Where(invoice => invoice.BusinessDayId == day.Id)
            .OrderBy(invoice => invoice.IssuedAtUtc).Select(invoice => invoice.BranchName)
            .FirstOrDefaultAsync(cancellationToken) ?? day.BranchId.ToString();

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La revalidación del atraso requiere la transacción de publicación.");
    }
}
