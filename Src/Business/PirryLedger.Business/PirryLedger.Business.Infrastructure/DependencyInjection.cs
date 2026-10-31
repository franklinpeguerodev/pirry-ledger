using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.UseCases;
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

        // RF-NEG-02: cada capacidad del negocio es un caso de uso que el API
        // resuelve por su tipo. La factura se busca y se guarda por el mismo
        // repositorio; el reloj, la autoridad de permisos y el gateway de
        // solicitudes (ADR 012) los aportan sus propias piezas.
        services.AddScoped<IssueInvoice>();
        services.AddScoped<CollectInvoicePayment>();
        services.AddScoped<LeaveInvoiceAsDebt>();
        services.AddScoped<EditInvoiceItems>();
        services.AddScoped<MarkInvoiceAsNotDelivered>();
        services.AddScoped<CancelInvoice>();
        services.AddScoped<ConfirmInvoiceTransfer>();
        services.AddScoped<RejectInvoiceTransfer>();
        services.AddScoped<RegisterDebtPayment>();
        services.AddScoped<ListInvoices>();
        services.AddScoped<GetInvoice>();
        services.AddScoped<ListInvoiceEdits>();

        return services;
    }
}

internal sealed class NoOpInvoiceEditedNotifier : IInvoiceEditedNotifier
{
    public void Notify(PirryLedger.Business.Domain.Entities.Invoice invoice, Guid editedByUserId, DateTime editedAtUtc)
    {
    }
}
