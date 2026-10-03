using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class RecoveryCodeRepository : IRecoveryCodeRepository
{
    private readonly AccessControlDbContext _context;

    public RecoveryCodeRepository(AccessControlDbContext context) => _context = context;

    public Task<CodigoRecuperacion?> BuscarPorHashAsync(
        string hashDelCodigo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hashDelCodigo))
        {
            return Task.FromResult<CodigoRecuperacion?>(null);
        }

        var normalized = hashDelCodigo.Trim().ToLowerInvariant();
        return _context.CodigosDeRecuperacion
            .FirstOrDefaultAsync(codigo => codigo.HashDelCodigo == normalized, cancellationToken);
    }

    public async Task InvalidarPendientesAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        await _context.CodigosDeRecuperacion
            .Where(codigo => codigo.UsuarioId == usuarioId && codigo.UsadoUtc == null)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task GuardarAsync(CodigoRecuperacion codigo, CancellationToken cancellationToken = default)
    {
        if (_context.CodigosDeRecuperacion.Local.Any(existing => existing.Id == codigo.Id))
        {
            _context.CodigosDeRecuperacion.Update(codigo);
        }
        else
        {
            _context.CodigosDeRecuperacion.Add(codigo);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
