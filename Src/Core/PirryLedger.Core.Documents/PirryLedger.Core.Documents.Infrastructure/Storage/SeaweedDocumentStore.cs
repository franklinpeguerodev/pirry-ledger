using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using PirryLedger.Core.Documents.Application.Exceptions;
using PirryLedger.Core.Documents.Application.Ports;

namespace PirryLedger.Core.Documents.Infrastructure.Storage;

// RF-DOC-01..04 (ADR 017): los bytes en SeaweedFS, por la API de S3.
//
// SeaweedFS habla el protocolo de S3, asi que se usa el mismo SDK que el
// adaptador de R2 del negocio (ADR 015). El cuerpo de cada subida viaja en una
// sola pieza y firmado con su sha256 (UseChunkEncoding = false, ADR 016): es la
// forma que ya se probo contra un almacenamiento S3-compatible.
//
// Nunca se imprime el endpoint ni el bucket (RD-08): solo la clave del objeto.
public sealed class SeaweedDocumentStore : IDocumentStore
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public SeaweedDocumentStore(DocumentStorageConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _bucket = configuration.Bucket;

        _client = new AmazonS3Client(
            new BasicAWSCredentials(configuration.AccessKeyId, configuration.SecretAccessKey),
            CreateClientConfig(configuration));
    }

    // internal para que la prueba compruebe la configuracion sin tocar la red.
    internal static AmazonS3Config CreateClientConfig(DocumentStorageConfiguration configuration)
    {
        return new AmazonS3Config
        {
            ServiceURL = configuration.Endpoint,

            // SeaweedFS no valida la region; us-east-1 es la convencion de S3 y
            // no cambia la firma.
            AuthenticationRegion = "us-east-1",

            // El bucket va en la ruta y no en el subdominio del host, que es lo
            // que espera un endpoint local sin DNS propio.
            ForcePathStyle = true,
        };
    }

    public async Task PutAsync(
        string key,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.PutObjectAsync(
                CreatePutObjectRequest(_bucket, key, content, contentType),
                cancellationToken);
        }
        catch (Exception error)
            when (error is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            // El mensaje del SDK puede traer el bucket o el endpoint: no se
            // escribe (RD-08).
            Console.Error.WriteLine($"No se pudo subir el documento {key} al almacenamiento.");

            throw new DocumentStorageUnavailableException();
        }
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _client.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = _bucket,
                    Key = key,
                },
                cancellationToken);

            using var memory = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memory, cancellationToken);

            return memory.ToArray();
        }
        catch (AmazonS3Exception error)
            when (error.StatusCode == HttpStatusCode.NotFound || error.ErrorCode == "NoSuchKey")
        {
            // El objeto no esta: es un 404 del caso de uso, no un fallo del
            // almacenamiento.
            return null;
        }
        catch (Exception error)
            when (error is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            Console.Error.WriteLine($"No se pudo leer el documento {key} del almacenamiento.");

            throw new DocumentStorageUnavailableException();
        }
    }

    // internal para que la prueba compruebe como viaja el cuerpo sin red.
    internal static PutObjectRequest CreatePutObjectRequest(
        string bucket,
        string key,
        byte[] content,
        string contentType)
    {
        return new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = new MemoryStream(content, writable: false),
            ContentType = contentType,
            UseChunkEncoding = false,
        };
    }
}
