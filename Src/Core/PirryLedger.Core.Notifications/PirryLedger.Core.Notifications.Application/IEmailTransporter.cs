namespace PirryLedger.Core.Notifications.Application;

// El transporte real (SMTP, MailKit) vive en Infrastructure. El caso de uso solo
// sabe que puede entregar un correo y que falla con una excepcion si no pudo.
//
// Se declara aqui y no en Contracts porque es interno de la pieza de
// notificaciones: ningun otro modulo entrega correo, todos lo encolan.
public interface IEmailTransporter
{
    Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken cancellationToken = default);
}
