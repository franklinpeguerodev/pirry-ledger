using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-05: una operation existe en el enum pero nadie declaro que role la ejecuta.
//
// Es un error de programacion, no un error del User que la invoca: por eso es
// una excepcion distinta de las de rechazo, y el endpoint NO la traduce a 403.
// Si un endpoint llegara aqui, lo correcto es que se caiga la peticion con un
// 500, porque una operation sin proteccion no se debe responder.
//
// El message lleva el Name de la operation, que es codigo del sistema y no un
// dato del User (RD-08).
public sealed class OperationWithoutRoleRequirementException : Exception
{
    public OperationWithoutRoleRequirementException(Operation operation)
        : base($"La operation {operation} no declara que role puede ejecutarla.")
    {
    }
}

// Un rechazo de administracion que el endpoint traduce a 409 o 400.
//
// Estas NO son el 403 de RF-CA-06: ese sale de Authenticate.ExecuteWithRoleAsync
// cuando el role no alcanza. Estas son para cuando el role es correcto pero la
// Operation no se puede hacer: desactivarse a si mismo, quedarse sin
// Administradores, cambiar un role que no existe.
//
// Los mensajes no llevan el Email ni el Name del User afectado (RD-08),
// porque el role ya sabe quien es y no lo necesita.
public sealed class AdministrationOperationRejectedException : Exception
{
    public AdministrationOperationRejectedException(string reason)
        : base(reason)
    {
    }
}
