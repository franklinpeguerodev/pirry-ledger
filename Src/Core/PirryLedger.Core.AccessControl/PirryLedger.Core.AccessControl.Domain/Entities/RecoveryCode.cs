using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Domain.Entities;

// RF-CA-09, RF-CA-10 y RF-CA-11: el codigo de recuperacion.
//
// Entidad exigida por docs/requirements/entities.md, con sus atributos minimos:
// User, codigo, fecha de emision, fecha de vencimiento y usado o no usado.
//
// El codigo NUNCA se guarda en claro. Se guarda su SHA-256, igual que el token de
// activacion. Esta fase solo crea la entidad y su transicion de un solo uso; el
// flujo completo de recuperacion llega en la fase next.
public sealed class RecoveryCode
{
    private RecoveryCode()
    {
    }

    private RecoveryCode(
        Guid id,
        Guid userId,
        string codeHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc)
    {
        Id = id;
        UserId = userId;
        CodeHash = codeHash;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        UsedAtUtc = null;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    // SHA-256 del codigo. El codigo en claro solo existe en el Email.
    public string CodeHash { get; private set; } = string.Empty;

    public DateTime IssuedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? UsedAtUtc { get; private set; }

    public bool IsUsed => UsedAtUtc is not null;

    public bool HasExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;

    // Un solo uso (RF-CA-10): las transiciones viven en la entidad (RD-04).
    public void MarkUsed(DateTime utcNow)
    {
        if (IsUsed)
        {
            throw new InvalidOperationException("El codigo de recuperacion ya se uso.");
        }

        UsedAtUtc = utcNow;
    }

    public static RecoveryCode Create(
        Guid userId,
        string codeHash,
        DateTime utcNow,
        TimeSpan validityDuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);

        if (validityDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(validityDuration), "El codigo necesita una duracion de validez positiva.");
        }

        return new RecoveryCode(
            Guid.NewGuid(),
            userId,
            codeHash,
            utcNow,
            utcNow.Add(validityDuration));
    }
}
