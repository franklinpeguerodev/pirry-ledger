namespace PirryLedger.Business.Application.Exceptions;

// RF-NEG-06 + ADR 012: la anulacion de una factura Paid o InDebt no tenia una
// concesion. La operacion pidio el permiso en nombre del usuario (o encontro
// una solicitud Pending) y rechaza la anulacion. El endpoint decide el status:
// 403 si la solicitud se acaba de crear, 409 si ya estaba pendiente.
public sealed class CancellationPermissionRequiredException : Exception
{
    public CancellationPermissionRequiredException(bool alreadyPending)
        : base(alreadyPending
            ? "Ya se envió una solicitud de permiso para anular esta factura; espera la aprobación del Administrador."
            : "Se envió una solicitud de permiso para anular esta factura. Un Administrador debe aprobarla.")
    {
        AlreadyPending = alreadyPending;
    }

    public bool AlreadyPending { get; }
}
