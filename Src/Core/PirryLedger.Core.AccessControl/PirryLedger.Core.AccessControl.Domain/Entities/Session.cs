using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Domain.Entities;

// RF-CA-03 y RF-CA-18. Decision documentada en docs/adr/001-session-credential.md.
//
// La credencial es opaca: 32 bytes del generador del sistema en base64url. En la
// base solo queda su SHA-256, asi que leer la tabla no permite autenticarse.
//
// El token NUNCA se guarda en claro, igual que el de activacion (RF-CA-15). Si un
// token se guarda en claro, un SELECT bastaria para suplantar a cualquier
// empleado, y en una tabla que se lee para diagnosticar.
//
// Caducidad ABSOLUTA, no deslizante: la Session no se renueva aunque el User
// este Active. Un token robado tiene 8 horas de vida maxima en lugar de valer
// mientras haya trafico. El ADR explica el razonamiento.
public sealed class Session
{
    private Session()
    {
    }

    private Session(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        int credentialVersion)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ClosedAtUtc = null;
        CredentialVersion = credentialVersion;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    // SHA-256 del token, en el mismo formato que el de activacion.
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime IssuedAtUtc { get; private set; }

    // Vencimiento absoluto. No se toca al usarla.
    public DateTime ExpiresAtUtc { get; private set; }

    // RF-CA-18: el instante del cierre. Nulo mientras la Session esta abierta.
    public DateTime? ClosedAtUtc { get; private set; }

    // Decision de Franklin. Copia del valor que tenia User.CredentialVersion al
    // emitirse. Si el User sube el suyo (cambio de contrasena, restablecimiento
    // o desactivacion), deja de coincidir y todas sus sesiones caen sin tocar la
    // tabla Session, que es lo que hace RF-CA-12 y RF-CA-20 de inmediato.
    public int CredentialVersion { get; private set; }

    public bool IsClosed => ClosedAtUtc is not null;

    public bool HasExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;

    // Las reglas de vigencia viven en la entidad y en un solo sitio (RD-04).
    // Devuelve por que se rechaza, porque el endpoint necesita distinguir un 401
    // por credencial caducada de un 401 por credencial desconocida, y decidirlo
    // aqui evita repetir las cinco comparaciones en cada punto de entrada.
    public bool IsValid(DateTime utcNow, User user) =>
        !IsClosed
        && !HasExpired(utcNow)
        && CredentialVersion == user.CredentialVersion
        && user.Active;

    // Decision propia. El token se genera fuera y entra ya hasheado, igual que el
    // de activacion: Domain no ve ningun paquete de criptografia.
    public static Session Create(
        Guid userId,
        string tokenHash,
        DateTime utcNow,
        TimeSpan validityDuration,
        int userCredentialVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (validityDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(validityDuration), "La Session necesita una duracion de validez positiva.");
        }

        if (userCredentialVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(userCredentialVersion), "La version de credencial no puede ser negativa.");
        }

        return new Session(
            Guid.NewGuid(),
            userId,
            tokenHash,
            utcNow,
            utcNow.Add(validityDuration),
            userCredentialVersion);
    }

    // RF-CA-18: Close una Session. Close dos veces la misma es un no-op, no un
    // error: un segundo cierre llega por la carrera de dos pestanas cerrando a la
    // vez, y eso no es un fallo del User.
    public void Close(DateTime utcNow)
    {
        if (IsClosed)
        {
            return;
        }

        ClosedAtUtc = utcNow;
    }
}
