using PirryLedger.Core.Permissions.Domain.Enums;

namespace PirryLedger.Core.Permissions.Domain.StateMachines;

// RF-NEG-09 y RD-04: cada transicion de la solicitud se resuelve aqui y en
// ningun otro sitio.
//
// La tabla AllowedTransitions es la unica fuente de verdad. Agregar una
// transicion nueva se hace modificando este unico componente; ningun caso de
// uso ni endpoint valida el estado por su cuenta.
//
// El codigo NO se comparte con InvoiceStateMachine: ni tipos, ni valores
// compartidos, ni helpers comunes. La pieza de negocio y la de permisos
// viven en proyectos distintos y se compilan por separado. Si la factura
// gana un estado, esta clase no se entera, y al reves.
public static class PermissionRequestStateMachine
{
    private static readonly IReadOnlySet<(PermissionRequestState From, PermissionRequestState To)> AllowedTransitions =
        new HashSet<(PermissionRequestState From, PermissionRequestState To)>
        {
            // RF-GP-03: un Administrator aprueba una solicitud pendiente.
            (PermissionRequestState.Pending, PermissionRequestState.Approved),

            // RF-GP-05: aplicada por el sistema, inmediatamente despues de aprobar.
            (PermissionRequestState.Approved, PermissionRequestState.Applied),

            // RF-GP-04: un Administrator rechaza una solicitud pendiente.
            (PermissionRequestState.Pending, PermissionRequestState.Rejected),

            (PermissionRequestState.Rejected, PermissionRequestState.Applied),
        };

    public static bool CanTransition(PermissionRequestState from, PermissionRequestState to) =>
        AllowedTransitions.Contains((from, to));

    // RF-GP-06: las transiciones prohibidas lanzan una excepcion controlada.
    // El endpoint la traduce a 400 con un mensaje en espanol, sin traza.
    public static PermissionRequestState Transition(PermissionRequestState from, PermissionRequestState to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidPermissionRequestTransitionException(from, to);
        }

        return to;
    }
}

public sealed class InvalidPermissionRequestTransitionException : Exception
{
    public InvalidPermissionRequestTransitionException(PermissionRequestState from, PermissionRequestState to)
        : base($"La solicitud de permiso no puede pasar de {from} a {to}.")
    {
    }
}