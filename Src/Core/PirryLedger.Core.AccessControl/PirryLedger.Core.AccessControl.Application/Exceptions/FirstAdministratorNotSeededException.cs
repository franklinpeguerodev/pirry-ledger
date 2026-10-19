using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
namespace PirryLedger.Core.AccessControl.Application;

// El seed no fallo por un problema del User: la cuenta ya existe o ya hay
// un Administrator. No lleva el Email ni la contrasena (RD-08), solo el motivo,
// porque el Host lo imprime en consola al arrancar.
public sealed class FirstAdministratorNotSeededException : Exception
{
    public FirstAdministratorNotSeededException(string motivo)
        : base(motivo)
    {
    }
}
