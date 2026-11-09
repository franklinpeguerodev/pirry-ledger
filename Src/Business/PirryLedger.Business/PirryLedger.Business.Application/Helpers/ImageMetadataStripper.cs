using System.Text;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Application.Helpers;

// Quita los metadatos de una imagen sin tocarla (ADR 015).
//
// Una foto de celular trae fecha, modelo y, a menudo, coordenadas GPS. Esos
// datos no deben salir del sistema en un catalogo de productos.
//
// Solo se quitan los segmentos de metadatos: los pixeles no cambian y la
// imagen no se re-codifica. Re-codificar obligaria a DECODIFICAR un archivo
// no confiable, y un archivo pequeno puede declarar dimensiones enormes y
// agotar la memoria: esa es la superficie de ataque clasica de los
// decodificadores de imagen.
//
// Regla de rechazo: se rechaza solo lo que no es una imagen estructuralmente
// valida. Los metadatos nunca son motivo de rechazo. Un segmento o chunk
// desconocido se elimina y la subida continua.
public static class ImageMetadataStripper
{
    public static byte[] Strip(byte[] content, ImageFormat format) =>
        format switch
        {
            ImageFormat.Jpeg => StripJpeg(content),
            ImageFormat.Png => StripPng(content),
            ImageFormat.WebP => StripWebP(content),
            _ => throw new UnsupportedProductImageException(),
        };

    // --- JPEG ---------------------------------------------------------------

    private const byte JpegMarkerPrefix = 0xFF;
    private const byte JpegStartOfImage = 0xD8;
    private const byte JpegEndOfImage = 0xD9;
    private const byte JpegStartOfScan = 0xDA;
    private const byte JpegComment = 0xFE;
    private const byte JpegApplicationFirst = 0xE0;
    private const byte JpegApplicationLast = 0xEF;

    private static byte[] StripJpeg(byte[] content)
    {
        if (content.Length < 4 || content[0] != JpegMarkerPrefix || content[1] != JpegStartOfImage)
        {
            throw new UnsupportedProductImageException("El archivo JPEG no tiene el marcador inicial.");
        }

        var output = new List<byte>(content.Length) { JpegMarkerPrefix, JpegStartOfImage };
        var position = 2;
        var sawEnd = false;

        while (position < content.Length)
        {
            if (content[position] != JpegMarkerPrefix)
            {
                throw new UnsupportedProductImageException("El archivo JPEG esta danado.");
            }

            // Los bytes 0xFF de relleno antes de un marcador no se copian.
            while (position < content.Length && content[position] == JpegMarkerPrefix)
            {
                position++;
            }

            if (position >= content.Length)
            {
                throw new UnsupportedProductImageException("El archivo JPEG esta danado.");
            }

            var marker = content[position];
            position++;

            if (marker == JpegEndOfImage)
            {
                output.Add(JpegMarkerPrefix);
                output.Add(JpegEndOfImage);
                sawEnd = true;
                break;
            }

            if (marker == JpegStartOfScan)
            {
                output.AddRange(ReadSegment(content, marker, ref position));
                var next = FindNextMarker(content, position);

                if (next < 0)
                {
                    throw new UnsupportedProductImageException("El archivo JPEG esta danado.");
                }

                output.AddRange(content[position..next]);
                position = next;
                continue;
            }

            // Marcadores sueltos, sin longitud: TEM y los de reinicio RSTn.
            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                output.Add(JpegMarkerPrefix);
                output.Add(marker);
                continue;
            }

            var segment = ReadSegment(content, marker, ref position);

            if (ShouldKeepJpegSegment(marker))
            {
                output.AddRange(segment);
            }
        }

        if (!sawEnd)
        {
            throw new UnsupportedProductImageException("El archivo JPEG esta danado: le falta el final.");
        }

        return [.. output];
    }

    // Se conservan JFIF (APP0), el perfil de color (APP2) y el de Adobe (APP14).
    // Todo lo demas de APP, y los comentarios, son metadatos.
    private static bool ShouldKeepJpegSegment(byte marker)
    {
        if (marker == JpegComment)
        {
            return false;
        }

        if (marker >= JpegApplicationFirst && marker <= JpegApplicationLast)
        {
            return marker is 0xE0 or 0xE2 or 0xEE;
        }

        return true;
    }

    // Un segmento con longitud: el marcador, dos bytes de longitud (que la
    // incluyen) y el payload. La longitud se valida contra lo que queda del
    // archivo.
    private static byte[] ReadSegment(byte[] content, byte marker, ref int position)
    {
        if (position + 2 > content.Length)
        {
            throw new UnsupportedProductImageException("El archivo esta danado.");
        }

        var length = (content[position] << 8) | content[position + 1];

        if (length < 2 || position + length > content.Length)
        {
            throw new UnsupportedProductImageException("El archivo esta danado.");
        }

        var segment = new byte[length + 2];
        segment[0] = JpegMarkerPrefix;
        segment[1] = marker;
        Array.Copy(content, position, segment, 2, length);
        position += length;
        return segment;
    }

    // El siguiente marcador real: un 0xFF seguido de un byte que no sea un
    // byte stuffing (0x00), ni relleno (0xFF), ni un marcador de reinicio.
    private static int FindNextMarker(byte[] content, int start)
    {
        for (var index = start; index < content.Length - 1; index++)
        {
            if (content[index] != JpegMarkerPrefix)
            {
                continue;
            }

            var next = content[index + 1];

            if (next != 0x00 && next != JpegMarkerPrefix && (next < 0xD0 || next > 0xD7))
            {
                return index;
            }
        }

        return -1;
    }

    // --- PNG ----------------------------------------------------------------

    private static readonly string[] PngChunksToKeep =
    [
        "IHDR", "PLTE", "IDAT", "IEND", "tRNS", "gAMA", "cHRM", "sRGB", "iCCP", "pHYs",
    ];

    private static byte[] StripPng(byte[] content)
    {
        if (content.Length < 8)
        {
            throw new UnsupportedProductImageException("El archivo PNG esta danado.");
        }

        var output = new List<byte>(content.Length);
        output.AddRange(content[..8]);
        var position = 8;
        var sawEnd = false;

        while (position < content.Length)
        {
            if (position + 8 > content.Length)
            {
                throw new UnsupportedProductImageException("El archivo PNG esta danado.");
            }

            var length = ReadBigEndianInt32(content, position);

            if (length < 0 || position + 12L + length > content.Length)
            {
                throw new UnsupportedProductImageException("El archivo PNG esta danado.");
            }

            var type = Encoding.ASCII.GetString(content, position + 4, 4);
            var total = (int)(12 + (long)length);
            var ancillary = type.Length > 0 && type[0] >= 'a' && type[0] <= 'z';

            if (PngChunksToKeep.Contains(type))
            {
                output.AddRange(content[position..(position + total)]);
            }
            else if (!ancillary)
            {
                // Un chunk critico que no se conoce no se puede copiar sin
                // entenderlo, y no se puede ignorar porque lo necesita.
                throw new UnsupportedProductImageException(
                    $"El archivo PNG tiene un chunk critico desconocido: {type}.");
            }

            position += total;

            if (type == "IEND")
            {
                sawEnd = true;
                break;
            }
        }

        if (!sawEnd)
        {
            throw new UnsupportedProductImageException("El archivo PNG esta danado: le falta el final.");
        }

        return [.. output];
    }

    private static int ReadBigEndianInt32(byte[] content, int offset) =>
        (content[offset] << 24)
        | (content[offset + 1] << 16)
        | (content[offset + 2] << 8)
        | content[offset + 3];

    // --- WebP ---------------------------------------------------------------

    private static readonly string[] WebPChunksToKeep =
    [
        "VP8 ", "VP8L", "VP8X", "ALPH", "ANIM", "ANMF", "ICCP", "sRGB", "BGRA",
    ];

    // Bit de EXIF (0x08) y bit de XMP (0x04) de la cabecera VP8X.
    private const byte WebPExifFlag = 0x08;
    private const byte WebPXmpFlag = 0x04;

    private static byte[] StripWebP(byte[] content)
    {
        if (content.Length < 12
            || Encoding.ASCII.GetString(content, 0, 4) != "RIFF"
            || Encoding.ASCII.GetString(content, 8, 4) != "WEBP")
        {
            throw new UnsupportedProductImageException("El archivo WebP no tiene la cabecera RIFF.");
        }

        var output = new List<byte>(content.Length);
        output.AddRange(Encoding.ASCII.GetBytes("RIFF"));
        output.AddRange([0, 0, 0, 0]);
        output.AddRange(Encoding.ASCII.GetBytes("WEBP"));

        var position = 12;
        var sawImage = false;

        while (position + 8 <= content.Length)
        {
            var type = Encoding.ASCII.GetString(content, position, 4);
            var size = ReadLittleEndianInt32(content, position + 4);

            if (size < 0 || position + 8L + size > content.Length)
            {
                throw new UnsupportedProductImageException("El archivo WebP esta danado.");
            }

            var payload = content[(position + 8)..(position + 8 + size)];
            var padded = size + (size % 2);

            if (WebPChunksToKeep.Contains(type))
            {
                if (type == "VP8X")
                {
                    payload = ClearWebPFlags(payload);
                }

                // Se copia el chunk con el payload ya limpio: si se quitan los
                // chunks de EXIF y XMP, la cabecera VP8X deja de anunciarlos.
                output.AddRange(content[position..(position + 8)]);
                output.AddRange(payload);

                if (size % 2 == 1)
                {
                    output.Add(0);
                }

                if (type is "VP8 " or "VP8L")
                {
                    sawImage = true;
                }
            }

            position += 8 + padded;
        }

        if (!sawImage)
        {
            throw new UnsupportedProductImageException("El archivo WebP no tiene datos de imagen.");
        }

        var riffSize = output.Count - 8;
        output[4] = (byte)(riffSize & 0xFF);
        output[5] = (byte)((riffSize >> 8) & 0xFF);
        output[6] = (byte)((riffSize >> 16) & 0xFF);
        output[7] = (byte)((riffSize >> 24) & 0xFF);

        return [.. output];
    }

    private static int ReadLittleEndianInt32(byte[] content, int offset) =>
        content[offset]
        | (content[offset + 1] << 8)
        | (content[offset + 2] << 16)
        | (content[offset + 3] << 24);

    // Si se quitan los chunks de EXIF y XMP, la cabecera VP8X ya no puede
    // anunciarlos: los bits se limpian para que el archivo siga siendo valido.
    private static byte[] ClearWebPFlags(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return payload;
        }

        var cleaned = (byte[])payload.Clone();
        cleaned[0] = (byte)(cleaned[0] & ~(WebPExifFlag | WebPXmpFlag));
        return cleaned;
    }
}
