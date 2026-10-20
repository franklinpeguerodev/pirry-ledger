namespace PirryLedger.Core.AccessControl.Application;

// RF-GP-05: cuando una operation exige AccessLevel.Permission y el usuario
// no tiene el permiso concedido, el filtro devuelve 403. La excepcion vive en
// AccessControl porque es el modulo que conoce el codigo de 403 (RF-CA-06);
// la pieza de Permissions no tiene su propio codigo de error de autorizacion,
// solo el contrato que el filtro consulta.
public sealed class InsufficientPermissionException : Exception
{
    public InsufficientPermissionException() : base("No tiene el permiso necesario para esta operacion.")
    {
    }
}