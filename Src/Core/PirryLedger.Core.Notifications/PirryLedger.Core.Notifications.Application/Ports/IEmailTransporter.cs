using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Application;

// El transporte real (SMTP, MailKit) vive en Infrastructure. El caso de uso solo
// sabe que puede entregar un Email y que falla con una excepcion si no pudo.
//
// Se declara aqui y no en Contracts porque es interno de la pieza de
// notificaciones: ningun otro modulo entrega Email, todos lo encolan.
public interface IEmailTransporter
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);
}
