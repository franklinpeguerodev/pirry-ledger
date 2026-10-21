using PirryLedger.Core.Permissions.Application.Exceptions;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Application.UseCases;

// Proyecciones para las respuestas HTTP. Mismo motivo que ListedUser en
// AccessControl: si el endpoint devolviera la entidad, cualquier campo nuevo
// (por ejemplo, un id interno futuro) se filtraria solo, sin revision. Con
// un record dedicado, los campos que salen son los que existen aqui.
public sealed record ListedPermissionRequest(
    Guid Id,
    Guid RequesterId,
    string RequestedPermission,
    string State,
    Guid? ApproverId,
    string? RejectionReason,
    DateTime CreatedAtUtc,
    DateTime? ApprovedAtUtc,
    DateTime? AppliedAtUtc,
    DateTime? RejectedAtUtc);

internal static class PermissionRequestMapping
{
    public static ListedPermissionRequest ToListed(PermissionRequest request) =>
        new(
            request.Id,
            request.RequesterId,
            request.RequestedPermission,
            request.State.ToString(),
            request.ApproverId,
            request.RejectionReason,
            request.CreatedAtUtc,
            request.ApprovedAtUtc,
            request.AppliedAtUtc,
            request.RejectedAtUtc);

    public static PermissionRequestState ParseState(string state) =>
        Enum.TryParse(state, ignoreCase: true, out PermissionRequestState parsed)
            ? parsed
            : throw new PermissionRequestRejectedException("El estado indicado no existe.");
}