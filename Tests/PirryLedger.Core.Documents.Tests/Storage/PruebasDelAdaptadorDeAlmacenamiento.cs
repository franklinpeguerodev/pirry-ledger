using PirryLedger.Core.Documents.Application.Exceptions;
using PirryLedger.Core.Documents.Domain.Rules;
using PirryLedger.Core.Documents.Infrastructure.Storage;

namespace PirryLedger.Core.Documents.Tests.Storage;

// ADR 017: la forma de la peticion al almacenamiento y el rechazo controlado
// cuando no hay configuracion. Ninguna prueba toca la red.
public sealed class PruebasDelAdaptadorDeAlmacenamiento
{
    private static readonly byte[] Content = [1, 2, 3, 4];

    [Fact]
    public void LaPeticionDeSubidaNoFirmaElCuerpoPorBloques()
    {
        var request = SeaweedDocumentStore.CreatePutObjectRequest(
            "pirry-ledger-documents",
            "documents/abc",
            Content,
            "application/pdf");

        Assert.False(request.UseChunkEncoding);
        Assert.Equal("pirry-ledger-documents", request.BucketName);
        Assert.Equal("documents/abc", request.Key);
        Assert.Equal("application/pdf", request.ContentType);
    }

    [Fact]
    public void ElClienteUsaElEndpointConfiguradoYLaRutaDelBucket()
    {
        var configuration = CreateConfiguration();

        var clientConfig = SeaweedDocumentStore.CreateClientConfig(configuration);

        // El SDK normaliza la direccion con una barra final; lo que importa es
        // que apunte al endpoint configurado.
        Assert.Equal("http://seaweedfs:8333", clientConfig.ServiceURL.TrimEnd('/'));
        Assert.True(clientConfig.ForcePathStyle);
    }

    [Fact]
    public async Task ElAlmacenamientoSinConfigurarSeRechazaDeFormaControladaAlGuardar()
    {
        var store = new UnavailableDocumentStore();

        await Assert.ThrowsAsync<DocumentStorageNotConfiguredException>(
            () => store.PutAsync("documents/abc", Content, "application/pdf"));
    }

    [Fact]
    public async Task ElAlmacenamientoSinConfigurarSeRechazaDeFormaControladaAlLeer()
    {
        var store = new UnavailableDocumentStore();

        await Assert.ThrowsAsync<DocumentStorageNotConfiguredException>(
            () => store.GetAsync("documents/abc"));
    }

    [Fact]
    public void LaConfiguracionVaciaConservaElLimitePorDefecto()
    {
        Assert.Equal(DocumentRules.DefaultMaxSizeBytes, DocumentStorageConfiguration.Empty.MaxBytes);
    }

    private static DocumentStorageConfiguration CreateConfiguration() =>
        new("http://seaweedfs:8333", "clave-acceso", "clave-secreta", "pirry-ledger-documents", DocumentRules.DefaultMaxSizeBytes);
}
