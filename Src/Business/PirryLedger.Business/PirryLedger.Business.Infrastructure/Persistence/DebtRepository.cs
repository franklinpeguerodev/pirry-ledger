using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class DebtRepository : IDebtRepository
{
    private readonly BusinessDbContext _context;

    public DebtRepository(BusinessDbContext context)
    {
        _context = context;
    }

    private IQueryable<Invoice> Query(Guid? branchId) => _context.Invoices
        .Include(invoice => invoice.Items)
        .Include(invoice => invoice.Payments)
        .Where(invoice => invoice.DebtorName != null
            && (invoice.State == InvoiceState.InDebt || invoice.State == InvoiceState.Paid)
            && (branchId == null || invoice.BranchId == branchId));

    public async Task<IReadOnlyList<Invoice>> ListAsync(Guid? branchId, CancellationToken cancellationToken) =>
        await Query(branchId).AsNoTracking().OrderBy(invoice => invoice.IssuedAtUtc)
            .ThenBy(invoice => invoice.Id).ToListAsync(cancellationToken);

    public Task<Invoice?> FindAsync(Guid invoiceId, Guid? branchId, CancellationToken cancellationToken) =>
        Query(branchId).SingleOrDefaultAsync(invoice => invoice.Id == invoiceId, cancellationToken);

    public async Task<T> InInvoiceTransactionAsync<T>(Guid invoiceId, Guid? branchId,
        Func<Invoice, Task<T>> action, CancellationToken cancellationToken)
    {
        if (_context.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("El abono debe iniciar sin cambios pendientes de otra operación.");
        }

        // Leer estado actual bajo bloqueo, incluso si el contexto se reutilizó.
        _context.ChangeTracker.Clear();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var global = branchId is null;
            var branch = branchId ?? Guid.Empty;
            var invoice = await _context.Invoices.FromSqlInterpolated($"""
                SELECT * FROM bil_invoices
                WHERE id = {invoiceId} AND ({global} OR branch_id = {branch})
                  AND debtor_name IS NOT NULL
                  AND state IN ({(int)InvoiceState.InDebt}, {(int)InvoiceState.Paid})
                FOR UPDATE
                """)
                .Include(invoice => invoice.Items).Include(invoice => invoice.Payments)
                .AsSplitQuery().SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvoiceNotFoundException();

            var result = await action(invoice);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);
}
