using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class SessionRepository : ISessionRepository
{
    private readonly AccessControlDbContext _context;

    public SessionRepository(AccessControlDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(Session session, CancellationToken cancellationToken = default)
    {
        if (_context.Sessions.Local.Any(registered => registered.Id == session.Id))
        {
            _context.Sessions.Update(session);
        }
        else
        {
            _context.Sessions.Add(session);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Devuelve la session aunque este cerrada o caducada. El punto de validacion
    // decide, y filtrar aqui permitiria distinguir "caducada" de "no existe".
    public async Task<Session?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        var normalized = tokenHash.Trim().ToLowerInvariant();

        return await _context.Sessions
            .FirstOrDefaultAsync(session => session.TokenHash == normalized, cancellationToken);
    }

    // Cierra solo esa session. Si el token no existe o ya estaba cerrada, devuelve
    // false y el endpoint responde 204 igual: no se distingue un caso del otro.
    public async Task<bool> CloseByHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var hash = tokenHash.Trim().ToLowerInvariant();

        var session = await _context.Sessions
            .FirstOrDefaultAsync(registered => registered.TokenHash == hash, cancellationToken);

        if (session is null || session.IsClosed)
        {
            return false;
        }

        session.Close(utcNow);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
