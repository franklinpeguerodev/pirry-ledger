using System.Security.Cryptography;
using System.Text;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-03: el token de sesion. 32 bytes del generador del sistema en base64url.
//
// Va aparte de OneTimeCodeGenerator a proposito: el token de activacion y el de
// recuperacion son de un solo uso y viajan una vez por correo. Este se reutiliza
// en cada peticion durante 8 horas y se genera del mismo generador, pero mezclarlos
// haria que cambiar uno afectara al otro.
//
// Base64url y no base64: el token viaja en la cabecera Authorization, y la base64
// estandar usa '+' y '/' que no son válidos ahí sin escapar.
public static class SessionTokenGenerator
{
    public const int LongitudEnBytes = 32;

    public static string Generar() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(LongitudEnBytes))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    // Lo que se guarda en la base: el SHA-256, igual que los tokens de un solo uso.
    public static string CalcularHash(string tokenEnClaro) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenEnClaro))).ToLowerInvariant();
}