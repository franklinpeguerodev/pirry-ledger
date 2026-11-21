namespace PirryLedger.Core.Notifications.Domain.Entities;

public static class EmailQueueErrors
{
    public const string Configuration = "No se pudo preparar el correo o la configuración SMTP.";
    public const string Connection = "No se pudo conectar o autenticar con el servidor SMTP.";
    public const string Rejected = "El servidor SMTP rechazó el correo.";
    public const string Unknown = "Se registró un error de entrega.";
    public const string Uncertain = "No se pudo confirmar la entrega; el correo no se reenviará automáticamente.";

    public static string? Safe(string? error) => error is null ? null
        : error is Configuration or Connection or Rejected or Uncertain ? error : Unknown;
}
