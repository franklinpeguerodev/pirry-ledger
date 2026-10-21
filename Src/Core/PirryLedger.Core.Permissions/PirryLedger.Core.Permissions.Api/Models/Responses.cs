namespace PirryLedger.Core.Permissions.Api.Models;

// Respuestas HTTP. La estructura es la misma que ListedPermissionRequest
// expuesta por Application, pero serializada como JSON anonimo en cada
// endpoint para mantener el contrato HTTP explicito.
public sealed record CreatedPermissionRequestResponse(Guid Id);