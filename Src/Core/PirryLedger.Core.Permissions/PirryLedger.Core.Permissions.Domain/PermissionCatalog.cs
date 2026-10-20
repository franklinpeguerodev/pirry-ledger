namespace PirryLedger.Core.Permissions.Domain;

// Catalogo fijo de permisos declarados en un solo lugar (RF-GP-01,
// current-iteration.md punto 1). Un solo permiso por ahora: ViewSalesSummary,
// el que se demuestra con la ruta de resumen de ventas.
//
// Agregar un permiso nuevo es: anadir el nombre aqui, declarar la ruta que lo
// exige en RoleRequirements, y registrar una operacion en el enum Operation.
// Cualquier ruta que use un nombre que NO este aqui se rechaza con 400.
public static class PermissionCatalog
{
    public const string ViewSalesSummary = "ViewSalesSummary";

    // Lista cerrada. Si se anade un permiso, va aqui y solo aqui.
    private static readonly IReadOnlyCollection<string> Known =
        new[] { ViewSalesSummary };

    public static IReadOnlyCollection<string> KnownPermissions() => Known;
}