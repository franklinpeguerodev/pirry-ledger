namespace PirryLedger.Core.AccessControl.Domain;

// RF-CA-15 y RF-CA-16: el enlace de activacion lleva un token de un solo uso con
// fecha de vencimiento.
//
// entities.md no define esta entidad: la lista de "decisiones de diseno" incluye
// expresamente el token de activacion. Se guarda aparte y no junto al codigo de
// recuperacion (que si figura en entities.md como CodigoRecuperacion), y esta
// separacion es una decision de Franklin.
//
// El token NUNCA se guarda en claro. Lo que se guarda es su SHA-256: leer la
// tabla no permite completar ningun enlace de activacion.
public sealed class TokenActivacion
{
    private TokenActivacion()
    {
    }

    private TokenActivacion(
        Guid id,
        Guid usuarioId,
        string hashDelToken,
        DateTime emitidoUtc,
        DateTime expiraUtc)
    {
        Id = id;
        UsuarioId = usuarioId;
        HashDelToken = hashDelToken;
        EmitidoUtc = emitidoUtc;
        ExpiraUtc = expiraUtc;
        UsadoUtc = null;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    // SHA-256 del token. El token en claro solo existe en el correo.
    public string HashDelToken { get; private set; } = string.Empty;

    // Atributos de entities.md para los codigos de un solo uso: fecha de emision
    // y fecha de vencimiento. El "usado o no usado" es UsadoUtc a null.
    public DateTime EmitidoUtc { get; private set; }

    public DateTime ExpiraUtc { get; private set; }

    public DateTime? UsadoUtc { get; private set; }

    public bool EstaUsado => UsadoUtc is not null;

    public bool HaVencido(DateTime ahoraUtc) => ahoraUtc >= ExpiraUtc;

    // Un solo uso: la regla de transiciones vive en la entidad (RD-04).
    // Marcar usado un token ya usado es un error, no un no-op silencioso.
    public void MarcarUsado(DateTime ahoraUtc)
    {
        if (EstaUsado)
        {
            throw new InvalidOperationException("El token de activacion ya se uso.");
        }

        UsadoUtc = ahoraUtc;
    }

    // Decision propia: el token generado en el servidor. El valor en claro se
    // devuelve para mandar el correo y no se vuelve a guardar en ningun sitio.
    public static TokenActivacion Crear(
        Guid usuarioId,
        string hashDelToken,
        DateTime ahoraUtc,
        TimeSpan duracionDeValidez)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashDelToken);

        if (duracionDeValidez <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duracionDeValidez), "El token necesita una duracion de validez positiva.");
        }

        return new TokenActivacion(
            Guid.NewGuid(),
            usuarioId,
            hashDelToken,
            ahoraUtc,
            ahoraUtc.Add(duracionDeValidez));
    }
}