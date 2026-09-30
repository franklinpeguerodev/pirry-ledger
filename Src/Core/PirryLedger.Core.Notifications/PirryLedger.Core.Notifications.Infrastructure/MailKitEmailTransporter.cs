using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PirryLedger.Core.Notifications.Application;

namespace PirryLedger.Core.Notifications.Infrastructure;

internal sealed class MailKitEmailTransporter : IEmailTransporter
{
    private readonly SmtpConfiguracion _configuracion;

    public MailKitEmailTransporter(SmtpConfiguracion configuracion)
    {
        _configuracion = configuracion;
    }

    public async Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken cancellationToken = default)
    {
        if (!_configuracion.EstaConfigurada)
        {
            throw new EmailDeliveryException(
                "Faltan las variables de entorno del servidor de correo (RF-NOT-13). Revisa el README.");
        }

        using var cliente = new SmtpClient();

        try
        {
            await cliente.ConnectAsync(
                _configuracion.Servidor,
                _configuracion.Puerto,
                TraducirModoSsl(_configuracion.ModoSeguridad),
                cancellationToken);

            await cliente.AuthenticateAsync(_configuracion.Usuario, _configuracion.Contrasena, cancellationToken);

            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_configuracion.Remitente, _configuracion.Usuario));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = asunto;
            mensaje.Body = new TextPart("plain") { Text = cuerpo };

            await cliente.SendAsync(mensaje, cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception excepcion)
        {
            throw new EmailDeliveryException(
                $"No se pudo entregar el correo a {destinatario} por {_configuracion.Servidor}:{_configuracion.Puerto}. {excepcion.Message}",
                excepcion);
        }
        finally
        {
            if (cliente.IsConnected)
            {
                await cliente.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }

    private static SecureSocketOptions TraducirModoSsl(ModoSsl modo) => modo switch
    {
        ModoSsl.Ninguno => SecureSocketOptions.None,
        ModoSsl.StartTls => SecureSocketOptions.StartTls,
        ModoSsl.Ssl => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls,
    };
}
