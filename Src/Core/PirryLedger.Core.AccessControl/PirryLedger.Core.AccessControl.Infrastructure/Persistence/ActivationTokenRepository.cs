using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class ActivationTokenRepository : IActivationTokenRepository
{
    private readonly AccessControlDbContext _context;

    public ActivationTokenRepository(AccessControlDbContext context)
    {
        _context = context;
    }

    // Devuelve el token tanto si esta usado como si caduco. El caso de uso decide
    // que rechazar, y todos esos rechazos son iguales para quien llama
    // (RF-CA-16): filtrar aqui por vigencia dejaria ver si un enlace existio.
    public async Task<ActivationToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        var normalized = tokenHash.Trim().ToLowerInvariant();

        return await _context.ActivationTokens
            .FirstOrDefaultAsync(token => token.TokenHash == normalized, cancellationToken);
    }

    public async Task InvalidatePendingAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Solo los no usados. Un token ya abierto se conserva: es el registro de que
        // la cuenta quedo activa, y ademas ese Email ya no sirve para nada.
        await _context.ActivationTokens
            .Where(token => token.UserId == userId && token.UsedAtUtc == null)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SaveAsync(ActivationToken token, CancellationToken cancellationToken = default)
    {
        if (_context.ActivationTokens.Local.Any(registered => registered.Id == token.Id))
        {
            _context.ActivationTokens.Update(token);
        }
        else
        {
            _context.ActivationTokens.Add(token);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
