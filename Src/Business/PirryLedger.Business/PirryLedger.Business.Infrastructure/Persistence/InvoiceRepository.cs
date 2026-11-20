using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly BusinessDbContext _context;
    private readonly IBranchScopeAccessor _scope;

    public InvoiceRepository(BusinessDbContext context)
        : this(context, new UnscopedBranchAccessor())
    {
    }

    public InvoiceRepository(BusinessDbContext context, IBranchScopeAccessor scope)
    {
        _context = context;
        _scope = scope;
    }

    private sealed class UnscopedBranchAccessor : IBranchScopeAccessor
    {
        public Guid? BranchId { get; set; }

        public Guid? UserId { get; set; }

        public bool IsGlobal { get; set; } = true;
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Invoices
            .Include(invoice => invoice.Items)
            .Include(invoice => invoice.Payments)
            .Include(invoice => invoice.Edits)
            .Where(invoice => invoice.Id == id);

        query = ApplyScope(query, branchId);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> ListAsync(Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Invoices
            .Include(invoice => invoice.Items)
            .Include(invoice => invoice.Payments)
            .Include(invoice => invoice.Edits)
            .AsQueryable();

        query = ApplyScope(query, branchId);

        return await query.ToListAsync(cancellationToken);
    }

    private IQueryable<Invoice> ApplyScope(IQueryable<Invoice> query, Guid? branchId)
    {
        if (!_scope.IsGlobal)
        {
            if (_scope.BranchId is null || _scope.UserId is null)
                return query.Where(invoice => false);

            var ownBranch = _scope.BranchId.Value;
            var issuer = _scope.UserId.Value;
            query = query.Where(invoice => invoice.BranchId == ownBranch
                && invoice.IssuedByUserId == issuer);
        }

        return branchId is null ? query : query.Where(invoice => invoice.BranchId == branchId);
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
