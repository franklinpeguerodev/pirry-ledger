using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using PirryLedger.Business.Application.Exceptions;
using PirryLedger.Business.Application.Ports;

namespace PirryLedger.Business.Infrastructure.Storage;

// El almacenamiento de imagenes en Cloudflare R2 (ADR 015).
//
// R2 habla el protocolo de S3, asi que las descargas firmadas son las
// presigned URLs de SigV4 que genera el SDK de AWS. El bucket es privado: la
// unica forma de leer un objeto es con una firma.
//
// Firmar es un calculo local: no hay llamada de red. Subir y borrar si la
// hacen, y por eso necesitan credenciales.
public sealed class R2ImageStore : IImageStore
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public R2ImageStore(ImageStorageConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _bucket = configuration.Bucket;

        _client = new AmazonS3Client(
            new BasicAWSCredentials(configuration.AccessKeyId, configuration.SecretAccessKey),
            CreateClientConfig(configuration));
    }

    // La configuracion con la que se habla con R2.
    //
    // R2 no implementa el envio del cuerpo con el checksum en un trailer
    // (aws-chunked, STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER). El SDK lo
    // activa por defecto desde la version 4, asi que cada subida se rechazaba
    // con "not implemented". WHEN_REQUIRED deja el checksum solo en las
    // operaciones que lo exigen, y subir un objeto no lo exige.
    //
    // Es internal para que la prueba compruebe los dos ajustes sin tocar la
    // red: el cliente se construye igual, pero la prueba no lo usa.
    internal static AmazonS3Config CreateClientConfig(
        ImageStorageConfiguration configuration)
    {
        return new AmazonS3Config
        {
            ServiceURL = configuration.Endpoint,

            // R2 no tiene regiones: la region de la firma es "auto".
            AuthenticationRegion = "auto",

            // El bucket va en la ruta y no en el subdominio del host: es
            // lo que espera el endpoint de R2.
            ForcePathStyle = true,

            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
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
                new PutObjectRequest
                {
                    BucketName = _bucket,
                    Key = key,
                    InputStream = new MemoryStream(content, writable: false),
                    ContentType = contentType,
                },
                cancellationToken);
        }
        catch (AmazonClientException)
        {
            throw new ImageStorageUnavailableException();
        }
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteObjectAsync(
                new DeleteObjectRequest
                {
                    BucketName = _bucket,
                    Key = key,
                },
                cancellationToken);

            return true;
        }
        catch (AmazonClientException)
        {
            // El objeto se queda en el bucket como imagen residual, que se
            // asume por ahora (ADR 015). Se anota la clave para poder
            // limpiarlo mas adelante.
            //
            // No se escribe el error del bucket: puede traer el nombre del
            // bucket o el endpoint, y eso no se imprime (RD-08).
            Console.Error.WriteLine(
                $"No se pudo borrar el objeto {key} del almacenamiento de imagenes.");

            return false;
        }
    }

    public string CreateDownloadUrl(string key, string contentType, DateTime expiresAtUtc)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAtUtc,

            // El tipo que se fijo al subir, no el que dice el cliente. Asi el
            // bucket no puede servir un HTML como si fuera una imagen.
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentType = contentType,
                ContentDisposition = "inline",
            },
        };

        try
        {
            return _client.GetPreSignedURL(request);
        }
        catch (AmazonClientException)
        {
            throw new ImageStorageUnavailableException();
        }
    }
}
