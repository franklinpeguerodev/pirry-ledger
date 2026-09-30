namespace PirryLedger.Core.AccessControl.Domain;

// RF-CA-04: exactamente dos roles y cada usuario tiene uno.
//
// El nombre sale de entities.md ("Rol: Administrador o Estandar"). Se declara
// aqui, en un solo sitio, para que ningun caso de uso tenga su propia idea de
// que es un Administrador (RF-CA-05 exige un punto legible con la exigencia).
public enum Rol
{
    // Puede operar el negocio y administrar usuarios.
    Administrador = 0,

    // Solo opera el negocio. No toca la administracion de usuarios.
    Estandar = 1,
}