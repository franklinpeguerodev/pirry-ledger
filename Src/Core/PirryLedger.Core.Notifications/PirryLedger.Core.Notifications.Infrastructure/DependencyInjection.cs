using PirryLedger.Core.Contracts.Persistence;
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
        services.AddScoped<IModuleDatabaseConnection>(_ => new Persistence.ModuleDatabaseConnection(connectionString));
        services.AddScoped<IApplicationTransaction>(provider => new Persistence.PostgresApplicationTransaction(
            () => provider.GetRequiredService<IModuleDatabaseConnection>().Connection,
            () => provider.GetServices<IModuleTransactionParticipant>().ToArray(), ownsConnection: false));

        services.AddDbContext<NotificationsDbContext>((provider, options) =>
        {
            var shared = provider.GetService<IModuleDatabaseConnection>();
            if (shared is null) options.UseNpgsql(connectionString);
            else options.UseNpgsql(shared.Connection);
        });
        services.AddScoped<IModuleTransactionParticipant>(provider => provider.GetRequiredService<NotificationsDbContext>());

        // ADR 018: el paso de migracion de la pieza.
        services.AddScoped<PirryLedger.Core.Contracts.Persistence.IModuleMigration, Persistence.NotificationsMigration>();

        services.AddSingleton(smtp);
        services.AddSingleton<IEmailTransporter, MailKitEmailTransporter>();
        services.AddScoped<IEmailQueueRepository, EmailQueueRepository>();
        services.AddScoped<IEmailQueue, EmailQueue>();
        services.AddScoped<ProcessEmailQueue>();
        services.AddScoped<INotificationInboxRepository, NotificationInboxRepository>();
        services.AddScoped<ManageNotificationInbox>();

        return services;
    }
}
