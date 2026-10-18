namespace PirryLedger.Core.AccessControl.Domain.Enums;

// RF-CA-04: exactamente dos roles y cada User tiene uno.
//
// El Name sale de entities.md ("Role: Administrator o Standard"). Se declara
// aqui, en un solo sitio, para que ningun caso de uso tenga su propia idea de
// que es un Administrator (RF-CA-05 exige un punto legible con la Requirement).
public enum Role
{
    // Puede operar el negocio y administrar usuarios.
    Administrator = 0,

    // Solo opera el negocio. No toca la administracion de usuarios.
    Standard = 1,
}
