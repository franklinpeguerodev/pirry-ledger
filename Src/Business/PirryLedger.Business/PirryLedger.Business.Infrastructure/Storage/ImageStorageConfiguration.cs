namespace PirryLedger.Business.Infrastructure.Storage;

// La configuracion del bucket de imagenes (ADR 015).
//
// La lee el Host de las variables de entorno y se la pasa al modulo ya
// construida: la capa de Infrastructure no sabe de donde salio (ADR 006,
// RD-10). Todas sus variables son obligatorias, asi que esta estructura
// siempre esta completa.
public sealed record ImageStorageConfiguration(
    string AccountId,
    string AccessKeyId,
    string SecretAccessKey,
    string Bucket,
    string Endpoint,
    int SignatureMinutes,
    long MaxBytes);
