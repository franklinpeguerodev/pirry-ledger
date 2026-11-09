namespace PirryLedger.Business.Infrastructure.Persistence;

// El contador global de revisiones del conjunto de imagenes (ADR 015).
//
// Una sola fila (id = 1). Cada cambio del conjunto reserva la siguiente
// revision. El bloqueo de la fila durante la transaccion serializa las
// escrituras: la revision N+1 nunca se confirma antes que la N, y asi el
// cursor del cliente no se salta ningun cambio.
//
// No es una columna identity porque una revision tambien se necesita al
// actualizar una fila que ya existe (cuando una imagen sale del conjunto), y
// una identity solo se asigna al insertar.
public sealed class ImageRevisionCounter
{
    public int Id { get; set; }

    public long Value { get; set; }
}
