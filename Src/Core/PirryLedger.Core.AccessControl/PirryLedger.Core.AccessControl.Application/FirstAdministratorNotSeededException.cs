namespace PirryLedger.Core.AccessControl.Application;

// El seed no fallo por un problema del usuario: la cuenta ya existe o ya hay
// un Administrador. No lleva el correo ni la contrasena (RD-08), solo el motivo,
// porque el Host lo imprime en consola al arrancar.
public sealed class FirstAdministratorNotSeededException : Exception
{
    public FirstAdministratorNotSeededException(string motivo)
        : base(motivo)
    {
    }
}