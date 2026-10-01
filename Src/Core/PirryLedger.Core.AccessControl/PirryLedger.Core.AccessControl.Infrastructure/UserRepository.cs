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

    // RF-CA-21. Se ordena por correo para que el listado sea estable: sin
    // OrderBy, PostgreSQL devuelve las filas en el orden que le resulte y el
    // listado cambiaria entre dos peticiones seguidas sin que nadie haya
    // tocado nada.
    public async Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Usuarios
            .OrderBy(usuario => usuario.Correo)
            .ToListAsync(cancellationToken);
    }

    // RF-CA-20: cuenta solo los ACTIVOS, porque un Administrador desactivado no
    // puede volver a entrar a reactivar a nadie. Si se contaran tambien los
    // inactivos, el ultimo Administrador activo podria desactivarse y el
    // sistema se quedaria sin administracion sin avisar.
    public async Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Usuarios
            .CountAsync(usuario => usuario.Rol == Rol.Administrador && usuario.Activo, cancellationToken);
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