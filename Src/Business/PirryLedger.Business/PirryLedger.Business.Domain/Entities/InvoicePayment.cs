using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Domain.Entities;

// RF-NEG-02: un pago de la factura. El efectivo nace confirmado y guarda el
// monto recibido y el cambio; una transferencia nace Registered y la confirma o
// rechaza un Administrador. Una transferencia rechazada se queda en la factura
// como historial, pero no cuenta para el total pagado.
//
// En una factura de delivery el recibido y el cambio no se registran: no se sabe
// con exactitud cuanto entrego ni cuanto devolvio el courier. Por eso el monto
// recibido es opcional aqui y la factura es la que exige que venga cuando la
// venta es de mostrador.
public sealed class InvoicePayment
{
    private InvoicePayment()
    {
    }

    private InvoicePayment(
        Guid id,
        PaymentKind kind,
        PaymentMethod method,
        PaymentStatus status,
        decimal amount,
        decimal? receivedAmount,
        DateTime registeredAtUtc,
        Guid registeredByUserId)
    {
        Id = id;
        Kind = kind;
        Method = method;
        Status = status;
        Amount = amount;
        ReceivedAmount = receivedAmount;
        ChangeAmount = receivedAmount - amount;
        RegisteredAtUtc = registeredAtUtc;
        RegisteredByUserId = registeredByUserId;
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public PaymentKind Kind { get; private set; }

    public PaymentMethod Method { get; private set; }

    public PaymentStatus Status { get; private set; }

    public decimal Amount { get; private set; }

    // Solo en efectivo de mostrador. En delivery y en transferencia queda null.
    public decimal? ReceivedAmount { get; private set; }

    public decimal? ChangeAmount { get; private set; }

    public DateTime RegisteredAtUtc { get; private set; }

    public Guid RegisteredByUserId { get; private set; }

    // Pago en efectivo del cobro de la venta. El monto recibido es opcional
    // porque en delivery no se registra; la factura decide si es obligatorio.
    public static InvoicePayment Cash(
        decimal amount,
        decimal? receivedAmount,
        DateTime registeredAtUtc,
        Guid registeredByUserId)
    {
        EnsureAmountIsValid(amount);

        if (receivedAmount is not null)
        {
            EnsureAmountIsValid(receivedAmount.Value);

            if (receivedAmount.Value < amount)
            {
                throw new InvoiceRuleViolationException(
                    "El monto recibido no puede ser menor que el monto del pago.");
            }
        }

        return new InvoicePayment(
            Guid.NewGuid(),
            PaymentKind.Sale,
            PaymentMethod.Cash,
            PaymentStatus.Confirmed,
            amount,
            receivedAmount,
            registeredAtUtc,
            registeredByUserId);
    }

    // Transferencia del cobro de la venta. Nace Registered: el Administrador la
    // confirma o la rechaza, y mientras tanto la factura queda en
    // PendingVerification.
    public static InvoicePayment Transfer(
        decimal amount,
        DateTime registeredAtUtc,
        Guid registeredByUserId)
    {
        EnsureAmountIsValid(amount);

        return new InvoicePayment(
            Guid.NewGuid(),
            PaymentKind.Sale,
            PaymentMethod.Transfer,
            PaymentStatus.Registered,
            amount,
            receivedAmount: null,
            registeredAtUtc,
            registeredByUserId);
    }

    // La factura es la unica que asigna el duenio del pago.
    internal void AttachTo(Guid invoiceId)
    {
        InvoiceId = invoiceId;
    }

    internal void Confirm()
    {
        Status = PaymentStatus.Confirmed;
    }

    internal void Reject()
    {
        Status = PaymentStatus.Rejected;
    }

    private static void EnsureAmountIsValid(decimal amount)
    {
        if (!Money.HasExactDecimals(amount))
        {
            throw new InvoiceRuleViolationException(
                "El monto del pago debe tener dos decimales exactos.");
        }

        if (amount <= 0m || amount > Money.MaximumAmount)
        {
            throw new InvoiceRuleViolationException(
                $"El monto del pago debe ser mayor que cero y no pasar de {Money.MaximumAmount:0.00}.");
        }
    }
}
