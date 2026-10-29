namespace PirryLedger.Business.Domain.Rules;

// Regla de dinero de Franklin: dos decimales exactos siempre. No se redondea en
// silencio; un importe con mas precision se rechaza, para que el total de la
// factura sea siempre la suma exacta de sus lineas.
public static class Money
{
    public const int Decimals = 2;

    public const decimal MaximumAmount = 1_000_000m;

    public static bool HasExactDecimals(decimal value) =>
        decimal.Round(value, Decimals) == value;
}
