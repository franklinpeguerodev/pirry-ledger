using PirryLedger.Core.Documents.Domain.Exceptions;
using PirryLedger.Core.Documents.Domain.Rules;

namespace PirryLedger.Core.Documents.Domain.Entities;

// RF-DOC-01: el registro de un documento. Guarda quien lo subio y a que
// sucursal pertenece, ademas del nombre, el tipo, el tamano y la fecha que pide
// el enunciado.
//
// La clave del almacenamiento la genera el servidor y llega ya construida. El
// nombre original se guarda como dato para mostrarlo y para la descarga; nunca
// se usa como ruta ni como clave (ADR 017).
//
// No hay clave foranea a la tabla de usuarios: cada modulo es duenio de sus
// tablas y el id es un valor. La existencia del usuario la garantiza la sesion
// que ya valido el filtro de acceso.
public sealed class Document
{
    private Document()
    {
    }

    private Document(
        Guid id,
        string name,
        string contentType,
        long sizeBytes,
        Guid uploadedByUserId,
        Guid branchId,
        string storageKey,
        DateTime uploadedAtUtc)
    {
        Id = id;
        Name = name;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        UploadedByUserId = uploadedByUserId;
        BranchId = branchId;
        StorageKey = storageKey;
        UploadedAtUtc = uploadedAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public Guid BranchId { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public DateTime UploadedAtUtc { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public Guid? DeletedByUserId { get; private set; }

    public bool IsDeleted => DeletedAtUtc is not null;

    // maxSizeBytes llega de la configuracion (PIRRY_LEDGER_DOCUMENT_MAX_BYTES),
    // con el valor por defecto de DocumentRules. La regla vive aqui: el limite
    // es el dato que el operador elige.
    public static Document Create(
        string name,
        string? contentType,
        long sizeBytes,
        Guid uploadedByUserId,
        Guid branchId,
        string storageKey,
        DateTime uploadedAtUtc,
        long maxSizeBytes)
    {
        if (uploadedByUserId == Guid.Empty)
        {
            throw new DocumentRuleViolationException("El documento necesita el usuario que lo subió.");
        }

        if (branchId == Guid.Empty)
        {
            throw new DocumentRuleViolationException("El documento necesita una sucursal.");
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new DocumentRuleViolationException("El documento necesita una clave de almacenamiento.");
        }

        if (uploadedAtUtc == default)
        {
            throw new DocumentRuleViolationException("El documento necesita la fecha de la subida.");
        }

        var normalizedName = ValidateName(name);

        if (sizeBytes <= 0)
        {
            throw new EmptyDocumentException();
        }

        if (sizeBytes > maxSizeBytes)
        {
            throw new DocumentTooLargeException(maxSizeBytes);
        }

        return new Document(
            Guid.NewGuid(),
            normalizedName,
            DocumentRules.NormalizeContentType(contentType),
            sizeBytes,
            uploadedByUserId,
            branchId,
            storageKey.Trim(),
            uploadedAtUtc);
    }

    // RF-DOC-04: eliminacion logica. El registro sigue existiendo y el objeto
    // sigue en el almacenamiento; lo unico que cambia es que deja de aparecer
    // en el listado.
    public void Delete(Guid deletedByUserId, DateTime deletedAtUtc)
    {
        if (IsDeleted)
        {
            throw new DocumentAlreadyDeletedException();
        }

        if (deletedByUserId == Guid.Empty)
        {
            throw new DocumentRuleViolationException("La eliminación necesita el usuario que la hizo.");
        }

        if (deletedAtUtc == default)
        {
            throw new DocumentRuleViolationException("La eliminación necesita su fecha.");
        }

        DeletedByUserId = deletedByUserId;
        DeletedAtUtc = deletedAtUtc;
    }

    // RF-DOC-03: la propiedad se compara por identidad, como en RF-GP-07.
    public bool IsOwnedBy(Guid userId) => UploadedByUserId == userId;

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DocumentRuleViolationException("El nombre del documento es obligatorio.");
        }

        var normalized = DocumentRules.NormalizeName(name);

        if (normalized.Length > DocumentRules.MaxNameLength)
        {
            throw new DocumentRuleViolationException(
                $"El nombre del documento no puede pasar de {DocumentRules.MaxNameLength} caracteres.");
        }

        return normalized;
    }
}
