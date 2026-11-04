using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class BranchRepository : IBranchRepository
{
    private readonly AccessControlDbContext _context;

    public BranchRepository(AccessControlDbContext context)
    {
        _context = context;
    }

    public Task<Branch?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Branches.FirstOrDefaultAsync(branch => branch.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Branch>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.Branches.OrderBy(branch => branch.Name).ToListAsync(cancellationToken);

    public async Task SaveAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        if (_context.Branches.Local.Any(existing => existing.Id == branch.Id))
        {
            _context.Branches.Update(branch);
        }
        else
        {
            await _context.Branches.AddAsync(branch, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
