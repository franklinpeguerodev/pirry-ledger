using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Api.Models;

// Respuestas HTTP del negocio. Se arman con records explicitos y no se
// serializa el agregado tal cual: asi el contrato HTTP no depende de las
// propiedades internas de la entidad ni de su coleccion escribible.
public sealed record InvoiceItemResponse(
    Guid Id,
    Guid? ProductId,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal)
{
    public static InvoiceItemResponse From(InvoiceItem item) =>
        new(item.Id, item.ProductId, item.Name, item.UnitPrice, item.Quantity, item.LineTotal);
}

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    bool IsActive,
    string? ImageKey,
    string? ImageVersion, Guid? SubcategoryId = null)
{
    public static ProductResponse From(Product product, ProductImage? image) =>
        new(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.IsActive,
            image?.ImageKey,
            image?.Version, product.SubcategoryId);

    public static ProductResponse From(ProductCatalogItem item) =>
        new(
            item.Id,
            item.Name,
            item.Description,
            item.Price,
            item.IsActive,
            item.ImageKey,
            item.ImageVersion, item.SubcategoryId);
}

// La imagen vigente de un producto. La clave es la que hay que pedir al
// bucket; la version es el sha256 de lo guardado, para que el cliente
// compruebe la descarga (ADR 015).
public sealed record ProductImageResponse(
    Guid ProductId,
    string ImageKey,
    string Version,
    string ContentType,
    int SizeBytes,
    long Revision)
{
    public static ProductImageResponse From(ProductImage image) =>
        new(
            image.ProductId,
            image.ImageKey,
            image.Version,
            image.ContentType,
            image.SizeBytes,
            image.RevisionAdded);
}

// La sincronizacion de las imagenes: que tiene que descargar el cliente y
// que tiene que borrar de su cache (ADR 015).
public sealed record ProductImageSyncResponse(
    bool FullSync,
    long Revision,
    IReadOnlyList<ProductImageUpsertResponse> Upserts,
    IReadOnlyList<string> Removed)
{
    public static ProductImageSyncResponse From(ProductImageSyncResult result) =>
        new(
            result.FullSync,
            result.Revision,
            [.. result.Upserts.Select(ProductImageUpsertResponse.From)],
            result.Removed);
}

// Una imagen con su firma de descarga. La firma caduca en ExpiresAtUtc.
public sealed record ProductImageUpsertResponse(
    Guid ProductId,
    string ImageKey,
    string Version,
    string ContentType,
    int SizeBytes,
    string SignedUrl,
    DateTime ExpiresAtUtc)
{
    public static ProductImageUpsertResponse From(ProductImageUpsert upsert) =>
        new(
            upsert.ProductId,
            upsert.ImageKey,
            upsert.Version,
            upsert.ContentType,
            upsert.SizeBytes,
            upsert.SignedUrl,
            upsert.ExpiresAtUtc);
}

public sealed record InvoicePaymentResponse(
    Guid Id,
    string Kind,
    string Method,
    string Status,
    decimal Amount,
    decimal? ReceivedAmount,
    decimal? ChangeAmount,
    DateTime RegisteredAtUtc,
    Guid RegisteredByUserId)
{
    public static InvoicePaymentResponse From(InvoicePayment payment) =>
        new(
            payment.Id,
            payment.Kind.ToString(),
            payment.Method.ToString(),
            payment.Status.ToString(),
            payment.Amount,
            payment.ReceivedAmount,
            payment.ChangeAmount,
            payment.RegisteredAtUtc,
            payment.RegisteredByUserId);
}

public sealed record InvoiceResponse(
    Guid Id,
    string State,
    bool IsDelivery,
    Guid BranchId,
    string BranchName,
    string BranchAddress,
    DateTime IssuedAtUtc,
    decimal Total,
    decimal PaidAmount,
    decimal Balance,
    string? DebtorName,
    IReadOnlyList<InvoiceItemResponse> Items,
    IReadOnlyList<InvoicePaymentResponse> Payments)
{
    public static InvoiceResponse From(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.State.ToString(),
            invoice.IsDelivery,
            invoice.BranchId,
            invoice.BranchName,
            invoice.BranchAddress,
            invoice.IssuedAtUtc,
            invoice.Total,
            invoice.PaidAmount,
            invoice.Total - invoice.PaidAmount,
            invoice.DebtorName,
            invoice.Items.Select(InvoiceItemResponse.From).ToList(),
            invoice.Payments.Select(InvoicePaymentResponse.From).ToList());
}

public sealed record InvoiceEditResponse(
    Guid Id,
    Guid InvoiceId,
    DateTime EditedAtUtc,
    Guid EditedByUserId,
    int ItemCount)
{
    public static InvoiceEditResponse From(InvoiceEdit edit) =>
        new(edit.Id, edit.InvoiceId, edit.EditedAtUtc, edit.EditedByUserId, edit.ItemCount);
}
