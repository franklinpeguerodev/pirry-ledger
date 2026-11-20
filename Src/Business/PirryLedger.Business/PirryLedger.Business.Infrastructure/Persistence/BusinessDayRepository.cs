using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.Persistence;

public sealed class BusinessDayRepository(BusinessDbContext context) : IBusinessDayRepository
{
    public async Task<T> InBranchTransactionAsync<T>(Guid branchId, Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            // Mismo espacio de claves para apertura, emisión, cierre y reapertura.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({branchId.ToString()}, 530026))", ct);
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public Task<BusinessDay?> LatestAsync(Guid branch, CancellationToken ct) => context.BusinessDays
        .Where(x => x.BranchId == branch).OrderByDescending(x => x.OpenedAtUtc).FirstOrDefaultAsync(ct);
    public Task<BusinessDay?> FindAsync(Guid id, Guid branch, CancellationToken ct) =>
        context.BusinessDays.SingleOrDefaultAsync(x => x.Id == id && x.BranchId == branch, ct);
    public Task<bool> HasLaterAsync(BusinessDay day, CancellationToken ct) => context.BusinessDays
        .AnyAsync(x => x.BranchId == day.BranchId && x.OpenedAtUtc > day.OpenedAtUtc, ct);
    public async Task AddAsync(BusinessDay day, CancellationToken ct) => await context.BusinessDays.AddAsync(day, ct);
    public async Task<IReadOnlyList<Invoice>> InvoicesAsync(Guid day, CancellationToken ct) =>
        await context.Invoices.Include(x => x.Items).Include(x => x.Payments)
            .Where(x => x.BusinessDayId == day).ToListAsync(ct);
    public async Task SaveAsync(CancellationToken ct) => await context.SaveChangesAsync(ct);
}
