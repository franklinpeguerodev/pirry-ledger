using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Domain.Entities;

// RF-CA-15 y RF-CA-16: el enlace de activacion lleva un token de un solo uso con
// fecha de vencimiento.
//
// entities.md no define esta entidad: la lista de "decisiones de diseno" incluye
// expresamente el token de activacion. Se guarda aparte y no junto al codigo de
// recuperacion (que si figura en entities.md como RecoveryCode), y esta
// separacion es una decision de Franklin.
//
// El token NUNCA se guarda en claro. Lo que se guarda es su SHA-256: leer la
// tabla no permite completar ningun enlace de activacion.
public sealed class ActivationToken
{
    private ActivationToken()
    {
    }

    private ActivationToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        UsedAtUtc = null;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    // SHA-256 del token. El token en claro solo existe en el Email.
    public string TokenHash { get; private set; } = string.Empty;

    // Atributos de entities.md para los codigos de un solo uso: fecha de emision
    // y fecha de vencimiento. El "usado o no usado" es UsedAtUtc a null.
    public DateTime IssuedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? UsedAtUtc { get; private set; }

    public bool IsUsed => UsedAtUtc is not null;

    public bool HasExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;

    // Un solo uso: la regla de transiciones vive en la entidad (RD-04).
    // Marcar usado un token ya usado es un error, no un no-op silencioso.
    public void MarkUsed(DateTime utcNow)
    {
        if (IsUsed)
        {
            throw new InvalidOperationException("El token de activacion ya se uso.");
        }

        UsedAtUtc = utcNow;
    }

    // Decision propia: el token generado en el servidor. El valor en claro se
    // devuelve para mandar el Email y no se vuelve a guardar en ningun sitio.
    public static ActivationToken Create(
        Guid userId,
        string tokenHash,
        DateTime utcNow,
        TimeSpan validityDuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (validityDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(validityDuration), "El token necesita una duracion de validez positiva.");
        }

        return new ActivationToken(
            Guid.NewGuid(),
            userId,
            tokenHash,
            utcNow,
            utcNow.Add(validityDuration));
    }
}
