using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Infrastructure.Persistence;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly BusinessDbContext _context;

    public InvoiceRepository(BusinessDbContext context)
    {
        _context = context;
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Invoices
            .Include(invoice => invoice.Items)
            .Include(invoice => invoice.Payments)
            .Include(invoice => invoice.Edits)
            .Where(invoice => invoice.Id == id);

        if (branchId is not null)
        {
            query = query.Where(invoice => invoice.BranchId == branchId);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> ListAsync(Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Invoices
            .Include(invoice => invoice.Items)
            .Include(invoice => invoice.Payments)
            .Include(invoice => invoice.Edits)
            .AsQueryable();

        if (branchId is not null)
        {
            query = query.Where(invoice => invoice.BranchId == branchId);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        await _context.Invoices.AddAsync(invoice, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
