using Microsoft.EntityFrameworkCore;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Infrastructure;

internal sealed class UserRepository : IUserRepository
{
    private readonly AccessControlDbContext _contexto;

    public UserRepository(AccessControlDbContext contexto)
    {
        _contexto = contexto;
    }

    // Normalizar a minusculas es lo que hace que "Juan@Correo.com" y
    // "juan@correo.com" sean la misma persona. Sin esto el indice unico de
    // PostgreSQL los trataria como distintos y habria dos cuentas.
    public async Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correo))
        {
            return null;
        }

        var normalizado = correo.Trim().ToLowerInvariant();

        return await _contexto.Usuarios
            .FirstOrDefaultAsync(usuario => usuario.Correo == normalizado, cancellationToken);
    }

    public async Task<Usuario?> BuscarPorIdAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        return await _contexto.Usuarios
            .FirstOrDefaultAsync(usuario => usuario.Id == usuarioId, cancellationToken);
    }

    // La entidad normaliza el correo al crearse, asi que aqui no hay que
    // reescribirlo. El repositorio solo decide si la fila es nueva o existente.
    public async Task GuardarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        if (_contexto.Usuarios.Local.Any(registrado => registrado.Id == usuario.Id))
        {
            _contexto.Usuarios.Update(usuario);
        }
        else
        {
            _contexto.Usuarios.Add(usuario);
        }

        await _contexto.SaveChangesAsync(cancellationToken);
    }

    // AnyAsync se detiene en la primera fila que cumple, asi que no trae la lista
    // de Administradores: solo responde si hay alguno.
    public async Task<bool> ExisteAlgunAdministradorAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Usuarios
            .AnyAsync(usuario => usuario.Rol == Rol.Administrador, cancellationToken);
    }
}