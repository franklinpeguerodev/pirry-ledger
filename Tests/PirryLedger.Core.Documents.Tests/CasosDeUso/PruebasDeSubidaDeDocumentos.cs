using PirryLedger.Core.Documents.Application.Exceptions;
using PirryLedger.Core.Documents.Domain.Exceptions;
using PirryLedger.Core.Documents.Domain.Rules;

namespace PirryLedger.Core.Documents.Tests.CasosDeUso;

// RF-DOC-01: subir un documento registra el nombre, el tipo, el tamano, la
// fecha y quien lo subio, y guarda los bytes. RD-07: lo invalido se rechaza sin
// tocar el almacenamiento.
public sealed class PruebasDeSubidaDeDocumentos
{
    private static readonly byte[] Content = [37, 80, 68, 70];

    [Fact]
    public async Task SubirDocumentoRegistraNombreTipoTamanoFechaYUsuario()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();

        var document = await scenario.NewUpload().ExecuteAsync(
            token,
            "factura.pdf",
            "application/pdf",
            Content);

        Assert.Equal("factura.pdf", document.Name);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(Content.LongLength, document.SizeBytes);
        Assert.Equal(scenario.Clock.UtcNow, document.UploadedAtUtc);
        Assert.Equal(scenario.Standard.Id, document.UploadedByUserId);
    }

    [Fact]
    public async Task ElDocumentoRegistraLaSucursalDeLaSesion()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();

        var document = await scenario.NewUpload().ExecuteAsync(token, "factura.pdf", "application/pdf", Content);

        Assert.Equal(EscenarioDeDocumentos.StandardBranch, document.BranchId);
    }

    [Fact]
    public async Task ElDocumentoDeUnAdministradorRegistraLaSucursalGlobal()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenAdminSessionAsync();

        var document = await scenario.NewUpload().ExecuteAsync(token, "factura.pdf", "application/pdf", Content);

        Assert.Equal(PirryLedger.Core.AccessControl.Domain.Entities.Branch.AllId, document.BranchId);
    }

    [Fact]
    public async Task ElNombreOriginalNoSeUsaComoClaveDeAlmacenamiento()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();

        await scenario.NewUpload().ExecuteAsync(token, "factura confidencial.pdf", "application/pdf", Content);

        var key = Assert.Single(scenario.Store.Objects.Keys);
        Assert.StartsWith("documents/", key, StringComparison.Ordinal);
        Assert.DoesNotContain("factura", key, StringComparison.OrdinalIgnoreCase);
        Assert.True(Guid.TryParse(key["documents/".Length..], out _));
    }

    [Fact]
    public async Task SubirDocumentoVacioSeRechazaSinTocarElAlmacenamiento()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();

        await Assert.ThrowsAsync<EmptyDocumentException>(
            () => scenario.NewUpload().ExecuteAsync(token, "vacio.pdf", "application/pdf", []));

        Assert.Equal(0, scenario.Store.PutCount);
        Assert.Equal(0, scenario.Documents.SaveCount);
    }

    [Fact]
    public async Task SubirDocumentoMayorAlLimiteSeRechazaSinTocarElAlmacenamiento()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();
        var tooLarge = new byte[DocumentRules.DefaultMaxSizeBytes + 1];

        await Assert.ThrowsAsync<DocumentTooLargeException>(
            () => scenario.NewUpload().ExecuteAsync(token, "grande.zip", "application/zip", tooLarge));

        Assert.Equal(0, scenario.Store.PutCount);
        Assert.Equal(0, scenario.Documents.SaveCount);
    }

    [Fact]
    public async Task ElAlmacenamientoSinConfigurarSeRechazaDeFormaControlada()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();
        scenario.Store.Mode = StoreMode.NotConfigured;

        await Assert.ThrowsAsync<DocumentStorageNotConfiguredException>(
            () => scenario.NewUpload().ExecuteAsync(token, "factura.pdf", "application/pdf", Content));

        Assert.Equal(0, scenario.Documents.SaveCount);
    }

    [Fact]
    public async Task ElAlmacenamientoNoDisponibleSeRechazaDeFormaControlada()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();
        scenario.Store.Mode = StoreMode.Unavailable;

        await Assert.ThrowsAsync<DocumentStorageUnavailableException>(
            () => scenario.NewUpload().ExecuteAsync(token, "factura.pdf", "application/pdf", Content));

        Assert.Equal(0, scenario.Documents.SaveCount);
    }
}
