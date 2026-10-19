using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
namespace PirryLedger.Core.AccessControl.Application;

// Un rechazo que el endpoint traduce a una respuesta con codigo de estado. No
// lleva datos del User ni traza: solo el motivo (RD-08).
//
// Es una excepcion porque el caso de uso falla y el endpoint decide como
// responder. El message ya es apto para mostrarse, no es un error interno.
public sealed class RegistrationRejectedException : Exception
{
    public RegistrationRejectedException(string motivo)
        : base(motivo)
    {
    }
}

// RF-CA-01: el Email ya esta registrado.
//
// Deliberadamente no dice "ese Email ya existe". El endpoint responde con un
// message generico para que el flujo no revele que correos estan registrados, que
// es el mismo criterio que exige RF-CA-03 y RF-CA-09. Aqui la excepcion distingue
// el caso para que el endpoint lo trate bien, pero el texto que ve el User no
// sale de este message.
public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException()
        : base("El Email ya esta registrado.")
    {
    }
}
