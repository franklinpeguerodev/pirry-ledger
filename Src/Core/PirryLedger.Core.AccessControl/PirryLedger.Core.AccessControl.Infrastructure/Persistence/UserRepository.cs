using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class UserRepository : IUserRepository
{
    private readonly AccessControlDbContext _context;

    public UserRepository(AccessControlDbContext context)
    {
        _context = context;
    }

    // Normalizar a minusculas es lo que hace que "Juan@Email.com" y
    // "juan@Email.com" sean la misma persona. Sin esto el indice unico de
    // PostgreSQL los trataria como distintos y habria dos cuentas.
    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();

        return await _context.Users
            .FirstOrDefaultAsync(user => user.Email == normalized, cancellationToken);
    }

    public async Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    // RF-CA-21. Se ordena por Email para que el listado sea estable: sin
    // OrderBy, PostgreSQL devuelve las filas en el orden que le resulte y el
    // listado cambiaria entre dos peticiones seguidas sin que nadie haya
    // tocado nada.
    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);
    }

    // RF-CA-20: cuenta solo los ACTIVOS, porque un Administrator desactivado no
    // puede volver a entrar a Reactivate a nadie. Si se contaran tambien los
    // inactivos, el ultimo Administrator Active podria desactivarse y el
    // sistema se quedaria sin administracion sin avisar.
    public async Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .CountAsync(user => user.Role == Role.Administrator && user.Active, cancellationToken);
    }

    // La entidad normaliza el Email al crearse, asi que aqui no hay que
    // reescribirlo. El repositorio solo decide si la fila es nueva o existente.
    public async Task SaveAsync(User user, CancellationToken cancellationToken = default)
    {
        if (_context.Users.Local.Any(registered => registered.Id == user.Id))
        {
            _context.Users.Update(user);
        }
        else
        {
            _context.Users.Add(user);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // AnyAsync se detiene en la primera fila que cumple, asi que no trae la lista
    // de Administradores: solo responde si hay alguno.
    public async Task<bool> AnyAdministratorExistsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AnyAsync(user => user.Role == Role.Administrator, cancellationToken);
    }
}
