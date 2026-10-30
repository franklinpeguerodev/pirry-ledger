namespace PirryLedger.Core.Contracts.Permissions;

// RF-NEG-06: convencion del permiso que autoriza anular una factura concreta.
// Vive en Core.Contracts porque la usan dos piezas: Business construye el
// nombre al pedir el permiso y Permissions lo valida en su catalogo. Business
// no puede depender de Permissions.Domain, asi que la convencion tiene que
// estar en el contrato compartido.
//
// El GUID va en formato "D" y en minusculas. Esa forma canonica es la unica
// que el catalogo acepta: asi una misma factura tiene un solo nombre de
// permiso y no se puede pedir dos veces con mayusculas o con llaves.
public static class PermissionNames
{
    public const string CancelInvoicePrefix = "CancelInvoice:";

    // 14 caracteres del prefijo + 36 del GUID "D" = 50, y la columna
    // permission_name es varchar(64) (ADR 008).
    public static string CancelInvoice(Guid invoiceId) =>
        CancelInvoicePrefix + invoiceId.ToString("D");

    public static bool IsCancelInvoice(string permissionName) =>
        IsCancelInvoice(permissionName, out _);

    public static bool IsCancelInvoice(string permissionName, out Guid invoiceId)
    {
        invoiceId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(permissionName)
            || !permissionName.StartsWith(CancelInvoicePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var raw = permissionName.Substring(CancelInvoicePrefix.Length);

        if (!Guid.TryParse(raw, out var parsed))
        {
            return false;
        }

        // Solo la forma canonica: Guid.TryParse acepta mayusculas, llaves y
        // espacios, pero el nombre guardado tiene que ser uno solo.
        if (!string.Equals(parsed.ToString("D"), raw, StringComparison.Ordinal))
        {
            return false;
        }

        invoiceId = parsed;
        return true;
    }
}
