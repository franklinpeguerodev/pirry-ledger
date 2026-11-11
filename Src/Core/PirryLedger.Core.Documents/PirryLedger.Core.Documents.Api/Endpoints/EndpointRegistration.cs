using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Documents.Application;
using PirryLedger.Core.Documents.Application.Exceptions;
using PirryLedger.Core.Documents.Application.UseCases;
using PirryLedger.Core.Documents.Domain.Exceptions;

namespace PirryLedger.Core.Documents.Api;

// RF-DOC-01..04: las rutas de la pieza pertenecen a la pieza, no al Host
// (RD-01). Aqui solo se traduce HTTP a una llamada de caso de uso: ninguna
// regla de negocio vive en esta capa (RD-02) y el endpoint no compara roles:
// declara su operacion y el filtro consulta la tabla unica (RD-06).
public static class EndpointRegistration
{
    // Los bytes de sobra que el limite de la ruta deja para las cabeceras del
    // formulario multipart. No son documento: son el nombre del archivo y los
    // limites del formulario. Mismo margen que la ruta de imagenes (ADR 015).
    private const long MultipartOverheadBytes = 64 * 1024;

    // El campo del formulario. RF-DOC-01 no impone un nombre; se fija uno para
    // que el cliente sepa que mandar y el rechazo sea explicito.
    private const string FormFieldName = "file";

    public static IEndpointRouteBuilder MapDocuments(this IEndpointRouteBuilder endpoints)
    {
        // RF-DOC-01: subir un documento. Responde 201 con el registro creado.
        endpoints.MapPost("/api/documents", async (
            HttpRequest httpRequest,
            UploadDocument useCase,
            DocumentUploadOptions options,
            CancellationToken cancellationToken) =>
        {
            try
            {
                RaiseBodyLimit(httpRequest.HttpContext, options);

                var (fileName, contentType, content) = await ReadFileAsync(httpRequest, options, cancellationToken);

                var document = await useCase.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    fileName,
                    contentType,
                    content,
                    cancellationToken);

                return Results.Json(document, statusCode: StatusCodes.Status201Created);
            }
            catch (DocumentTooLargeException error)
            {
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }
            catch (DocumentRuleViolationException error)
            {
                return BadRequest(error.Message);
            }
            catch (DocumentStorageNotConfiguredException error)
            {
                return Unavailable(error.Message);
            }
            catch (DocumentStorageUnavailableException error)
            {
                return Unavailable(error.Message);
            }
        })
        .WithAccess(Operation.UploadDocument);

        // RF-DOC-02: listar. Un Standard ve los suyos; un Administrator, todos.
        // Los eliminados no aparecen.
        endpoints.MapGet("/api/documents", async (
            HttpRequest httpRequest,
            ListDocuments useCase,
            CancellationToken cancellationToken) =>
        {
            var documents = await useCase.ExecuteAsync(
                TokenBearer.Read(httpRequest) ?? string.Empty,
                cancellationToken);

            return Results.Ok(documents);
        })
        .WithAccess(Operation.ListDocuments);

        // RF-DOC-03: descargar. Solo el duenio o un Administrator. Se sirve
        // siempre como adjunto y con tipo generico, para que un HTML o un SVG
        // subido no se ejecute en el navegador de quien descarga (ADR 017).
        endpoints.MapGet("/api/documents/{id:guid}/download", async (
            Guid id,
            HttpRequest httpRequest,
            DownloadDocument useCase,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var content = await useCase.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    id,
                    cancellationToken);

                return Results.File(
                    content.Content,
                    "application/octet-stream",
                    content.Name);
            }
            catch (DocumentNotFoundException error)
            {
                return NotFound(error.Message);
            }
            catch (DocumentAccessDeniedException error)
            {
                return Forbidden(error.Message);
            }
            catch (DocumentStorageNotConfiguredException error)
            {
                return Unavailable(error.Message);
            }
            catch (DocumentStorageUnavailableException error)
            {
                return Unavailable(error.Message);
            }
        })
        .WithAccess(Operation.DownloadDocument);

        // RF-DOC-04: eliminar de forma logica. Responde 204.
        endpoints.MapDelete("/api/documents/{id:guid}", async (
            Guid id,
            HttpRequest httpRequest,
            DeleteDocument useCase,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await useCase.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    id,
                    cancellationToken);

                return Results.NoContent();
            }
            catch (DocumentNotFoundException error)
            {
                return NotFound(error.Message);
            }
            catch (DocumentAccessDeniedException error)
            {
                return Forbidden(error.Message);
            }
        })
        .WithAccess(Operation.DeleteDocument);

        return endpoints;
    }

    // La ruta de subida admite mas que el limite general de la aplicacion
    // (1 MiB): un documento de 5 MiB no cabe. Se levanta solo en esta ruta y
    // solo hasta el maximo configurado mas el margen del formulario.
    private static void RaiseBodyLimit(HttpContext context, DocumentUploadOptions options)
    {
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (feature is null || feature.IsReadOnly)
        {
            return;
        }

        feature.MaxRequestBodySize = options.EffectiveMaxBytes + MultipartOverheadBytes;
    }

    // Lee el archivo del formulario sin cargar de mas: si el archivo supera el
    // maximo, se corta la lectura y se rechaza antes de tenerlo entero en
    // memoria (RD-07).
    private static async Task<(string FileName, string? ContentType, byte[] Content)> ReadFileAsync(
        HttpRequest request,
        DocumentUploadOptions options,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            throw new DocumentRuleViolationException("La petición debe ser un formulario con el archivo.");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        if (form.Files.Count != 1)
        {
            throw new DocumentRuleViolationException("La petición debe llevar exactamente un archivo.");
        }

        var file = form.Files[0];

        if (!string.Equals(file.Name, FormFieldName, StringComparison.Ordinal))
        {
            throw new DocumentRuleViolationException("El archivo del formulario se llama 'file'.");
        }

        var maxBytes = options.EffectiveMaxBytes;

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                break;
            }

            total += read;

            if (total > maxBytes)
            {
                throw new DocumentTooLargeException(maxBytes);
            }

            memory.Write(buffer, 0, read);
        }

        return (OnlyFileName(file.FileName), file.ContentType, memory.ToArray());
    }

    // El cliente puede mandar una ruta ("C:\fotos\factura.pdf" o
    // "../../etc/passwd"). Solo se guarda el nombre: la clave del objeto la
    // genera el servidor y el nombre es un dato para mostrar (ADR 017).
    private static string OnlyFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "documento";
        }

        var lastSeparator = fileName.LastIndexOfAny(['/', '\\']);

        var name = lastSeparator >= 0 ? fileName[(lastSeparator + 1)..] : fileName;

        return string.IsNullOrWhiteSpace(name) ? "documento" : name;
    }

    private static IResult BadRequest(string message) =>
        Results.Json(new ErrorResponse(message), statusCode: StatusCodes.Status400BadRequest);

    private static IResult NotFound(string message) =>
        Results.Json(new ErrorResponse(message), statusCode: StatusCodes.Status404NotFound);

    private static IResult Forbidden(string message) =>
        Results.Json(new ErrorResponse(message), statusCode: StatusCodes.Status403Forbidden);

    private static IResult Unavailable(string message) =>
        Results.Json(new ErrorResponse(message), statusCode: StatusCodes.Status503ServiceUnavailable);
}
