using PirryLedger.Core.Documents.Application.Exceptions;
using PirryLedger.Core.Documents.Domain.Entities;

namespace PirryLedger.Core.Documents.Tests.CasosDeUso;

// RF-DOC-02, RF-DOC-03 y RF-DOC-04: listar, descargar y eliminar, con la
// regla de propiedad y el rechazo de los eliminados.
public sealed class PruebasDeListadoYDescargaDeDocumentos
{
    private static readonly byte[] Content = [1, 2, 3, 4];

    [Fact]
    public async Task ListarDocumentosMuestraSoloLosPropiosDelUsuarioEstandar()
    {
        var scenario = new EscenarioDeDocumentos();
        var standardToken = await scenario.OpenStandardSessionAsync();
        var otherToken = await scenario.OpenOtherSessionAsync();

        await scenario.NewUpload().ExecuteAsync(standardToken, "mio.pdf", "application/pdf", Content);
        await scenario.NewUpload().ExecuteAsync(otherToken, "ajeno.pdf", "application/pdf", Content);

        var documents = await scenario.NewList().ExecuteAsync(standardToken);

        var document = Assert.Single(documents);
        Assert.Equal("mio.pdf", document.Name);
    }

    [Fact]
    public async Task ListarDocumentosMuestraTodosAlAdministrador()
    {
        var scenario = new EscenarioDeDocumentos();
        var standardToken = await scenario.OpenStandardSessionAsync();
        var otherToken = await scenario.OpenOtherSessionAsync();
        var adminToken = await scenario.OpenAdminSessionAsync();

        await scenario.NewUpload().ExecuteAsync(standardToken, "mio.pdf", "application/pdf", Content);
        await scenario.NewUpload().ExecuteAsync(otherToken, "ajeno.pdf", "application/pdf", Content);

        var documents = await scenario.NewList().ExecuteAsync(adminToken);

        Assert.Equal(2, documents.Count);
    }

    [Fact]
    public async Task ListarDocumentosNoMuestraLosEliminados()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();
        var document = await scenario.NewUpload().ExecuteAsync(token, "factura.pdf", "application/pdf", Content);

        await scenario.NewDelete().ExecuteAsync(token, document.Id);
        var documents = await scenario.NewList().ExecuteAsync(token);

        Assert.Empty(documents);
    }

    [Fact]
    public async Task DescargarDocumentoPropioDevuelveLosBytes()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();
        var document = await scenario.NewUpload().ExecuteAsync(token, "factura.pdf", "application/pdf", Content);

        var content = await scenario.NewDownload().ExecuteAsync(token, document.Id);

        Assert.Equal(Content, content.Content);
        Assert.Equal("factura.pdf", content.Name);
        Assert.Equal("application/pdf", content.ContentType);
    }

    [Fact]
    public async Task DescargarDocumentoAjenoSeRechazaParaElUsuarioEstandar()
    {
        var scenario = new EscenarioDeDocumentos();
        var ownerToken = await scenario.OpenStandardSessionAsync();
        var otherToken = await scenario.OpenOtherSessionAsync();
        var document = await scenario.NewUpload().ExecuteAsync(ownerToken, "factura.pdf", "application/pdf", Content);

        await Assert.ThrowsAsync<DocumentAccessDeniedException>(
            () => scenario.NewDownload().ExecuteAsync(otherToken, document.Id));

        var stored = await scenario.Documents.FindAsync(document.Id);
        Assert.False(stored!.IsDeleted);
        Assert.Single(scenario.Store.Objects);
    }

    [Fact]
    public async Task DescargarDocumentoAjenoFuncionaParaElAdministrador()
    {
        var scenario = new EscenarioDeDocumentos();
        var ownerToken = await scenario.OpenStandardSessionAsync();
        var adminToken = await scenario.OpenAdminSessionAsync();
        var document = await scenario.NewUpload().ExecuteAsync(ownerToken, "factura.pdf", "application/pdf", Content);

        var content = await scenario.NewDownload().ExecuteAsync(adminToken, document.Id);

        Assert.Equal(Content, content.Content);
    }

    [Fact]
    public async Task DescargarUnDocumentoEliminadoSeRechaza()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();
        var document = await scenario.NewUpload().ExecuteAsync(token, "factura.pdf", "application/pdf", Content);
        await scenario.NewDelete().ExecuteAsync(token, document.Id);

        await Assert.ThrowsAsync<DocumentNotFoundException>(
            () => scenario.NewDownload().ExecuteAsync(token, document.Id));
    }

    [Fact]
    public async Task EliminarDocumentoEsLogicoYElRegistroYElObjetoPermanece()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();
        var document = await scenario.NewUpload().ExecuteAsync(token, "factura.pdf", "application/pdf", Content);

        await scenario.NewDelete().ExecuteAsync(token, document.Id);

        var stored = await scenario.Documents.FindAsync(document.Id);
        Assert.NotNull(stored);
        Assert.True(stored!.IsDeleted);
        Assert.Equal(scenario.Standard.Id, stored.DeletedByUserId);
        Assert.Equal(scenario.Clock.UtcNow, stored.DeletedAtUtc);
        Assert.Single(scenario.Store.Objects);
    }

    [Fact]
    public async Task EliminarDocumentoAjenoSeRechazaParaElUsuarioEstandar()
    {
        var scenario = new EscenarioDeDocumentos();
        var ownerToken = await scenario.OpenStandardSessionAsync();
        var otherToken = await scenario.OpenOtherSessionAsync();
        var document = await scenario.NewUpload().ExecuteAsync(ownerToken, "factura.pdf", "application/pdf", Content);

        await Assert.ThrowsAsync<DocumentAccessDeniedException>(
            () => scenario.NewDelete().ExecuteAsync(otherToken, document.Id));

        var stored = await scenario.Documents.FindAsync(document.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsDeleted);
    }

    [Fact]
    public async Task EliminarDocumentoAjenoFuncionaParaElAdministrador()
    {
        var scenario = new EscenarioDeDocumentos();
        var ownerToken = await scenario.OpenStandardSessionAsync();
        var adminToken = await scenario.OpenAdminSessionAsync();
        var document = await scenario.NewUpload().ExecuteAsync(ownerToken, "factura.pdf", "application/pdf", Content);

        await scenario.NewDelete().ExecuteAsync(adminToken, document.Id);

        var stored = await scenario.Documents.FindAsync(document.Id);
        Assert.True(stored!.IsDeleted);
        Assert.Equal(scenario.Admin.Id, stored.DeletedByUserId);
    }

    [Fact]
    public async Task DescargarUnDocumentoInexistenteSeRechaza()
    {
        var scenario = new EscenarioDeDocumentos();
        var token = await scenario.OpenStandardSessionAsync();

        await Assert.ThrowsAsync<DocumentNotFoundException>(
            () => scenario.NewDownload().ExecuteAsync(token, Guid.NewGuid()));
    }
}
