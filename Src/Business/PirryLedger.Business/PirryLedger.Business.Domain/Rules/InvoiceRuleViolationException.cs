namespace PirryLedger.Business.Domain.Rules;

// Una regla de negocio de la factura no se cumple (dinero, nombres, estado de la
// edicion). Es un rechazo controlado distinto de una transicion prohibida
// (InvalidInvoiceTransitionException) y de una condicion de la maquina
// (InvoiceConditionNotMetException): el endpoint los traduce todos a 400.
public sealed class InvoiceRuleViolationException : Exception
{
    public InvoiceRuleViolationException(string message)
        : base(message)
    {
    }
}
