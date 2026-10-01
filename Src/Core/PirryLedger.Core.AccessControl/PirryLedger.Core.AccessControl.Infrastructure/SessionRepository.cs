using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class SessionRepository : ISessionRepository
{
    private readonly AccessControlDbContext _contexto;

    public SessionRepository(AccessControlDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task GuardarAsync(Sesion sesion, CancellationToken cancellationToken = default)
    {
        if (_contexto.Sesiones.Local.Any(registrada => registrada.Id == sesion.Id))
        {
            _contexto.Sesiones.Update(sesion);
        }
        else
        {
            _contexto.Sesiones.Add(sesion);
        }

        await _contexto.SaveChangesAsync(cancellationToken);
    }

    // Devuelve la sesion aunque este cerrada o caducada. El punto de validacion
    // decide, y filtrar aqui permitiria distinguir "caducada" de "no existe".
    public async Task<Sesion?> BuscarPorHashAsync(string hashDelToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hashDelToken))
        {
            return null;
        }

        var normalizado = hashDelToken.Trim().ToLowerInvariant();

        return await _contexto.Sesiones
            .FirstOrDefaultAsync(sesion => sesion.HashDelToken == normalizado, cancellationToken);
    }

    // Cierra solo esa sesion. Si el token no existe o ya estaba cerrada, devuelve
    // false y el endpoint responde 204 igual: no se distingue un caso del otro.
    public async Task<bool> CerrarPorHashAsync(
        string hashDelToken,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default)
    {
        var hash = hashDelToken.Trim().ToLowerInvariant();

        var sesion = await _contexto.Sesiones
            .FirstOrDefaultAsync(registrada => registrada.HashDelToken == hash, cancellationToken);

        if (sesion is null || sesion.EstaCerrada)
        {
            return false;
        }

        sesion.Cerrar(ahoraUtc);
        await _contexto.SaveChangesAsync(cancellationToken);

        return true;
    }
}