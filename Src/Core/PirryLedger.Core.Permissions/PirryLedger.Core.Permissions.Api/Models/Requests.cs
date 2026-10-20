namespace PirryLedger.Core.Permissions.Api.Models;

// Cuerpos de las peticiones HTTP. Binding de Minimal API valida que las
// propiedades requeridas lleguen; si el JSON falta o es invalido, devuelve
// 400 sin pasar por el caso de uso (RD-07).
public sealed class CreatePermissionRequestBody
{
    public string? RequestedPermission { get; init; }
}

public sealed class RejectPermissionRequestBody
{
    public string? Reason { get; init; }
}