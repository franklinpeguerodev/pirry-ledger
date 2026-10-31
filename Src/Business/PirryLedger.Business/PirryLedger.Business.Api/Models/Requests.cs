namespace PirryLedger.Business.Api.Models;

// Cuerpos de las peticiones HTTP del negocio. Las propiedades son nullable a
// proposito: un JSON incompleto tiene que llegar al caso de uso como un dato
// que falta, no reventar el binding con un 500. El metodo de pago viaja como
// texto y se valida aqui, porque el enum PaymentMethod tiene un valor (None)
// que no es un medio de pago valido para cobrar.
public sealed class IssueInvoiceBody
{
    public bool IsDelivery { get; init; }

    public List<InvoiceItemBody>? Items { get; init; }
}

public sealed class InvoiceItemBody
{
    public string? Name { get; init; }

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }
}

public sealed class PaymentBody
{
    public string? Method { get; init; }

    public decimal Amount { get; init; }

    public decimal? ReceivedAmount { get; init; }
}

public sealed class CollectPaymentBody
{
    public List<PaymentBody>? Payments { get; init; }
}

public sealed class LeaveAsDebtBody
{
    public string? DebtorName { get; init; }

    public List<PaymentBody>? Payments { get; init; }
}

public sealed class EditItemsBody
{
    public List<InvoiceItemBody>? Items { get; init; }
}

public sealed class RejectTransferBody
{
    public string? DebtorName { get; init; }
}

public sealed class DebtPaymentBody
{
    public decimal Amount { get; init; }
}
