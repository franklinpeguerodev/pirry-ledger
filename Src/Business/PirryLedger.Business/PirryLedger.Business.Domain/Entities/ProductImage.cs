using System.Security.Cryptography;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Domain.Rules;

namespace PirryLedger.Business.Domain.Entities;

// La imagen de un producto: un objeto privado en el bucket, con su clave y su
// version en la base de datos (ADR 015).
//
// No es un atributo del producto: es una fila con su propio ciclo de vida. Cada
// cambio en el conjunto de imagenes inserta una fila, y una fila existente solo
// se modifica para registrar que salio del conjunto. El historial es el propio
// dato, y por eso no hace falta una tabla de auditoria aparte.
//
// La clave del objeto lleva el identificador de la imagen, no su contenido:
// products/{productId}/{id}.{ext}. Con el sha256 en la clave, subir dos veces el
// mismo contenido usaria la misma clave y un borrado pendiente podria borrar
// un objeto recien subido.
public sealed class ProductImage
{
    public const int MaximumKeyLength = 255;
    public const int MaximumVersionLength = 64;
    public const int MaximumContentTypeLength = 32;

    private ProductImage()
    {
    }

    private ProductImage(
        Guid id,
        Guid productId,
        string imageKey,
        string version,
        string contentType,
        int sizeBytes,
        DateTime createdAtUtc)
    {
        Id = id;
        ProductId = productId;
        ImageKey = imageKey;
        Version = version;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string ImageKey { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    // La revision global en la que la imagen entro al conjunto. Cero quiere
    // decir todavia no esta dentro: el repositorio la asigna al guardar.
    public long RevisionAdded { get; private set; }

    // La revision global en la que salio del conjunto. Vacia mientras siga
    // dentro del conjunto.
    public long? RevisionRemoved { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? RemovedAtUtc { get; private set; }

    public bool IsLive => RevisionRemoved is null;

    // Crea la imagen a partir del contenido que se va a guardar. El contenido
    // ya viene limpio de metadatos: el sha256 es el de los bytes que quedan en
    // el bucket, y es el que el cliente usa para verificar la descarga.
    public static ProductImage Create(
        Guid productId,
        byte[] content,
        ImageFormat format,
        DateTime createdAtUtc)
    {
        if (productId == Guid.Empty)
        {
            throw new InvoiceRuleViolationException("La imagen necesita un producto.");
        }

        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            throw new InvoiceRuleViolationException("La imagen esta vacia.");
        }

        var id = Guid.NewGuid();

        return new ProductImage(
            id,
            productId,
            BuildKey(productId, id, ImageFormatDetector.ExtensionOf(format)),
            ComputeVersion(content),
            ImageFormatDetector.ContentTypeOf(format),
            content.Length,
            createdAtUtc);
    }

    // La imagen entra al conjunto. Sirve para la primera vez y para cuando
    // vuelve: el producto reactivado recupera la misma imagen, con la misma
    // clave y la misma version, sin volver a subir el archivo.
    public void Enter(long revision)
    {
        if (revision <= 0)
        {
            throw new InvoiceRuleViolationException("La revision de la imagen debe ser mayor que cero.");
        }

        if (RevisionAdded > 0 && RevisionRemoved is null)
        {
            throw new InvoiceRuleViolationException("La imagen ya esta vigente.");
        }

        RevisionAdded = revision;
        RevisionRemoved = null;
        RemovedAtUtc = null;
    }

    // La imagen sale del conjunto. El archivo del bucket no se borra aqui: si
    // el producto se desactiva, la reactivacion lo necesita.
    public void Retire(long revision, DateTime removedAtUtc)
    {
        if (revision <= 0)
        {
            throw new InvoiceRuleViolationException("La revision de la imagen debe ser mayor que cero.");
        }

        if (RevisionRemoved is not null)
        {
            throw new InvoiceRuleViolationException("La imagen ya no esta vigente.");
        }

        RevisionRemoved = revision;
        RemovedAtUtc = removedAtUtc;
    }

    // El sha256 del contenido, en hexadecimal minusculas. Es la version que ve
    // el cliente: la calcula el servidor y el cliente nunca la elige.
    public static string ComputeVersion(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static string BuildKey(Guid productId, Guid id, string extension) =>
        $"products/{productId:D}/{id:D}.{extension}";
}
