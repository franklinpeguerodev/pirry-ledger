namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-02 y RD-05: la contrasena nunca se guarda ni se compara en texto plano.
//
// Esta interfaz vive en Application y la implementa Infrastructure, que es la
// unica capa que referencia el paquete de Argon2id. Registro, cambio de
// contrasena, recuperacion y restablecimiento forzado pasan todos por aqui: hay
// un solo hasher en el sistema, no uno por caso de uso.
public interface IPasswordHasher
{
    // Devuelve el hash en formato versionado. Nunca la contrasena.
    string Hash(string contrasena);

    // Compara en tiempo constante. Un hash de una version o algoritmo
    // desconocido se trata como "no coincide", nunca como excepcion.
    bool Verificar(string contrasena, string hashGuardado);

    // Produce un hash con los mismos parametros reales pero de una contrasena
    // aleatoria. Sirve para verificar una contrasena cuando el correo no existe,
    // de modo que el tiempo de respuesta no revele que correos estan registrados
    // (RF-CA-03). El resultado no corresponde a ninguna contrasena conocida.
    string CrearHashSenaluelo();
}
