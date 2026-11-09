using System.Text;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Helpers;
using PirryLedger.Business.Domain.Enums;

namespace PirryLedger.Business.Tests.Imagenes;

// Eliminacion de metadatos (ADR 015). Los archivos son sinteticos: lo que
// importa es que los segmentos y chunks que llevan informacion secundaria
// desaparezcan y que los de imagen se queden intactos.
public sealed class PruebasDeMetadatosDeImagen
{
    private const string TextoGps = "GPSLatitude 18N30";

    [Fact]
    public void UnJpegSinMetadatosQuedaIgual()
    {
        var original = Jpeg();

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.Jpeg);

        Assert.Equal(original, limpio);
    }

    [Fact]
    public void ElSegmentoExifDelJpegSeElimina()
    {
        var original = Jpeg(withExif: true);

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.Jpeg);

        Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(limpio));
        Assert.DoesNotContain(TextoGps, Encoding.ASCII.GetString(limpio));
        Assert.Contains("JFIF", Encoding.ASCII.GetString(limpio));
    }

    [Fact]
    public void ElComentarioDelJpegSeElimina()
    {
        var original = Jpeg(withComment: true);

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.Jpeg);

        Assert.DoesNotContain("un comentario", Encoding.ASCII.GetString(limpio));
    }

    [Fact]
    public void UnSegmentoDesconocidoDelJpegSeEliminaSinRechazar()
    {
        var original = Jpeg(withUnknownApplication: true);

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.Jpeg);

        Assert.DoesNotContain("datos secretos", Encoding.ASCII.GetString(limpio));
        Assert.Contains("JFIF", Encoding.ASCII.GetString(limpio));
    }

    [Fact]
    public void LosBytesDespuesDelFinalDelJpegSeDescartan()
    {
        var original = Jpeg(trailing: Encoding.ASCII.GetBytes("payload escondido"));

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.Jpeg);

        Assert.DoesNotContain("payload escondido", Encoding.ASCII.GetString(limpio));
    }

    [Fact]
    public void UnJpegSinFinalSeRechaza()
    {
        var original = Jpeg();
        var truncado = original[..^2];

        Assert.Throws<UnsupportedProductImageException>(
            () => ImageMetadataStripper.Strip(truncado, ImageFormat.Jpeg));
    }

    [Fact]
    public void UnPngSinMetadatosQuedaIgual()
    {
        var original = Png();

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.Png);

        Assert.Equal(original, limpio);
    }

    [Fact]
    public void ElTextoDelPngSeElimina()
    {
        var original = Png(withText: true, withExif: true);

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.Png);

        var texto = Encoding.ASCII.GetString(limpio);
        Assert.DoesNotContain("Software", texto);
        Assert.DoesNotContain("GPS", texto);
        Assert.Contains("IHDR", texto);
        Assert.Contains("IDAT", texto);
    }

    [Fact]
    public void UnChunkAuxiliarDesconocidoDelPngSeEliminaSinRechazar()
    {
        var original = Png(withUnknownAncillary: true);

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.Png);

        Assert.DoesNotContain("anotacion", Encoding.ASCII.GetString(limpio));
    }

    [Fact]
    public void UnChunkCriticoDesconocidoDelPngSeRechaza()
    {
        var original = Png(withUnknownCritical: true);

        Assert.Throws<UnsupportedProductImageException>(
            () => ImageMetadataStripper.Strip(original, ImageFormat.Png));
    }

    [Fact]
    public void UnPngSinFinalSeRechaza()
    {
        var original = Png();
        var truncado = original[..^12];

        Assert.Throws<UnsupportedProductImageException>(
            () => ImageMetadataStripper.Strip(truncado, ImageFormat.Png));
    }

    [Fact]
    public void ElExifDelWebPSeElimina()
    {
        var original = WebP(withExif: true, withVp8x: true);

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.WebP);

        var texto = Encoding.ASCII.GetString(limpio);
        Assert.DoesNotContain("EXIF", texto);
        Assert.DoesNotContain(TextoGps, texto);
        Assert.Contains("VP8 ", texto);
    }

    [Fact]
    public void ElTamanoDelRiffSeCorrigeTrasQuitarLosMetadatos()
    {
        var original = WebP(withExif: true, withVp8x: true);

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.WebP);

        var tamano = limpio[4] | (limpio[5] << 8) | (limpio[6] << 16) | (limpio[7] << 24);
        Assert.Equal(limpio.Length - 8, tamano);
    }

    [Fact]
    public void LosBitsDeExifYXmpDeLaCabeceraVp8xSeLimpian()
    {
        var original = WebP(withExif: true, withVp8x: true);

        var limpio = ImageMetadataStripper.Strip(original, ImageFormat.WebP);

        // La cabecera VP8X es el primer chunk que sobrevive.
        var indiceVp8x = Encoding.ASCII.GetString(limpio).IndexOf("VP8X", StringComparison.Ordinal);
        var flags = limpio[indiceVp8x + 8];
        Assert.Equal(0, flags & 0x08);
        Assert.Equal(0, flags & 0x04);
    }

    // --- Archivos sinteticos ------------------------------------------------

    private static byte[] Jpeg(
        bool withExif = false,
        bool withComment = false,
        bool withUnknownApplication = false,
        byte[]? trailing = null)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange(Segment(0xE0, [.. "JFIF\0"u8, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]));

        if (withExif)
        {
            bytes.AddRange(Segment(0xE1, [.. "Exif\0\0"u8, .. Encoding.ASCII.GetBytes(TextoGps)]));
        }

        if (withComment)
        {
            bytes.AddRange(Segment(0xFE, Encoding.ASCII.GetBytes("un comentario")));
        }

        if (withUnknownApplication)
        {
            bytes.AddRange(Segment(0xE3, Encoding.ASCII.GetBytes("datos secretos")));
        }

        bytes.AddRange(Segment(0xDB, [0x00, 0x01, 0x02, 0x03]));
        bytes.AddRange(Segment(0xDA, [0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]));
        bytes.AddRange([0x11, 0x22, 0x33, 0x44]);
        bytes.AddRange([0xFF, 0xD9]);

        if (trailing is not null)
        {
            bytes.AddRange(trailing);
        }

        return [.. bytes];
    }

    private static byte[] Segment(byte marker, byte[] payload)
    {
        var length = payload.Length + 2;
        var result = new List<byte> { 0xFF, marker, (byte)(length >> 8), (byte)(length & 0xFF) };
        result.AddRange(payload);
        return [.. result];
    }

    private static byte[] Png(
        bool withText = false,
        bool withExif = false,
        bool withUnknownAncillary = false,
        bool withUnknownCritical = false)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        bytes.AddRange(Chunk("IHDR", [0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x08, 0x06, 0x00, 0x00, 0x00]));

        if (withText)
        {
            bytes.AddRange(Chunk("tEXt", Encoding.ASCII.GetBytes("Software\0Photoshop")));
        }

        if (withExif)
        {
            bytes.AddRange(Chunk("eXIf", Encoding.ASCII.GetBytes($"Exif\0\0{TextoGps}")));
        }

        if (withUnknownAncillary)
        {
            bytes.AddRange(Chunk("zzZz", Encoding.ASCII.GetBytes("anotacion")));
        }

        if (withUnknownCritical)
        {
            bytes.AddRange(Chunk("ZzZz", [0x01, 0x02, 0x03]));
        }

        bytes.AddRange(Chunk("IDAT", [0x01, 0x02, 0x03]));
        bytes.AddRange(Chunk("IEND", []));

        return [.. bytes];
    }

    private static byte[] Chunk(string type, byte[] data)
    {
        var result = new List<byte>();
        var length = data.Length;
        result.AddRange([(byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length]);
        result.AddRange(Encoding.ASCII.GetBytes(type));
        result.AddRange(data);
        result.AddRange([0x00, 0x00, 0x00, 0x00]);
        return [.. result];
    }

    private static byte[] WebP(bool withExif = false, bool withVp8x = false)
    {
        var chunks = new List<byte>();

        if (withVp8x)
        {
            // La cabecera VP8X con los bits de EXIF (0x08) y XMP (0x04) activos.
            chunks.AddRange(WebPChunk("VP8X", [0x1C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
        }

        chunks.AddRange(WebPChunk("VP8 ", [0x00, 0x00, 0x00, 0x00]));

        if (withExif)
        {
            chunks.AddRange(WebPChunk("EXIF", Encoding.ASCII.GetBytes(TextoGps)));
        }

        var body = new List<byte>();
        body.AddRange(Encoding.ASCII.GetBytes("WEBP"));
        body.AddRange(chunks);

        var result = new List<byte>();
        result.AddRange(Encoding.ASCII.GetBytes("RIFF"));
        var size = body.Count;
        result.AddRange([(byte)(size & 0xFF), (byte)((size >> 8) & 0xFF), (byte)((size >> 16) & 0xFF), (byte)((size >> 24) & 0xFF)]);
        result.AddRange(body);
        return [.. result];
    }

    private static byte[] WebPChunk(string type, byte[] payload)
    {
        var result = new List<byte>();
        result.AddRange(Encoding.ASCII.GetBytes(type));
        var size = payload.Length;
        result.AddRange([(byte)(size & 0xFF), (byte)((size >> 8) & 0xFF), (byte)((size >> 16) & 0xFF), (byte)((size >> 24) & 0xFF)]);
        result.AddRange(payload);

        if (size % 2 == 1)
        {
            result.Add(0x00);
        }

        return [.. result];
    }
}
