namespace PirryLedger.Core.Permissions.Domain;

// Catalogo de permisos declarados en un solo lugar (RF-GP-01,
// current-iteration.md punto 1). Dos clases de nombre:
//
//   1. Los fijos, como ViewSalesSummary, que estan en la lista cerrada.
//   2. El de anular una factura concreta, CancelInvoice:<guid>, que no puede
//      estar en una lista porque depende de la factura. Su convencion vive en
//      Core.Contracts.Permissions.PermissionNames, y aqui se valida con la
//      misma forma canonica (GUID "D" en minusculas) para que una factura
//      tenga un solo nombre. Las dos implementaciones se mantienen separadas
//      para que el dominio no dependa de Contracts; una prueba del modulo ata
//      la una a la otra.
//
// Cualquier otro nombre se rechaza con 400 al crear la solicitud.
public static class PermissionCatalog
{
    public const string ViewSalesSummary = "ViewSalesSummary";

    // Mismo literal que PermissionNames.CancelInvoicePrefix. No se comparte
    // para no crear la referencia Permissions.Domain -> Core.Contracts; la
    // prueba PruebasDelCatalogoDePermisos comprueba que las dos coinciden.
    private const string CancelInvoicePrefix = "CancelInvoice:";

    // Lista cerrada de los nombres fijos. Si se anade un permiso fijo, va
    // aqui y solo aqui.
    private static readonly IReadOnlyCollection<string> Known =
        new[] { ViewSalesSummary };

    public static IReadOnlyCollection<string> KnownPermissions() => Known;

    public static bool IsKnown(string permissionName)
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return false;
        }

        return Known.Contains(permissionName) || IsCancelInvoice(permissionName);
    }

    // Misma regla que PermissionNames.IsCancelInvoice, sin la referencia a
    // Contracts. El GUID canonico evita que la misma factura tenga dos
    // nombres (mayusculas, llaves o espacios).
    private static bool IsCancelInvoice(string permissionName)
    {
        if (!permissionName.StartsWith(CancelInvoicePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var raw = permissionName.Substring(CancelInvoicePrefix.Length);

        return Guid.TryParse(raw, out var parsed)
            && string.Equals(parsed.ToString("D"), raw, StringComparison.Ordinal);
    }
}
