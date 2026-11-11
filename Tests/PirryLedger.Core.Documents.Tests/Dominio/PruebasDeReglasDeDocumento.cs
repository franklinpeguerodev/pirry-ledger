using PirryLedger.Core.Documents.Domain.Entities;
using PirryLedger.Core.Documents.Domain.Exceptions;
using PirryLedger.Core.Documents.Domain.Rules;

namespace PirryLedger.Core.Documents.Tests.Dominio;

// RF-DOC-01 y RF-DOC-04: las reglas del registro y de la eliminacion logica.
// Cada prueba verifica un solo comportamiento y falla por su cuenta.
public sealed class PruebasDeReglasDeDocumento
{
    private static readonly Guid Uploader = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Branch = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime UploadedAt = new(2026, 11, 10, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void ElDocumentoGuardaNombreTipoTamanoFechaYUsuario()
    {
        var document = CreateValid();

        Assert.Equal("factura.pdf", document.Name);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(1024, document.SizeBytes);
        Assert.Equal(UploadedAt, document.UploadedAtUtc);
        Assert.Equal(Uploader, document.UploadedByUserId);
        Assert.NotEqual(Guid.Empty, document.Id);
        Assert.False(document.IsDeleted);
    }

    [Fact]
    public void ElNombreDelDocumentoSeGuardaSinEspaciosSobrantes()
    {
        var document = CreateValid(name: "  factura.pdf  ");

        Assert.Equal("factura.pdf", document.Name);
    }

    [Fact]
    public void ElDocumentoVacioSeRechaza()
    {
        var error = Assert.Throws<EmptyDocumentException>(() => CreateValid(sizeBytes: 0));

        Assert.Equal("El archivo está vacío.", error.Message);
    }

    [Fact]
    public void ElDocumentoQueSuperaElMaximoSeRechaza()
    {
        var error = Assert.Throws<DocumentTooLargeException>(
            () => CreateValid(sizeBytes: DocumentRules.DefaultMaxSizeBytes + 1));

        Assert.Equal(DocumentRules.DefaultMaxSizeBytes, error.MaxSizeBytes);
    }

    [Fact]
    public void ElDocumentoJustoEnElMaximoSeAcepta()
    {
        var document = CreateValid(sizeBytes: DocumentRules.DefaultMaxSizeBytes);

        Assert.Equal(DocumentRules.DefaultMaxSizeBytes, document.SizeBytes);
    }

    [Fact]
    public void ElDocumentoConNombreVacioSeRechaza()
    {
        var error = Assert.Throws<DocumentRuleViolationException>(() => CreateValid(name: "   "));

        Assert.Equal("El nombre del documento es obligatorio.", error.Message);
    }

    [Fact]
    public void ElDocumentoConNombreDemasiadoLargoSeRechaza()
    {
        var longName = new string('a', DocumentRules.MaxNameLength + 1);

        Assert.Throws<DocumentRuleViolationException>(() => CreateValid(name: longName));
    }

    [Fact]
    public void ElDocumentoSeGuardaSinTipoDeclaradoComoOctetStream()
    {
        var document = CreateValid(contentType: null);

        Assert.Equal(DocumentRules.DefaultContentType, document.ContentType);
    }

    [Fact]
    public void LaSucursalYElUsuarioSeGuardanTalComoLlegan()
    {
        var document = CreateValid();

        Assert.Equal(Branch, document.BranchId);
        Assert.Equal(Uploader, document.UploadedByUserId);
    }

    [Fact]
    public void EliminarMarcaLaFechaYElUsuario()
    {
        var document = CreateValid();
        var deletedAt = UploadedAt.AddHours(1);

        document.Delete(Uploader, deletedAt);

        Assert.True(document.IsDeleted);
        Assert.Equal(deletedAt, document.DeletedAtUtc);
        Assert.Equal(Uploader, document.DeletedByUserId);
    }

    [Fact]
    public void EliminarDosVecesSeRechaza()
    {
        var document = CreateValid();
        document.Delete(Uploader, UploadedAt.AddHours(1));

        Assert.Throws<DocumentAlreadyDeletedException>(
            () => document.Delete(Uploader, UploadedAt.AddHours(2)));
    }

    [Fact]
    public void ElDocumentoReconoceASuDuenio()
    {
        var document = CreateValid();

        Assert.True(document.IsOwnedBy(Uploader));
        Assert.False(document.IsOwnedBy(Guid.NewGuid()));
    }

    private static Document CreateValid(
        string name = "factura.pdf",
        string? contentType = "application/pdf",
        long sizeBytes = 1024) =>
        Document.Create(
            name,
            contentType,
            sizeBytes,
            Uploader,
            Branch,
            "documents/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            UploadedAt,
            DocumentRules.DefaultMaxSizeBytes);
}
