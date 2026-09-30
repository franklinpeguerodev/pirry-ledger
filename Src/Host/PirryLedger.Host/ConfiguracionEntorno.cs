using PirryLedger.Core.Notifications.Infrastructure;

namespace PirryLedger.Host;

// Lectura de las variables de entorno (RD-10). Vive en el Host porque el Host es
// el unico sitio con acceso a IConfiguration: las piezas de Infrastructure
// reciben estructuras ya construidas y no saben de donde salieron.
//
// Ningun metodo imprime ni registra un valor: solo el nombre de la variable, para
// que un error no pueda acabar en un log con una contrasena (RD-08).
internal static class ConfiguracionEntorno
{
    public const string VariableSmtpServidor = "PIRRY_SMTP_SERVIDOR";
    public const string VariableSmtpPuerto = "PIRRY_SMTP_PUERTO";
    public const string VariableSmtpSeguridad = "PIRRY_SMTP_SEGURIDAD";
    public const string VariableSmtpUsuario = "PIRRY_SMTP_USUARIO";
    public const string VariableSmtpContrasena = "PIRRY_SMTP_CONTRASENA";
    public const string VariableCorreoRemitente = "PIRRY_CORREO_REMITENTE";
    public const string VariableBaseUrl = "PIRRY_BASE_URL";

    // Devuelve null y no lanza: el comando decide si puede seguir sin ese valor.
    public static SmtpConfiguracion? LeerSmtp(IConfiguration configuracion)
    {
        var servidor = configuracion[VariableSmtpServidor];
        var usuario = configuracion[VariableSmtpUsuario];
        var contrasena = configuracion[VariableSmtpContrasena];
        var remitente = configuracion[VariableCorreoRemitente];

        if (string.IsNullOrWhiteSpace(servidor) &&
            string.IsNullOrWhiteSpace(usuario) &&
            string.IsNullOrWhiteSpace(contrasena) &&
            string.IsNullOrWhiteSpace(remitente))
        {
            return null;
        }

        var puerto = LeerEntero(configuracion, VariableSmtpPuerto, 587);
        var seguridad = LeerSeguridad(configuracion[VariableSmtpSeguridad]);

        return new SmtpConfiguracion(
            servidor ?? string.Empty,
            puerto,
            seguridad,
            usuario ?? string.Empty,
            contrasena ?? string.Empty,
            remitente ?? string.Empty);
    }

    private static int LeerEntero(IConfiguration configuracion, string variable, int valorPorDefecto)
    {
        var texto = configuracion[variable];

        return int.TryParse(texto, out var valor) ? valor : valorPorDefecto;
    }

    private static ModoSsl LeerSeguridad(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "ninguno" or "none" => ModoSsl.Ninguno,
        "ssl" or "sslsobretls" => ModoSsl.Ssl,
        _ => ModoSsl.StartTls,
    };
}
