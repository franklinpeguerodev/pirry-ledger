using PirryLedger.Core.AccessControl.Domain;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// Quien esta autenticado, ya validado. Viaja hasta el endpoint, que solo lo usa
// para responder.
public sealed record UsuarioAutenticado(Guid Id, string Nombre, string Correo, Rol Rol);

// Excepcion unica de autenticacion. El endpoint la convierte en 401. El rol
// insuficiente NO se lanza desde aqui: es un 403 y lo decide el punto de rol.
public sealed class SesionInvalidaException : Exception
{
    public SesionInvalidaException() : base("Sesion no valida.")
    {
    }
}

// RF-CA-06: rol insuficiente. Distinto de SesionInvalidaException a proposito,
// porque el codigo tambien es distinto: 403 dice "tu sesion vale, tu rol no".
public sealed class RolInsuficienteException : Exception
{
    public RolInsuficienteException() : base("No tiene permisos para esta operacion.")
    {
    }
}

// EL UNICO PUNTO DE VALIDACION.
//
// Las cinco condiciones de docs/adr/001-session-credential.md se comprueban aqui
// y en ningun otro sitio: la sesion existe, no esta cerrada, no vencio, su
// CredencialVersion coincide con la del usuario y el usuario sigue activo.
//
// Que sea uno solo no es estilo, es el requisito: "State transitions are resolved
// in a single component (RD-04)" y "Authorization is enforced on the server for
// every operation (RD-06)". Si cada endpoint comprobara la sesion por su cuenta,
// bastaria uno que se olvidara para abrir un agujero, y el rechazo dejaria de
// funcionar con peticiones armadas a mano contra la API.
public sealed class Autenticar
{
    private readonly ISessionRepository _sesiones;
    private readonly IUserRepository _usuarios;
    private readonly IClock _reloj;

    public Autenticar(ISessionRepository sesiones, IUserRepository usuarios, IClock reloj)
    {
        _sesiones = sesiones;
        _usuarios = usuarios;
        _reloj = reloj;
    }

    // Lanza SesionInvalidaException si no hay sesion valida. Devuelve el usuario
    // ya validado: el endpoint no vuelve a comprobar nada.
    public async Task<UsuarioAutenticado> EjecutarAsync(string tokenEnClaro, CancellationToken cancellationToken = default)
    {
        var usuario = await ValidarAsync(tokenEnClaro, cancellationToken);

        return new UsuarioAutenticado(usuario.Id, usuario.Nombre, usuario.Correo, usuario.Rol);
    }

    // La misma validacion, pero devolviendo false en vez de lanzar. Para los
    // endpoints que solo necesitan saber si hay sesion.
    public async Task<bool> HaySesionValidaAsync(string tokenEnClaro, CancellationToken cancellationToken = default)
    {
        try
        {
            await ValidarAsync(tokenEnClaro, cancellationToken);
            return true;
        }
        catch (SesionInvalidaException)
        {
            return false;
        }
    }

    // El rol tambien se comprueba aqui y no en el endpoint, por el mismo motivo
    // (RD-06): el rechazo tiene que ocurrir con peticiones hechas a mano.
    public async Task<UsuarioAutenticado> EjecutarConRolAsync(
        string tokenEnClaro,
        Rol rolRequerido,
        CancellationToken cancellationToken = default)
    {
        var usuario = await ValidarAsync(tokenEnClaro, cancellationToken);

        if (usuario.Rol != rolRequerido)
        {
            throw new RolInsuficienteException();
        }

        return new UsuarioAutenticado(usuario.Id, usuario.Nombre, usuario.Correo, usuario.Rol);
    }

    // El nucleo: las cinco condiciones, en un solo metodo. Todo lo demas lo llama.
    private async Task<Usuario> ValidarAsync(string tokenEnClaro, CancellationToken cancellationToken)
    {
        // RD-07: una cabecera vacia o corrupta es un 401, no una excepcion sin
        // manejar.
        if (string.IsNullOrWhiteSpace(tokenEnClaro))
        {
            throw new SesionInvalidaException();
        }

        var hash = SessionTokenGenerator.CalcularHash(tokenEnClaro.Trim());

        var sesion = await _sesiones.BuscarPorHashAsync(hash, cancellationToken);

        if (sesion is null)
        {
            throw new SesionInvalidaException();
        }

        var usuario = await _usuarios.BuscarPorIdAsync(sesion.UsuarioId, cancellationToken);

        if (usuario is null)
        {
            throw new SesionInvalidaException();
        }

        // Las cuatro condiciones restantes viven en la entidad (RD-04). Aqui solo
        // se le pasa el usuario y el reloj.
        if (!sesion.EsValida(_reloj.UtcNow, usuario))
        {
            throw new SesionInvalidaException();
        }

        return usuario;
    }
}