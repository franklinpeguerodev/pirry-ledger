using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PirryLedger.Core.Notifications.Domain.Errors;

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
                EmailQueueErrors.Configuration);
        }

        using var client = new SmtpClient();
        var submissionStarted = false;

        try
        {
            await client.ConnectAsync(
                _configuration.Server,
                _configuration.Port,
                TranslateSslMode(_configuration.SecurityMode),
                cancellationToken);

            // ADR 019: un servidor local sin credenciales no autentica. Antes
            // se autenticaba siempre, y con Mailpit (usuario y contrasena
            // vacios) el intento fallaba y el correo no salia nunca.
            if (_configuration.HasCredentials)
            {
                await client.AuthenticateAsync(_configuration.User, _configuration.Password, cancellationToken);
            }

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_configuration.From));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            submissionStarted = true;
            await client.SendAsync(message, cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            throw;
        }
        catch (SmtpCommandException)
        {
            throw new EmailDeliveryException(EmailQueueErrors.Rejected);
        }
        catch (FormatException) when (!submissionStarted)
        {
            throw new EmailDeliveryException(EmailQueueErrors.Configuration);
        }
        catch (Exception)
        {
            throw new EmailDeliveryException(submissionStarted ? EmailQueueErrors.Uncertain : EmailQueueErrors.Connection,
                isUncertain: submissionStarted);
        }
        finally
        {
            if (client.IsConnected)
            {
                // La aceptación de SendAsync no cambia si falla cerrar la sesión.
                try { await client.DisconnectAsync(true, CancellationToken.None); }
                catch (Exception) { }
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

}
