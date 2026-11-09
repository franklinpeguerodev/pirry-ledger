namespace PirryLedger.Business.Domain.Enums;

// Los tres formatos de imagen que el catalogo acepta (ADR 015). El tipo se
// decide por los bytes magicos del archivo, nunca por el Content-Type que
// declara el cliente: ese dato lo elige quien envia la peticion.
public enum ImageFormat
{
    Jpeg,
    Png,
    WebP,
}
