using PirryLedger.Core.Notifications.Api;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Infrastructure;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Host;

// Lectura de las variables de entorno (RD-10). Vive en el Host porque el Host es
// el unico sitio con acceso a IConfiguration: las piezas de Infrastructure
// reciben estructuras ya construidas y no saben de donde salieron.
//
// Ningun metodo imprime ni registra un valor: solo el Name de la variable, para
// que un error no pueda acabar en un log con una contrasena (RD-08).
internal static class EnvironmentConfiguration
{
    // Email saliente. La contrasena es la contrasena de aplicacion del
    // proveedor, nunca la de la cuenta.
    public const string SmtpServerVariable = "PIRRY_LEDGER_SMTP_HOST";
    public const string SmtpPortVariable = "PIRRY_LEDGER_SMTP_PORT";
    public const string SmtpSecurityVariable = "PIRRY_LEDGER_SMTP_SECURITY";
    public const string SmtpUserVariable = "PIRRY_LEDGER_SMTP_USER";
    public const string SmtpPasswordVariable = "PIRRY_LEDGER_SMTP_PASSWORD";
    public const string FromEmailVariable = "PIRRY_LEDGER_SMTP_FROM";

    // Direccion publica de la aplicacion. Es con la que se arman los enlaces de
    // activacion, para que funcionen al abrirse en un navegador, y con la que se
    // fija el host:puerto donde escucha la API. La recuperacion no lleva enlace:
    // lleva un codigo suelto, asi que esa variable no se usa ahi.
    public const string BaseUrlVariable = "PIRRY_LEDGER_PUBLIC_BASE_URL";

    // Primer Administrator (docs/adr/002-first-administrator.md). El registro
    // publico siempre crea Estándares, asi que sin estas dos variables no existe
    // ningun Administrator y la administracion de usuarios no se puede probar.
    //
    // La contrasena es la de esa cuenta, no la de aplicacion de un servidor de
    // Email. Al ser una variable de entorno cumple RD-10 como las de SMTP.
    public const string FirstAdminEmailVariable = "PIRRY_LEDGER_FIRST_ADMIN_EMAIL";
    public const string FirstAdminPasswordVariable = "PIRRY_LEDGER_FIRST_ADMIN_PASSWORD";
    public const string FirstAdminNameVariable = "PIRRY_LEDGER_FIRST_ADMIN_NAME";

    // Devuelve null y no lanza si no hay ninguna variable de Email definida. El
    // emisor decide entonces que avisar, y las demas operaciones ni se enteran:
    // encolar un Email no necesita servidor de Email (RF-NOT-08).
    public static SmtpConfiguration? ReadSmtp(IConfiguration configuration)
    {
        var server = configuration[SmtpServerVariable];
        var port = configuration[SmtpPortVariable];
        var security = configuration[SmtpSecurityVariable];
        var user = configuration[SmtpUserVariable];
        var password = configuration[SmtpPasswordVariable];
        var from = configuration[FromEmailVariable];

        var anyDefined = !string.IsNullOrWhiteSpace(server) ||
                        !string.IsNullOrWhiteSpace(user) ||
                        !string.IsNullOrWhiteSpace(password) ||
                        !string.IsNullOrWhiteSpace(from);

        if (!anyDefined)
        {
            return null;
        }

        return new SmtpConfiguration(
            server ?? string.Empty,
            ReadPort(port),
            ReadSecurityMode(security),
            user ?? string.Empty,
            password ?? string.Empty,
            from ?? string.Empty);
    }

    private static int ReadPort(string? text)
    {
        var defaultPort = 587;

        if (!int.TryParse(text, out var port) || port is < 1 or > 65535)
        {
            return defaultPort;
        }

        return port;
    }

    private static SslMode ReadSecurityMode(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "ninguno" or "none" => SslMode.None,
        "ssl" or "sslsobretls" or "ssl_on_connect" => SslMode.Ssl,
        _ => SslMode.StartTls,
    };
}
