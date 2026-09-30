namespace PirryLedger.Core.Notifications.Infrastructure;

// Como se negotiate la seguridad del transporte SMTP. No todas las cuentas usan
// el mismo puerto: Gmail, por ejemplo, usa 587 con STARTTLS y 465 con TLS
// directo.
public enum ModoSsl
{
    // Sin cifrado. Solo para un servidor de pruebas en la misma maquina.
    Ninguno = 0,

    // Puerto 587: se conecta en claro y se sube a TLS al pedirlo el servidor.
    StartTls = 1,

    // Puerto 465: la conexion ya nace cifrada.
    Ssl = 2,
}

// RF-NOT-13 y RD-10: estos valores vienen de variables de entorno, nunca de un
// archivo del repositorio. El Host lee las variables y pasa esta estructura a
// AddNotifications, para que Infrastructure no tenga que conocer el
// mecanismo de configuracion.
public sealed record SmtpConfiguracion(
    string Servidor,
    int Puerto,
    ModoSsl ModoSeguridad,
    string Usuario,
    string Contrasena,
    string Remitente)
{
    public static SmtpConfiguracion Vacia { get; } =
        new(string.Empty, 0, ModoSsl.StartTls, string.Empty, string.Empty, string.Empty);

    public bool EstaConfigurada =>
        !string.IsNullOrWhiteSpace(Servidor) &&
        !string.IsNullOrWhiteSpace(Usuario) &&
        !string.IsNullOrWhiteSpace(Contrasena) &&
        !string.IsNullOrWhiteSpace(Remitente);
}
