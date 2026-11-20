namespace PirryLedger.Business.Application.Exceptions;

public sealed class DebtAccessDeniedException : Exception
{
    public DebtAccessDeniedException() : base("No tiene acceso a esa operación de deudas.")
    {
    }
}
