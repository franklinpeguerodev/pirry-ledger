using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RD-12: las piezas se prueban sin levantar la aplicacion ni tocar PostgreSQL.
// Los dobles de los repositorios viven aqui, en el proyecto de pruebas.
//
// El reloj falso es lo que hace que RF-CA-19 sea comprobable: los quince minutos
// de bloqueo se recorren instanciando otro reloj quince minutos adelante, sin
// esperar. Con el reloj real, esta prueba tardaria un cuarto de hora.

internal sealed class RelojFalso : IClock
{
    public RelojFalso(DateTime ahora)
    {
        UtcNow = ahora;
    }

    public DateTime UtcNow { get; set; }

    public void Avanzar(TimeSpan cantidad) => UtcNow = UtcNow.Add(cantidad);
}

internal sealed class UsuarioEnMemoria : IUserRepository
{
    private readonly List<Usuario> _usuarios = [];

    public Usuario Crear(string nombre, string correo, string hash, DateTime ahoraUtc, bool activo, Domain.Rol rol = Domain.Rol.Estandar)
    {
        // Se crea y luego se activa o no desde fuera, porque el constructor de
        // Usuario lo deja siempre inactivo (RF-CA-15).
        var usuario = Usuario.Crear(nombre, correo, hash, ahoraUtc);

        if (activo)
        {
            usuario.Activar();
        }

        // Crear no recibe rol: el usuario nace con el rol por defecto y se cambia
        // despues con CambiarRol, que es el camino real.
        if (rol != usuario.Rol)
        {
            usuario.CambiarRol(rol);
        }

        _usuarios.Add(usuario);

        return usuario;
    }

    public Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios
            .FirstOrDefault(usuario => usuario.Correo == correo.Trim().ToLowerInvariant()));

    public Task<Usuario?> BuscarPorIdAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(usuario => usuario.Id == usuarioId));

    public Task GuardarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        // El doble guarda la misma instancia, asi que las mutaciones de la entidad
        // ya estan visibles. No hace falta persistir nada.
        //
        // Aun asi hay que registrar la fila si no estaba: hay casos de uso (como
        // el seed del primer Administrador) que crean la entidad y la guardan
        // sin pasar antes por Crear, y sin esto no se podrian volver a buscar.
        if (!_usuarios.Any(registrado => registrado.Id == usuario.Id))
        {
            _usuarios.Add(usuario);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExisteAlgunAdministradorAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios.Any(usuario => usuario.Rol == Domain.Rol.Administrador));

    // Solo para pruebas: el repositorio real no necesita listar Administradores.
    // Sirve para comprobar que el seed no creo dos.
    public Task<List<Usuario>> BuscarTodosLosAdministradoresAsync() =>
        Task.FromResult(_usuarios.Where(usuario => usuario.Rol == Domain.Rol.Administrador).ToList());
}

internal sealed class SesionEnMemoria : ISessionRepository
{
    private readonly List<Sesion> _sesiones = [];

    public int Guardadas => _sesiones.Count;

    public Sesion? Buscar(string hashDelToken) =>
        _sesiones.FirstOrDefault(sesion => sesion.HashDelToken == hashDelToken);

    public Task GuardarAsync(Sesion sesion, CancellationToken cancellationToken = default)
    {
        _sesiones.Add(sesion);
        return Task.CompletedTask;
    }

    public Task<Sesion?> BuscarPorHashAsync(string hashDelToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(Buscar(hashDelToken));

    public Task<bool> CerrarPorHashAsync(
        string hashDelToken,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default)
    {
        var sesion = Buscar(hashDelToken);

        if (sesion is null || sesion.EstaCerrada)
        {
            return Task.FromResult(false);
        }

        sesion.Cerrar(ahoraUtc);

        return Task.FromResult(true);
    }
}