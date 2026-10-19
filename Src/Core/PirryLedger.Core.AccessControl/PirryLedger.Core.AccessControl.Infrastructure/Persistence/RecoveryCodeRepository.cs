using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class RecoveryCodeRepository : IRecoveryCodeRepository
{
    private readonly AccessControlDbContext _context;

    public RecoveryCodeRepository(AccessControlDbContext context) => _context = context;

    public Task<RecoveryCode?> FindByHashAsync(
        string codeHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
        {
            return Task.FromResult<RecoveryCode?>(null);
        }

        var normalized = codeHash.Trim().ToLowerInvariant();
        return _context.RecoveryCodes
            .FirstOrDefaultAsync(code => code.CodeHash == normalized, cancellationToken);
    }

    public async Task InvalidatePendingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _context.RecoveryCodes
            .Where(code => code.UserId == userId && code.UsedAtUtc == null)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SaveAsync(RecoveryCode code, CancellationToken cancellationToken = default)
    {
        if (_context.RecoveryCodes.Local.Any(existing => existing.Id == code.Id))
        {
            _context.RecoveryCodes.Update(code);
        }
        else
        {
            _context.RecoveryCodes.Add(code);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
