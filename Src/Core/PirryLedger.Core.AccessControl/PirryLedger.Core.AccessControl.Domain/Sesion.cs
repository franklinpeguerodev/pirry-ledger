namespace PirryLedger.Core.AccessControl.Domain;

// RF-CA-03 y RF-CA-18. Decision documentada en docs/adr/001-session-credential.md.
//
// La credencial es opaca: 32 bytes del generador del sistema en base64url. En la
// base solo queda su SHA-256, asi que leer la tabla no permite autenticarse.
//
// El token NUNCA se guarda en claro, igual que el de activacion (RF-CA-15). Si un
// token se guarda en claro, un SELECT bastaria para suplantar a cualquier
// empleado, y en una tabla que se lee para diagnosticar.
//
// Caducidad ABSOLUTA, no deslizante: la sesion no se renueva aunque el usuario
// este activo. Un token robado tiene 8 horas de vida maxima en lugar de valer
// mientras haya trafico. El ADR explica el razonamiento.
public sealed class Sesion
{
    private Sesion()
    {
    }

    private Sesion(
        Guid id,
        Guid usuarioId,
        string hashDelToken,
        DateTime emitidaUtc,
        DateTime expiraUtc,
        int credencialVersion)
    {
        Id = id;
        UsuarioId = usuarioId;
        HashDelToken = hashDelToken;
        EmitidaUtc = emitidaUtc;
        ExpiraUtc = expiraUtc;
        CerradaUtc = null;
        CredencialVersion = credencialVersion;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    // SHA-256 del token, en el mismo formato que el de activacion.
    public string HashDelToken { get; private set; } = string.Empty;

    public DateTime EmitidaUtc { get; private set; }

    // Vencimiento absoluto. No se toca al usarla.
    public DateTime ExpiraUtc { get; private set; }

    // RF-CA-18: el instante del cierre. Nulo mientras la sesion esta abierta.
    public DateTime? CerradaUtc { get; private set; }

    // Decision de Franklin. Copia del valor que tenia Usuario.CredencialVersion al
    // emitirse. Si el usuario sube el suyo (cambio de contrasena, restablecimiento
    // o desactivacion), deja de coincidir y todas sus sesiones caen sin tocar la
    // tabla sesion, que es lo que hace RF-CA-12 y RF-CA-20 de inmediato.
    public int CredencialVersion { get; private set; }

    public bool EstaCerrada => CerradaUtc is not null;

    public bool HaVencido(DateTime ahoraUtc) => ahoraUtc >= ExpiraUtc;

    // Las reglas de vigencia viven en la entidad y en un solo sitio (RD-04).
    // Devuelve por que se rechaza, porque el endpoint necesita distinguir un 401
    // por credencial caducada de un 401 por credencial desconocida, y decidirlo
    // aqui evita repetir las cinco comparaciones en cada punto de entrada.
    public bool EsValida(DateTime ahoraUtc, Usuario usuario) =>
        !EstaCerrada
        && !HaVencido(ahoraUtc)
        && CredencialVersion == usuario.CredencialVersion
        && usuario.Activo;

    // Decision propia. El token se genera fuera y entra ya hasheado, igual que el
    // de activacion: Domain no ve ningun paquete de criptografia.
    public static Sesion Crear(
        Guid usuarioId,
        string hashDelToken,
        DateTime ahoraUtc,
        TimeSpan duracionDeValidez,
        int credencialVersionDelUsuario)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashDelToken);

        if (duracionDeValidez <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duracionDeValidez), "La sesion necesita una duracion de validez positiva.");
        }

        if (credencialVersionDelUsuario < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(credencialVersionDelUsuario), "La version de credencial no puede ser negativa.");
        }

        return new Sesion(
            Guid.NewGuid(),
            usuarioId,
            hashDelToken,
            ahoraUtc,
            ahoraUtc.Add(duracionDeValidez),
            credencialVersionDelUsuario);
    }

    // RF-CA-18: cerrar una sesion. Cerrar dos veces la misma es un no-op, no un
    // error: un segundo cierre llega por la carrera de dos pestanas cerrando a la
    // vez, y eso no es un fallo del usuario.
    public void Cerrar(DateTime ahoraUtc)
    {
        if (EstaCerrada)
        {
            return;
        }

        CerradaUtc = ahoraUtc;
    }
}