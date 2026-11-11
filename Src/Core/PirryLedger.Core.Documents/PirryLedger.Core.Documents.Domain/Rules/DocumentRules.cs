namespace PirryLedger.Core.Documents.Domain.Rules;

// RF-DOC-01: las reglas del registro de un documento viven aqui y no en el
// endpoint. El limite de tamano es un dato configurable, pero su valor por
// defecto y el resto de las cotas son reglas de la pieza.
public static class DocumentRules
{
    // 5 MiB. El enunciado no fija un numero: se elige el mismo tope que las
    // imagenes del catalogo (ADR 015) para no tener dos ideas de "archivo
    // grande" en el mismo sistema.
    public const long DefaultMaxSizeBytes = 5 * 1024 * 1024;

    // Coincide con el limite de una columna de texto razonable y con lo que
    // cualquier sistema de archivos acepta como nombre. El nombre original se
    // guarda, nunca se usa como ruta.
    public const int MaxNameLength = 255;

    // Un cliente que no declara tipo no deja el campo vacio: se guarda el tipo
    // generico de los adjuntos (RF-DOC-01 pide registrar el tipo).
    public const string DefaultContentType = "application/octet-stream";

    public static string NormalizeName(string name) => name.Trim();

    public static string NormalizeContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? DefaultContentType : contentType.Trim();
}
