using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PirryLedger.Core.Notifications.Infrastructure;

internal sealed class MailKitEmailTransporter : IEmailTransporter
{
    private readonly SmtpConfiguration _configuration;

    public MailKitEmailTransporter(SmtpConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (!_configuration.IsConfigured)
        {
            throw new EmailDeliveryException(
                "Faltan las variables de entorno del servidor de Email (RF-NOT-13). Revisa el README.");
        }

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(
                _configuration.Server,
                _configuration.Port,
                TranslateSslMode(_configuration.SecurityMode),
                cancellationToken);

            await client.AuthenticateAsync(_configuration.User, _configuration.Password, cancellationToken);

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_configuration.From));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            await client.SendAsync(message, cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EmailDeliveryException(
                $"No se pudo entregar el Email a {recipient} por {_configuration.Server}:{_configuration.Port}. {Mask(exception.Message)}",
                new Exception(Mask(exception.ToString())));
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }

    private static SecureSocketOptions TranslateSslMode(SslMode mode) => mode switch
    {
        SslMode.None => SecureSocketOptions.None,
        SslMode.StartTls => SecureSocketOptions.StartTls,
        SslMode.Ssl => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls,
    };

    // RD-08: un error jamas debe filtrar credenciales. El User y la contrasena
    // son valores de variables de entorno (RD-10), asi que si una libreria
    // los repite en su message, ese message no puede guardarse ni mostrarse tal cual.
    private string Mask(string text)
    {
        var result = text;

        if (!string.IsNullOrWhiteSpace(_configuration.User))
        {
            result = result.Replace(_configuration.User, "[User SMTP]", StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(_configuration.Password))
        {
            result = result.Replace(_configuration.Password, "[contrasena SMTP]", StringComparison.Ordinal);
        }

        return result;
    }
}
