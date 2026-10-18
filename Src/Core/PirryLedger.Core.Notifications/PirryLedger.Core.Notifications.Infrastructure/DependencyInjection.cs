using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.Contracts.Notifications;

namespace PirryLedger.Core.Notifications.Infrastructure;

// El modulo se registra a si mismo. El Host solo llama a este metodo y no
// necesita saber que DbContext, repositorios o casos de uso existen aqui (RD-01).
public static class DependencyInjection
{
    public static IServiceCollection AddNotifications(
        this IServiceCollection services,
        string connectionString,
        SmtpConfiguration smtp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton(smtp);
        services.AddSingleton<IEmailTransporter, MailKitEmailTransporter>();
        services.AddScoped<IEmailQueueRepository, EmailQueueRepository>();
        services.AddScoped<IEmailQueue, EmailQueue>();
        services.AddScoped<ProcessEmailQueue>();

        return services;
    }
}
