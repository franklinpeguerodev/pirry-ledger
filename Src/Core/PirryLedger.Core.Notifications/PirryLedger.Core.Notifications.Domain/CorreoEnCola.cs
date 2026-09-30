namespace PirryLedger.Core.Notifications.Domain;

// RF-NOT-08 y RF-NOT-09: el correo no sale de la operacion que lo origina, queda
// en esta fila como Pendiente y un proceso aparte lo entrega.
//
// Atributos minimos exigidos por docs/requirements/entities.md: destinatario,
// asunto, cuerpo, estado, intentos, fecha de creacion, fecha de envio y ultimo
// error. Los tres ultimos se crean vacios y los rellena el emisor.
//
// La regla de transiciones vive aqui, en la entidad (RD-04): de Enviado no se
// sale, y de Enviado a Enviado tampoco, que es lo que hace imposible el envio
// duplicado de RF-NOT-12.
public sealed class CorreoEnCola
{
    private CorreoEnCola()
    {
    }

    private CorreoEnCola(Guid id, string destinatario, string asunto, string cuerpo, DateTime fechaCreacionUtc)
    {
        Id = id;
        Destinatario = destinatario;
        Asunto = asunto;
        Cuerpo = cuerpo;
        Estado = EstadoCorreo.Pendiente;
        Intentos = 0;
        FechaCreacionUtc = fechaCreacionUtc;
        FechaEnvioUtc = null;
        UltimoError = null;
    }

    public Guid Id { get; private set; }

    public string Destinatario { get; private set; } = string.Empty;

    public string Asunto { get; private set; } = string.Empty;

    public string Cuerpo { get; private set; } = string.Empty;

    public EstadoCorreo Estado { get; private set; }

    public int Intentos { get; private set; }

    public DateTime FechaCreacionUtc { get; private set; }

    public DateTime? FechaEnvioUtc { get; private set; }

    public string? UltimoError { get; private set; }

    // Una sola puerta de entrada: nada escribe la fila directamente.
    public static CorreoEnCola Crear(string destinatario, string asunto, string cuerpo, DateTime fechaCreacionUtc)
    {
        if (string.IsNullOrWhiteSpace(destinatario))
        {
            throw new ArgumentException("El destinatario es obligatorio.", nameof(destinatario));
        }

        if (string.IsNullOrWhiteSpace(asunto))
        {
            throw new ArgumentException("El asunto es obligatorio.", nameof(asunto));
        }

        return new CorreoEnCola(Guid.NewGuid(), destinatario, asunto, cuerpo, fechaCreacionUtc);
    }

    // El emisor toma el correo. De Pending a Procesando es el unico salto que
    // permite despues marcar Enviado, asi que dos procesos que lean la cola a
    // la vez no pueden enviar el mismo correo.
    public void Reclamar()
    {
        if (Estado != EstadoCorreo.Pendiente)
        {
            throw new InvalidOperationException(
                $"Solo un correo Pendiente se puede reclamar. Estado actual: {Estado}.");
        }

        Estado = EstadoCorreo.Procesando;
        Intentos++;
    }

    public void MarcarEnviado(DateTime ahoraUtc)
    {
        if (Estado != EstadoCorreo.Procesando)
        {
            throw new InvalidOperationException(
                $"Solo un correo Procesando se puede marcar Enviado. Estado actual: {Estado}.");
        }

        Estado = EstadoCorreo.Enviado;
        FechaEnvioUtc = ahoraUtc;
    }

    // El transporte fallo. El correo vuelve a Pendiente para que la proxima
    // ejecucion del emisor lo intente otra vez. No hay reintento automatico:
    // el emisor corre cuando alguien lo lanza.
    public void Liberar()
    {
        if (Estado != EstadoCorreo.Procesando)
        {
            throw new InvalidOperationException(
                $"Solo un correo Procesando se puede liberar. Estado actual: {Estado}.");
        }

        Estado = EstadoCorreo.Pendiente;
    }
}
