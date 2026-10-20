namespace PirryLedger.Core.Permissions.Domain.Enums;

// RF-GP-*: estado de la solicitud de permiso.
//
// Solo se permiten las transiciones declaradas en
// PermissionRequestStateMachine, igual que InvoiceStateMachine hace con
// InvoiceState (RF-NEG-09). Esta pieza y la de negocio comparten la forma
// (maquina de estados en un solo componente, RD-04) pero ni tipos ni valores:
// cambiar los estados de la factura no obliga a tocar los de la solicitud de
// permiso, ni al reves.
public enum PermissionRequestState
{
    // RF-GP-01: nace al crear la solicitud. Es el unico estado en el que se
    // puede pasar a Approved o Rejected.
    Pending,

    // RF-GP-03: aprobada por un Administrator. Quedan registrados aprobador
    // y fecha.
    Approved,

    // RF-GP-05: aplicada por el sistema, en la misma operacion que la
    // aprueba. El solicitante ya puede ejecutar la accion del permiso.
    Applied,

    // RF-GP-04: rechazada por un Administrator. Quedan registrados
    // resolutor, fecha y motivo escrito.
    Rejected,
}