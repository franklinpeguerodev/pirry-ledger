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

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Invoices
            .Include(invoice => invoice.Items)
            .Include(invoice => invoice.Payments)
            .Include(invoice => invoice.Edits)
            .FirstOrDefaultAsync(invoice => invoice.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Invoices
            .Include(invoice => invoice.Items)
            .Include(invoice => invoice.Payments)
            .Include(invoice => invoice.Edits)
            .ToListAsync(cancellationToken);
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
