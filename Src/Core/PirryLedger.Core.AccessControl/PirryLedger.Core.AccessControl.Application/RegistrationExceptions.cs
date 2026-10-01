namespace PirryLedger.Core.AccessControl.Application;

// Un rechazo que el endpoint traduce a una respuesta con codigo de estado. No
// lleva datos del usuario ni traza: solo el motivo (RD-08).
//
// Es una excepcion porque el caso de uso falla y el endpoint decide como
// responder. El mensaje ya es apto para mostrarse, no es un error interno.
public sealed class RegistrationRejectedException : Exception
{
    public RegistrationRejectedException(string motivo)
        : base(motivo)
    {
    }
}

// RF-CA-01: el correo ya esta registrado.
//
// Deliberadamente no dice "ese correo ya existe". El endpoint responde con un
// mensaje generico para que el flujo no revele que correos estan registrados, que
// es el mismo criterio que exige RF-CA-03 y RF-CA-09. Aqui la excepcion distingue
// el caso para que el endpoint lo trate bien, pero el texto que ve el usuario no
// sale de este mensaje.
public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException()
        : base("El correo ya esta registrado.")
    {
    }
}