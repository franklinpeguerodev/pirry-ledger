namespace PirryLedger.Business.Application;

// Las reglas de la imagen que vienen de la configuracion (ADR 015).
//
// Viven en la capa de Application y no en la de Infrastructure porque los
// casos de uso las necesitan y no pueden depender de la configuracion del
// bucket, que es un detalle de persistencia.
public sealed record ProductImageOptions(long MaxBytes, int SignatureMinutes);
