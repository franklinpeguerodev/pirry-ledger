using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Infrastructure;

// Como se negotiate la seguridad del transporte SMTP. No todas las cuentas usan
// el mismo puerto: Gmail, por ejemplo, usa 587 con STARTTLS y 465 con TLS
// directo.
public enum SslMode
{
    // Sin cifrado. Solo para un servidor de pruebas en la misma maquina.
    None = 0,

    // Puerto 587: se conecta en claro y se sube a TLS al pedirlo el servidor.
    StartTls = 1,

    // Puerto 465: la conexion ya nace cifrada.
    Ssl = 2,
}

// RF-NOT-13 y RD-10: estos valores vienen de variables de entorno, nunca de un
// archivo del repository. El Host lee las variables y pasa esta estructura a
// AddNotifications, para que Infrastructure no tenga que conocer el
// mecanismo de configuration.
public sealed record SmtpConfiguration(
    string Server,
    int Port,
    SslMode SecurityMode,
    string User,
    string Password,
    string From)
{
    public static SmtpConfiguration Empty { get; } =
        new(string.Empty, 0, SslMode.StartTls, string.Empty, string.Empty, string.Empty);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Server) &&
        !string.IsNullOrWhiteSpace(User) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(From);
}
