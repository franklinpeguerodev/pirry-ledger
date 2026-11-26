using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;
using PirryLedger.Core.Contracts.Reports;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class BusinessReportRepository(BusinessDbContext context) : IBusinessReportRepository
{
    private IQueryable<Invoice> Invoices(BusinessReportFilter filter, bool open = false)
    {
        var days = context.BusinessDays.Where(day => day.IsOpen == open
            && filter.BranchIds.Contains(day.BranchId)
            && day.BusinessDate >= filter.From && day.BusinessDate <= filter.To).Select(day => day.Id);
        return context.Invoices.AsNoTracking().Where(invoice => filter.BranchIds.Contains(invoice.BranchId) && invoice.BusinessDayId != null
            && days.Contains(invoice.BusinessDayId.Value)
            && (filter.ProductNames.Count == 0 || invoice.Items.Any(item => filter.ProductNames.Contains(item.Name))));
    }

    public async Task<IReadOnlyList<Guid>> InvoiceIdsAsync(BusinessReportFilter filter, bool open, CancellationToken ct) =>
        await Invoices(filter, open).Select(invoice => invoice.Id).ToArrayAsync(ct);

    private IQueryable<InvoiceValue> Values(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, bool open = false)
    {
        var states = InvoiceSalesCalculation.IncludedStates.ToArray();
        var names = filter.ProductNames.ToArray();
        var pending = excluded.ToArray();
        return from invoice in Invoices(filter, open)
               join day in context.BusinessDays on invoice.BusinessDayId equals (Guid?)day.Id
               select new InvoiceValue
               {
                   BranchId = invoice.BranchId, BusinessDayId = day.Id, Date = day.BusinessDate,
                   UserId = invoice.IssuedByUserId, Name = invoice.IssuedByUserName,
                   Included = !pending.Contains(invoice.Id) && states.Contains(invoice.State),
                   Pending = pending.Contains(invoice.Id),
                   Cancelled = !pending.Contains(invoice.Id) && invoice.State == InvoiceState.Cancelled,
                   NotDelivered = !pending.Contains(invoice.Id) && invoice.State == InvoiceState.NotDelivered,
                   Amount = invoice.Items.Where(item => names.Length == 0 || names.Contains(item.Name))
                       .Sum(item => item.UnitPrice * item.Quantity),
                   Cash = names.Length == 0 ? invoice.Payments.Where(payment => payment.Kind == PaymentKind.Sale
                       && payment.Method == PaymentMethod.Cash && payment.Status == PaymentStatus.Confirmed).Sum(payment => payment.Amount) : 0m,
                   Confirmed = names.Length == 0 ? invoice.Payments.Where(payment => payment.Kind == PaymentKind.Sale
                       && payment.Method == PaymentMethod.Transfer && payment.Status == PaymentStatus.Confirmed).Sum(payment => payment.Amount) : 0m,
                   Unverified = names.Length == 0 ? invoice.Payments.Where(payment => payment.Kind == PaymentKind.Sale
                       && payment.Method == PaymentMethod.Transfer && payment.Status == PaymentStatus.Registered).Sum(payment => payment.Amount) : 0m,
                   Debt = names.Length == 0 && invoice.DebtorName != null
                       ? invoice.Items.Sum(item => item.UnitPrice * item.Quantity) - invoice.Payments
                           .Where(payment => payment.Kind == PaymentKind.Sale && payment.Status != PaymentStatus.Rejected).Sum(payment => payment.Amount)
                       : 0m
               };
    }

    public async Task<IReadOnlyList<SalesReportRow>> DailySalesAsync(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, CancellationToken ct)
    {
        var rows = await Values(filter, excluded).GroupBy(row => new { row.BranchId, row.Date })
            .Select(group => new
            {
                group.Key.BranchId, group.Key.Date, Count = group.Count(row => row.Included),
                Sold = group.Sum(row => row.Included ? row.Amount : 0m),
                Cash = group.Sum(row => row.Included ? row.Cash : 0m),
                Confirmed = group.Sum(row => row.Included ? row.Confirmed : 0m),
                Pending = group.Sum(row => row.Included ? row.Unverified : 0m),
                Debt = group.Sum(row => row.Included ? row.Debt : 0m)
            }).ToArrayAsync(ct);
        return rows.Select(row => new SalesReportRow(row.BranchId, row.Date, row.Count, row.Sold,
            filter.ProductNames.Count == 0 ? new(row.Cash, row.Confirmed, row.Pending, row.Debt) : null)).ToArray();
    }

    public async Task<IReadOnlyList<MethodReportRow>> MethodsAsync(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, CancellationToken ct) =>
        await Values(filter, excluded).GroupBy(row => row.BranchId).Select(group => new MethodReportRow(group.Key,
            new SaleMethods(group.Sum(row => row.Included ? row.Cash : 0m),
                group.Sum(row => row.Included ? row.Confirmed : 0m),
                group.Sum(row => row.Included ? row.Unverified : 0m),
                group.Sum(row => row.Included ? row.Debt : 0m)))).ToArrayAsync(ct);

    public async Task<ProductReportData> ProductsAsync(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, CancellationToken ct)
    {
        var states = InvoiceSalesCalculation.IncludedStates.ToArray();
        var pending = excluded.ToArray();
        var names = filter.ProductNames.ToArray();
        var invoices = Invoices(filter).Where(invoice => !pending.Contains(invoice.Id) && states.Contains(invoice.State));
        var rows = await invoices.SelectMany(invoice => invoice.Items
                .Where(item => names.Length == 0 || names.Contains(item.Name))
                .Select(item => new { invoice.BranchId, InvoiceId = invoice.Id, item.Name, item.Quantity, item.UnitPrice }))
            .GroupBy(item => new { item.BranchId, item.Name })
            .Select(group => new ProductReportRow(group.Key.BranchId, group.Key.Name,
                group.Sum(item => (long)item.Quantity), group.Sum(item => item.UnitPrice * item.Quantity),
                group.Select(item => item.InvoiceId).Distinct().Count())).ToArrayAsync(ct);
        var counts = await invoices.GroupBy(invoice => invoice.BranchId)
            .Select(group => new BranchInvoiceCount(group.Key, group.Count())).ToArrayAsync(ct);
        return new(rows, counts);
    }

    public async Task<IReadOnlyList<ExclusionReportRow>> ExclusionsAsync(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, CancellationToken ct) =>
        await Values(filter, excluded).GroupBy(row => row.BranchId)
            .Select(group => new ExclusionReportRow(group.Key, group.Count(),
                new CountAmount(group.Count(row => row.Pending), group.Sum(row => row.Pending ? row.Amount : 0m)),
                new CountAmount(group.Count(row => row.Cancelled), group.Sum(row => row.Cancelled ? row.Amount : 0m)),
                new CountAmount(group.Count(row => row.NotDelivered), group.Sum(row => row.NotDelivered ? row.Amount : 0m))))
            .ToArrayAsync(ct);

    public async Task<IReadOnlyList<EmployeeReportRow>> EmployeesAsync(BusinessReportFilter filter, IReadOnlyList<Guid> excluded, CancellationToken ct)
    {
        var rows = await Values(filter, excluded).GroupBy(row => new { row.BranchId, row.UserId, row.Name })
            .Select(group => new
            {
                group.Key.BranchId, group.Key.UserId, group.Key.Name, Issued = group.Count(),
                SoldCount = group.Count(row => row.Included), Sold = group.Sum(row => row.Included ? row.Amount : 0m),
                Cash = group.Sum(row => row.Included ? row.Cash : 0m), Confirmed = group.Sum(row => row.Included ? row.Confirmed : 0m),
                Pending = group.Sum(row => row.Included ? row.Unverified : 0m), Debt = group.Sum(row => row.Included ? row.Debt : 0m),
                PendingCount = group.Count(row => row.Pending), PendingAmount = group.Sum(row => row.Pending ? row.Amount : 0m),
                CancelledCount = group.Count(row => row.Cancelled), CancelledAmount = group.Sum(row => row.Cancelled ? row.Amount : 0m),
                NotDeliveredCount = group.Count(row => row.NotDelivered), NotDeliveredAmount = group.Sum(row => row.NotDelivered ? row.Amount : 0m)
            }).ToArrayAsync(ct);
        return rows.Select(row => new EmployeeReportRow(row.BranchId, row.UserId, row.Name, row.Issued, row.SoldCount, row.Sold,
            filter.ProductNames.Count == 0 ? new(row.Cash, row.Confirmed, row.Pending, row.Debt) : null,
            new(row.PendingCount, row.PendingAmount), new(row.CancelledCount, row.CancelledAmount),
            new(row.NotDeliveredCount, row.NotDeliveredAmount))).ToArray();
    }

    public async Task<DebtReportPage> DebtsAsync(BusinessReportFilter filter, bool settled, ReportPage page, CancellationToken ct)
    {
        var invoices = Invoices(filter).Where(invoice => invoice.DebtorName != null
            && invoice.State == (settled ? InvoiceState.Paid : InvoiceState.InDebt));
        var values = from invoice in invoices
                     join day in context.BusinessDays on invoice.BusinessDayId equals (Guid?)day.Id
                     select new
                     {
                         invoice.Id, invoice.BranchId, invoice.BranchName, day.BusinessDate, invoice.IssuedAtUtc, invoice.DebtorName,
                         Total = invoice.Items.Sum(item => item.UnitPrice * item.Quantity),
                         Outstanding = invoice.Items.Sum(item => item.UnitPrice * item.Quantity)
                             - invoice.Payments.Where(payment => payment.Status == PaymentStatus.Confirmed).Sum(payment => payment.Amount),
                         Pending = invoice.Payments.Any(payment => payment.Method == PaymentMethod.Transfer && payment.Status == PaymentStatus.Registered),
                         PaidAt = settled ? invoice.Payments.Where(payment => payment.Status == PaymentStatus.Confirmed)
                             .Max(payment => (DateTime?)payment.RegisteredAtUtc) : null
                     };
        var totals = await values.GroupBy(row => row.BranchId).Select(group =>
            new DebtBranchTotals(group.Key, group.Count(), group.Sum(row => row.Total), group.Sum(row => row.Outstanding)))
            .ToArrayAsync(ct);
        var rows = await values.OrderBy(row => row.IssuedAtUtc).ThenBy(row => row.Id).Skip(page.Offset).Take(page.Limit)
            .Select(row => new DebtReportRow(row.Id, row.BranchId, row.BranchName, row.BusinessDate,
                row.IssuedAtUtc, row.DebtorName!, row.Total, row.Outstanding, row.Pending, row.PaidAt)).ToArrayAsync(ct);
        return new(rows, totals.Sum(row => row.Count), totals.Sum(row => row.TotalInvoiced),
            totals.Sum(row => row.Outstanding), page.Offset, page.Limit, totals);
    }

    public async Task<ClosingReportPage> ClosingsAsync(BusinessReportFilter filter, ReportPage page, CancellationToken ct)
    {
        var days = context.BusinessDays.AsNoTracking().Where(day => filter.BranchIds.Contains(day.BranchId)
            && day.BusinessDate >= filter.From && day.BusinessDate <= filter.To
            && day.Closing != null && day.LastClosedAtUtc != null);
        var summary = await days.GroupBy(day => day.BranchId).Select(group => new
        {
            BranchId = group.Key, Count = group.Count(), Closed = group.Count(day => !day.IsOpen),
            Sold = group.Sum(day => day.IsOpen ? 0m : day.Closing!.Sold),
            Cash = group.Sum(day => day.IsOpen ? 0m : day.Closing!.Cash),
            Confirmed = group.Sum(day => day.IsOpen ? 0m : day.Closing!.ConfirmedTransfers),
            Pending = group.Sum(day => day.IsOpen ? 0m : day.Closing!.PendingTransfers),
            Debt = group.Sum(day => day.IsOpen ? 0m : day.Closing!.Debt),
            PendingCancellation = group.Sum(day => day.IsOpen ? 0m : day.Closing!.PendingCancellation),
            Cancelled = group.Sum(day => day.IsOpen ? 0m : day.Closing!.Cancelled),
            NotDelivered = group.Sum(day => day.IsOpen ? 0m : day.Closing!.NotDelivered)
        }).ToArrayAsync(ct);
        var rows = await days.OrderBy(day => day.BusinessDate).ThenBy(day => day.Id).Skip(page.Offset).Take(page.Limit)
            .Select(day => new ClosingReportRow(day.Id, day.BranchId, day.BusinessDate, day.LastClosedAtUtc!.Value,
                day.LastClosedByUserId!.Value, day.IsOpen, new ClosingAmounts(day.Closing!.Sold, day.Closing.Cash,
                    day.Closing.ConfirmedTransfers, day.Closing.PendingTransfers, day.Closing.Debt,
                    day.Closing.PendingCancellation, day.Closing.Cancelled, day.Closing.NotDelivered))).ToArrayAsync(ct);
        var branchTotals = summary.Select(row => new ClosingBranchTotals(row.BranchId, row.Count, row.Closed,
            row.Count - row.Closed, new(row.Sold, row.Cash, row.Confirmed, row.Pending, row.Debt,
                row.PendingCancellation, row.Cancelled, row.NotDelivered))).ToArray();
        var totals = new ClosingAmounts(summary.Sum(row => row.Sold), summary.Sum(row => row.Cash),
            summary.Sum(row => row.Confirmed), summary.Sum(row => row.Pending), summary.Sum(row => row.Debt),
            summary.Sum(row => row.PendingCancellation), summary.Sum(row => row.Cancelled), summary.Sum(row => row.NotDelivered));
        return new(rows, summary.Sum(row => row.Count), summary.Sum(row => row.Closed),
            summary.Sum(row => row.Count - row.Closed), totals, page.Offset, page.Limit, branchTotals);
    }

    public async Task<IReadOnlyList<OpenSalesReportRow>> OpenSalesAsync(IReadOnlyList<Guid> branchIds, IReadOnlyList<Guid> excluded, CancellationToken ct)
    {
        var filter = new BusinessReportFilter(DateOnly.MinValue, DateOnly.MaxValue, branchIds, []);
        return await Values(filter, excluded, true).GroupBy(row => new { row.BusinessDayId, row.BranchId, row.Date })
            .Select(group => new OpenSalesReportRow(group.Key.BusinessDayId, group.Key.BranchId, group.Key.Date,
                group.Sum(row => row.Included ? row.Amount : 0m))).ToArrayAsync(ct);
    }

    private sealed class InvoiceValue
    {
        public Guid BranchId { get; init; }
        public Guid BusinessDayId { get; init; }
        public DateOnly Date { get; init; }
        public Guid UserId { get; init; }
        public string Name { get; init; } = "";
        public bool Included { get; init; }
        public bool Pending { get; init; }
        public bool Cancelled { get; init; }
        public bool NotDelivered { get; init; }
        public decimal Amount { get; init; }
        public decimal Cash { get; init; }
        public decimal Confirmed { get; init; }
        public decimal Unverified { get; init; }
        public decimal Debt { get; init; }
    }
}
