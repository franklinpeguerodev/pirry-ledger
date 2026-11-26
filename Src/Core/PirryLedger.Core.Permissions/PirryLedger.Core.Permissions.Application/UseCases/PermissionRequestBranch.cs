using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.Permissions.Application.Exceptions;

namespace PirryLedger.Core.Permissions.Application;

internal static class PermissionRequestBranch
{
    public static Guid Resolve(bool administrator, Guid? assignedBranch)
    {
        if (assignedBranch is null || assignedBranch == Guid.Empty)
            throw new PermissionRequestRejectedException("El solicitante debe tener una sucursal asignada.");
        if (administrator) return Branch.AllId;
        if (assignedBranch == Branch.AllId)
            throw new PermissionRequestRejectedException("El Estándar debe tener una sucursal física.");
        return assignedBranch.Value;
    }
}
