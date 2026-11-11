using PirryLedger.Core.Documents.Domain.Entities;

namespace PirryLedger.Core.Documents.Application;

// Une la entidad con lo que devuelve el API. Un solo sitio para que el
// contenido de la respuesta no se repita en los cuatro casos de uso.
internal static class DocumentViewMapper
{
    public static DocumentView From(Document document) =>
        new(
            document.Id,
            document.Name,
            document.ContentType,
            document.SizeBytes,
            document.UploadedByUserId,
            document.BranchId,
            document.UploadedAtUtc);
}
