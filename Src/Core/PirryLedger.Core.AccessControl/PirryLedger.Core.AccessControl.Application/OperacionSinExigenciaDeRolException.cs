namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-05: una operacion existe en el enum pero nadie declaro que rol la ejecuta.
//
// Es un error de programacion, no un error del usuario que la invoca: por eso es
// una excepcion distinta de las de rechazo, y el endpoint NO la traduce a 403.
// Si un endpoint llegara aqui, lo correcto es que se caiga la peticion con un
// 500, porque una operacion sin proteccion no se debe responder.
//
// El mensaje lleva el nombre de la operacion, que es codigo del sistema y no un
// dato del usuario (RD-08).
public sealed class OperacionSinExigenciaDeRolException : Exception
{
    public OperacionSinExigenciaDeRolException(Operacion operacion)
        : base($"La operacion {operacion} no declara que rol puede ejecutarla.")
    {
    }
}

// Un rechazo de administracion que el endpoint traduce a 409 o 400.
//
// Estas NO son el 403 de RF-CA-06: ese sale de Autenticar.EjecutarConRolAsync
// cuando el rol no alcanza. Estas son para cuando el rol es correcto pero la
// operacion no se puede hacer: desactivarse a si mismo, quedarse sin
// Administradores, cambiar un rol que no existe.
//
// Los mensajes no llevan el correo ni el nombre del usuario afectado (RD-08),
// porque el rol ya sabe quien es y no lo necesita.
public sealed class OperacionDeAdministracionRechazadaException : Exception
{
    public OperacionDeAdministracionRechazadaException(string motivo)
        : base(motivo)
    {
    }
}