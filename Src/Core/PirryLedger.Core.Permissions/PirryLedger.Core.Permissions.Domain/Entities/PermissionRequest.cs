using PirryLedger.Core.Permissions.Domain.Enums;
using PirryLedger.Core.Permissions.Domain.StateMachines;

namespace PirryLedger.Core.Permissions.Domain.Entities;

// RF-GP-01..07: entidad central de la pieza. Su estado lo decide
// PermissionRequestStateMachine y nadie mas (RD-04). Los metodos de mutacion
// estan aqui para que la regla de transicion viva en un solo componente.
//
// Atributos minimos por entities.md (SolicitudPermiso): solicitante,
// permiso solicitado, estado, aprobador, fecha de creacion, fecha de
// resolucion, motivo del rechazo. Se anaden approved_at_utc y applied_at_utc
// porque RF-GP-03 y RF-GP-05 los diferencian: aprobar y aplicar son dos
// instantes distintos aunque ocurran en la misma operacion.
public sealed class PermissionRequest
{
    private PermissionRequest()
    {
    }

    private PermissionRequest(
        Guid id,
        Guid requesterId,
        string requestedPermission,
        DateTime createdAtUtc)
    {
        Id = id;
        RequesterId = requesterId;
        RequestedPermission = requestedPermission;
        State = PermissionRequestState.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid RequesterId { get; private set; }

    public string RequestedPermission { get; private set; } = string.Empty;

    // PermissionRequestState lo decide la maquina de estados. Asignar el
    // campo directamente es un fallo de programacion: la unica forma valida
    // de cambiar el estado es llamar a Approve, MarkApplied o Reject.
    public PermissionRequestState State { get; private set; }

    public Guid? ApproverId { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ApprovedAtUtc { get; private set; }

    public DateTime? AppliedAtUtc { get; private set; }

    public DateTime? RejectedAtUtc { get; private set; }

    // RF-GP-01: la solicitud nace en Pending. La unica fabrica publica.
    public static PermissionRequest Create(
        Guid requesterId,
        string requestedPermission,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedPermission);

        return new PermissionRequest(
            Guid.NewGuid(),
            requesterId,
            requestedPermission,
            utcNow);
    }

    // RF-GP-03: aprobar una solicitud Pending pasa por la maquina de estados
    // y registra aprobador e instante. Quien llama (caso de uso) ya verifico
    // que el resolutor no es el solicitante y que tiene rol Administrator.
    public void Approve(Guid approverId, DateTime utcNow)
    {
        State = PermissionRequestStateMachine.Transition(State, PermissionRequestState.Approved);
        ApproverId = approverId;
        ApprovedAtUtc = utcNow;
    }

    // RF-GP-05: aplicar el permiso, en la misma operacion que la aprobacion.
    // Lo llama el caso de uso ApplyPermissionRequest, sin intermediarios.
    public void MarkApplied(DateTime utcNow)
    {
        State = PermissionRequestStateMachine.Transition(State, PermissionRequestState.Applied);
        AppliedAtUtc = utcNow;
    }

    // RF-GP-04: rechazar exige motivo escrito no vacio. Si llega vacio, la
    // excepcion del caso de uso lo rechaza antes de llegar aqui.
    public void Reject(Guid approverId, DateTime utcNow, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        State = PermissionRequestStateMachine.Transition(State, PermissionRequestState.Rejected);
        ApproverId = approverId;
        RejectedAtUtc = utcNow;
    }
}