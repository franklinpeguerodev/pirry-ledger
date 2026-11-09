using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Domain.Rules;

// Decide que formato tiene un archivo por sus bytes magicos (ADR 015).
//
// Por que no el Content-Type declarado: ese dato lo elige quien hace la
// peticion. Un archivo de texto con "Content-Type: image/jpeg" tiene que
// descubrirse como lo que es. Los bytes no mienten.
//
// Devuelve null en vez de lanzar: la excepcion que usa la API depende de la
// capa de presentacion (415), y el Dominio no la conoce. Quien llama decide
// que hacer con un archivo que no es una imagen.
public static class ImageFormatDetector
{
    // Firma del PNG: ocho bytes fijos (ISO 15948).
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // El JPEG empieza por SOI (FF D8) seguido de un marcador (FF).
    private const byte JpegFirst = 0xFF;
    private const byte JpegSecond = 0xD8;
    private const byte JpegThird = 0xFF;

    // El WebP es un RIFF cuyo tipo es "WEBP": "RIFF" + 4 bytes de tamaño + "WEBP".
    private const int WebPTypeOffset = 8;
    private const int WebPMinimumLength = 12;

    public static ImageFormat? Detect(byte[] content)
    {
        if (content is null || content.Length == 0)
        {
            return null;
        }

        if (IsJpeg(content))
        {
            return ImageFormat.Jpeg;
        }

        if (IsPng(content))
        {
            return ImageFormat.Png;
        }

        if (IsWebP(content))
        {
            return ImageFormat.WebP;
        }

        return null;
    }

    // El content type que se guarda en la base y el que viaja en la respuesta.
    public static string ContentTypeOf(ImageFormat format) =>
        format switch
        {
            ImageFormat.Jpeg => "image/jpeg",
            ImageFormat.Png => "image/png",
            ImageFormat.WebP => "image/webp",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Formato de imagen desconocido."),
        };

    // La extension de la clave del objeto. No decide el tipo: solo nombra el
    // archivo en el bucket.
    public static string ExtensionOf(ImageFormat format) =>
        format switch
        {
            ImageFormat.Jpeg => "jpg",
            ImageFormat.Png => "png",
            ImageFormat.WebP => "webp",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Formato de imagen desconocido."),
        };

    private static bool IsJpeg(byte[] content) =>
        content.Length >= 3
        && content[0] == JpegFirst
        && content[1] == JpegSecond
        && content[2] == JpegThird;

    private static bool IsPng(byte[] content)
    {
        if (content.Length < PngSignature.Length)
        {
            return false;
        }

        for (var index = 0; index < PngSignature.Length; index++)
        {
            if (content[index] != PngSignature[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWebP(byte[] content)
    {
        if (content.Length < WebPMinimumLength)
        {
            return false;
        }

        return content[0] == 'R'
            && content[1] == 'I'
            && content[2] == 'F'
            && content[3] == 'F'
            && content[WebPTypeOffset] == 'W'
            && content[WebPTypeOffset + 1] == 'E'
            && content[WebPTypeOffset + 2] == 'B'
            && content[WebPTypeOffset + 3] == 'P';
    }
}
