using PirryLedger.Core.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class DebtRepository : IDebtRepository
{
    private readonly BusinessDbContext _context;
    private readonly IApplicationTransaction? _coordinator;

    public DebtRepository(BusinessDbContext context, IApplicationTransaction? coordinator = null)
    {
        _context = context;
        _coordinator = coordinator;
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

    public Task<T> InInvoiceTransactionAsync<T>(Guid invoiceId, Guid? branchId,
        Func<Invoice, Task<T>> action, CancellationToken cancellationToken) =>
        _coordinator is null ? ExecuteLockedAsync(invoiceId, branchId, action, cancellationToken)
            : _coordinator.ExecuteAsync(() => ExecuteLockedAsync(invoiceId, branchId, action, cancellationToken), cancellationToken);

    private async Task<T> ExecuteLockedAsync<T>(Guid invoiceId, Guid? branchId,
        Func<Invoice, Task<T>> action, CancellationToken cancellationToken)
    {
        if (_context.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("El abono debe iniciar sin cambios pendientes de otra operación.");
        }

        // Leer estado actual bajo bloqueo, incluso si el contexto se reutilizó.
        _context.ChangeTracker.Clear();
        await using var transaction = _context.Database.CurrentTransaction is null ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
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
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);
}
