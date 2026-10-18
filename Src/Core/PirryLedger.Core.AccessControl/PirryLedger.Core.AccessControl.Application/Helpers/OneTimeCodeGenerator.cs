using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using System.Security.Cryptography;
using System.Text;

namespace PirryLedger.Core.AccessControl.Application;

// Tokens y codigos de un solo uso.
//
// El valor en claro se genera aqui, se manda por Email y no vuelve a
// almacenarse: lo que se guarda es su SHA-256 (RF-CA-15, RF-CA-16, RF-CA-10).
// Leer la tabla no permite completar ningun enlace.
//
// Son 32 bytes del generador del sistema, que es criptograficamente aleatorio.
// Un token adivinable o derivado del Email seria un agujero, asi que no se usa
// ningun patron como el identificador del User.
public static class OneTimeCodeGenerator
{
    public const int LengthInBytes = 32;

    // El valor que viaja en el enlace.
    public static string Generate() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(LengthInBytes)).ToLowerInvariant();

    // Lo que se guarda en la base.
    public static string CalculateHash(string plainValue) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainValue))).ToLowerInvariant();
}
