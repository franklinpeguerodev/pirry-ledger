namespace PirryLedger.Core.AccessControl.Domain;

// RF-CA-09, RF-CA-10 y RF-CA-11: el codigo de recuperacion.
//
// Entidad exigida por docs/requirements/entities.md, con sus atributos minimos:
// Usuario, codigo, fecha de emision, fecha de vencimiento y usado o no usado.
//
// El codigo NUNCA se guarda en claro. Se guarda su SHA-256, igual que el token de
// activacion. Esta fase solo crea la entidad y su transicion de un solo uso; el
// flujo completo de recuperacion llega en la fase siguiente.
public sealed class CodigoRecuperacion
{
    private CodigoRecuperacion()
    {
    }

    private CodigoRecuperacion(
        Guid id,
        Guid usuarioId,
        string hashDelCodigo,
        DateTime emitidoUtc,
        DateTime expiraUtc)
    {
        Id = id;
        UsuarioId = usuarioId;
        HashDelCodigo = hashDelCodigo;
        EmitidoUtc = emitidoUtc;
        ExpiraUtc = expiraUtc;
        UsadoUtc = null;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    // SHA-256 del codigo. El codigo en claro solo existe en el correo.
    public string HashDelCodigo { get; private set; } = string.Empty;

    public DateTime EmitidoUtc { get; private set; }

    public DateTime ExpiraUtc { get; private set; }

    public DateTime? UsadoUtc { get; private set; }

    public bool EstaUsado => UsadoUtc is not null;

    public bool HaVencido(DateTime ahoraUtc) => ahoraUtc >= ExpiraUtc;

    // Un solo uso (RF-CA-10): las transiciones viven en la entidad (RD-04).
    public void MarcarUsado(DateTime ahoraUtc)
    {
        if (EstaUsado)
        {
            throw new InvalidOperationException("El codigo de recuperacion ya se uso.");
        }

        UsadoUtc = ahoraUtc;
    }

    public static CodigoRecuperacion Crear(
        Guid usuarioId,
        string hashDelCodigo,
        DateTime ahoraUtc,
        TimeSpan duracionDeValidez)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashDelCodigo);

        if (duracionDeValidez <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duracionDeValidez), "El codigo necesita una duracion de validez positiva.");
        }

        return new CodigoRecuperacion(
            Guid.NewGuid(),
            usuarioId,
            hashDelCodigo,
            ahoraUtc,
            ahoraUtc.Add(duracionDeValidez));
    }
}