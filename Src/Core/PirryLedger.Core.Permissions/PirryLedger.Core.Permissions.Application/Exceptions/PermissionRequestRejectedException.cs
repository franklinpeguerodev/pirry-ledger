namespace PirryLedger.Core.Permissions.Application.Exceptions;

// Excepciones de la pieza de Permissions. Cada una se traduce a 400 en el
// endpoint (RD-07, RD-08) con un mensaje en espanol sin trazas.

// RF-GP-01: el permiso solicitado no existe en el catalogo o el cuerpo
// de la peticion es invalido (campo vacio, etc.).
public sealed class PermissionRequestRejectedException : Exception
{
    public PermissionRequestRejectedException(string message) : base(message)
    {
    }
}

// RF-GP-06: se intento una transicion no permitida por la maquina de estados.
public sealed class InvalidPermissionRequestTransitionApplicationException : Exception
{
    public InvalidPermissionRequestTransitionApplicationException(string message) : base(message)
    {
    }
}

// RF-GP-07: nadie resuelve su propia solicitud. Se lanza desde el caso de
// uso cuando caller.Id == request.RequesterId, comparando identidades
// (no roles), tal como exige la regla.
public sealed class SelfResolutionRejectedException : Exception
{
    public SelfResolutionRejectedException()
        : base("No puede resolver su propia solicitud de permiso.")
    {
    }
}