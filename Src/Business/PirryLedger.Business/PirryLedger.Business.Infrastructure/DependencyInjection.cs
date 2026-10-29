using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Ports;
using PirryLedger.Business.Infrastructure.Persistence;

namespace PirryLedger.Business.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBusiness(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<BusinessDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();

        // Hoy la notificacion al Administrador es vacia: se encola y un proceso
        // aparte la manda (regla de seguridad). La implementacion real llega en
        // la semana 10 (RF-NEG-07).
        services.AddSingleton<IInvoiceEditedNotifier, NoOpInvoiceEditedNotifier>();

        return services;
    }
}

internal sealed class NoOpInvoiceEditedNotifier : IInvoiceEditedNotifier
{
    public void Notify(PirryLedger.Business.Domain.Entities.Invoice invoice, Guid editedByUserId, DateTime editedAtUtc)
    {
    }
}
