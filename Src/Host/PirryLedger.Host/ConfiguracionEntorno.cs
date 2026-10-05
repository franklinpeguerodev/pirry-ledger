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
    // Correo saliente. La contrasena es la contrasena de aplicacion del
    // proveedor, nunca la de la cuenta.
    public const string VariableSmtpServidor = "PIRRY_LEDGER_SMTP_HOST";
    public const string VariableSmtpPuerto = "PIRRY_LEDGER_SMTP_PORT";
    public const string VariableSmtpSeguridad = "PIRRY_LEDGER_SMTP_SECURITY";
    public const string VariableSmtpUsuario = "PIRRY_LEDGER_SMTP_USER";
    public const string VariableSmtpContrasena = "PIRRY_LEDGER_SMTP_PASSWORD";
    public const string VariableCorreoRemitente = "PIRRY_LEDGER_SMTP_FROM";

    // Direccion publica de la aplicacion. Es con la que se arman los enlaces de
    // activacion, para que funcionen al abrirse en un navegador, y con la que se
    // fija el host:puerto donde escucha la API. La recuperacion no lleva enlace:
    // lleva un codigo suelto, asi que esa variable no se usa ahi.
    public const string VariableBaseUrl = "PIRRY_LEDGER_PUBLIC_BASE_URL";

    // Primer Administrador (docs/adr/002-primer-administrador.md). El registro
    // publico siempre crea Estándares, asi que sin estas dos variables no existe
    // ningun Administrador y la administracion de usuarios no se puede probar.
    //
    // La contrasena es la de esa cuenta, no la de aplicacion de un servidor de
    // correo. Al ser una variable de entorno cumple RD-10 como las de SMTP.
    public const string VariableAdministradorCorreo = "PIRRY_LEDGER_FIRST_ADMIN_EMAIL";
    public const string VariableAdministradorContrasena = "PIRRY_LEDGER_FIRST_ADMIN_PASSWORD";
    public const string VariableAdministradorNombre = "PIRRY_LEDGER_FIRST_ADMIN_NAME";

    // Devuelve null y no lanza si no hay ninguna variable de correo definida. El
    // emisor decide entonces que avisar, y las demas operaciones ni se enteran:
    // encolar un correo no necesita servidor de correo (RF-NOT-08).
    public static SmtpConfiguracion? LeerSmtp(IConfiguration configuracion)
    {
        var servidor = configuracion[VariableSmtpServidor];
        var puerto = configuracion[VariableSmtpPuerto];
        var seguridad = configuracion[VariableSmtpSeguridad];
        var usuario = configuracion[VariableSmtpUsuario];
        var contrasena = configuracion[VariableSmtpContrasena];
        var remitente = configuracion[VariableCorreoRemitente];

        var hayAlguna = !string.IsNullOrWhiteSpace(servidor) ||
                        !string.IsNullOrWhiteSpace(usuario) ||
                        !string.IsNullOrWhiteSpace(contrasena) ||
                        !string.IsNullOrWhiteSpace(remitente);

        if (!hayAlguna)
        {
            return null;
        }

        return new SmtpConfiguracion(
            servidor ?? string.Empty,
            LeerPuerto(puerto),
            LeerModoSeguridad(seguridad),
            usuario ?? string.Empty,
            contrasena ?? string.Empty,
            remitente ?? string.Empty);
    }

    private static int LeerPuerto(string? texto)
    {
        var puertoPorDefecto = 587;

        if (!int.TryParse(texto, out var puerto) || puerto is < 1 or > 65535)
        {
            return puertoPorDefecto;
        }

        return puerto;
    }

    private static ModoSsl LeerModoSeguridad(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "ninguno" or "none" => ModoSsl.Ninguno,
        "ssl" or "sslsobretls" or "ssl_on_connect" => ModoSsl.Ssl,
        _ => ModoSsl.StartTls,
    };
}
