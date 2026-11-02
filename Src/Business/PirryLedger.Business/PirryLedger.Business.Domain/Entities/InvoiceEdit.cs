namespace PirryLedger.Business.Domain.Entities;

// Un registro del historial de edicion de una factura emitida: quien, cuando y
// cuantos items quedaron. Solo un Administrador puede leer este historial.
public sealed class InvoiceEdit
{
    public InvoiceEdit(Guid id, Guid invoiceId, DateTime editedAtUtc, Guid editedByUserId, int itemCount)
    {
        Id = id;
        InvoiceId = invoiceId;
        EditedAtUtc = editedAtUtc;
        EditedByUserId = editedByUserId;
        ItemCount = itemCount;
    }

    public Guid Id { get; }

    public Guid InvoiceId { get; }

    public DateTime EditedAtUtc { get; }

    public Guid EditedByUserId { get; }

    public int ItemCount { get; }
}
