using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Domain.Entities;

// RF-NEG-02: una linea de la factura. El total de la linea se calcula, no se
// guarda: guardarlo permitiria que contradijera al precio y a la cantidad.
public sealed class InvoiceItem
{
    public const int MaximumNameLength = 120;

    public const int MaximumQuantity = 999;

    private InvoiceItem()
    {
    }

    private InvoiceItem(Guid id, string name, decimal unitPrice, int quantity)
    {
        Id = id;
        Name = name;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    public static InvoiceItem Create(string name, decimal unitPrice, int quantity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvoiceRuleViolationException("El nombre del ítem es obligatorio.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaximumNameLength)
        {
            throw new InvoiceRuleViolationException(
                $"El nombre del ítem no puede pasar de {MaximumNameLength} caracteres.");
        }

        if (!Money.HasExactDecimals(unitPrice))
        {
            throw new InvoiceRuleViolationException(
                "El precio del ítem debe tener dos decimales exactos.");
        }

        if (unitPrice <= 0m || unitPrice > Money.MaximumAmount)
        {
            throw new InvoiceRuleViolationException(
                $"El precio del ítem debe ser mayor que cero y no pasar de {Money.MaximumAmount:0.00}.");
        }

        if (quantity < 1 || quantity > MaximumQuantity)
        {
            throw new InvoiceRuleViolationException(
                $"La cantidad del ítem debe estar entre 1 y {MaximumQuantity}.");
        }

        return new InvoiceItem(Guid.NewGuid(), trimmed, unitPrice, quantity);
    }

    // La factura es la unica que asigna el duenio del item.
    internal void AttachTo(Guid invoiceId)
    {
        InvoiceId = invoiceId;
    }
}
