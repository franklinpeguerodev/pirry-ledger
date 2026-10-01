using System.Security.Cryptography;
using System.Text;

namespace PirryLedger.Core.AccessControl.Application;

// Tokens y codigos de un solo uso.
//
// El valor en claro se genera aqui, se manda por correo y no vuelve a
// almacenarse: lo que se guarda es su SHA-256 (RF-CA-15, RF-CA-16, RF-CA-10).
// Leer la tabla no permite completar ningun enlace.
//
// Son 32 bytes del generador del sistema, que es criptograficamente aleatorio.
// Un token adivinable o derivado del correo seria un agujero, asi que no se usa
// ningun patron como el identificador del usuario.
public static class OneTimeCodeGenerator
{
    public const int LongitudEnBytes = 32;

    // El valor que viaja en el enlace.
    public static string Generar() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(LongitudEnBytes)).ToLowerInvariant();

    // Lo que se guarda en la base.
    public static string CalcularHash(string valorEnClaro) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valorEnClaro))).ToLowerInvariant();
}