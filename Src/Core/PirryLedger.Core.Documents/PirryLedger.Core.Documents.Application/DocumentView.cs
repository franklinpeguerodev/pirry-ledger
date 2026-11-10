namespace PirryLedger.Core.Documents.Application;

// RF-DOC-01: lo que devuelve el Manejador de documentos. Es el registro que el
// enunciado pide poder consultar con todos sus datos: nombre, tipo, tamano,
// fecha y quien lo subio, mas la sucursal.
public sealed record DocumentView(
    Guid Id,
    string Name,
    string ContentType,
    long SizeBytes,
    Guid UploadedByUserId,
    Guid BranchId,
    DateTime UploadedAtUtc);

// RF-DOC-03: los bytes y lo necesario para servirlos como adjunto.
public sealed record DocumentContent(byte[] Content, string Name, string ContentType);
