namespace PirryLedger.Business.Domain.Enums;

public enum PaymentMethod
{
    Cash,
    Transfer,

    // Registros históricos sin método. Los abonos nuevos exigen Cash o Transfer;
    // no se atribuye un método ficticio a datos anteriores.
    None,
}
