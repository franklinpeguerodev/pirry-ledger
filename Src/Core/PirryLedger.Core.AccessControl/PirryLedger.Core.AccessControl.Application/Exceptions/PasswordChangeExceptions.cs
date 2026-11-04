namespace PirryLedger.Core.AccessControl.Application;

// ADR 014: los dos rechazos de un cambio de contrasena sin concesion.
//
// Son tipos distintos porque el codigo HTTP tambien lo es, igual que en ADR 012
// con la anulacion de facturas: 403 cuando la solicitud se acaba de crear y 409
// cuando ya habia una pendiente. Un solo tipo obligaria al endpoint a adivinar
// cual de los dos casos ocurrio.
//
// El texto no lleva datos del usuario (RD-08) y es lo que el usuario lee.
public sealed class PasswordChangeApprovalRequiredException : Exception
{
    public PasswordChangeApprovalRequiredException()
        : base("Se envio una solicitud para cambiar su contrasena. Un Administrador debe aprobarla.")
    {
    }
}

public sealed class PasswordChangeApprovalPendingException : Exception
{
    public PasswordChangeApprovalPendingException()
        : base("Ya hay una solicitud pendiente para cambiar su contrasena.")
    {
    }
}
