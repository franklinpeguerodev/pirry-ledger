using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Notifications.Application;

namespace PirryLedger.Core.Notifications.Infrastructure;

// El modulo se registra a si mismo. El Host solo llama a este metodo y no
// necesita saber que DbContext, repositorios o casos de uso existen aqui (RD-01).
public static class DependencyInjection
{
    public static IServiceCollection AddNotifications(
        this IServiceCollection services,
        string connectionString,
        SmtpConfiguracion smtp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<NotificationsDbContext>(opciones =>
            opciones.UseNpgsql(connectionString));

        services.AddSingleton(smtp);
        services.AddSingleton<IEmailTransporter, MailKitEmailTransporter>();
        services.AddScoped<ICorreoEnColaRepository, CorreoEnColaRepository>();
        services.AddScoped<IEmailQueue, EmailQueue>();
        services.AddScoped<ProcesarColaDeCorreo>();

        return services;
    }
}
