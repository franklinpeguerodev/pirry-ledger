using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Ports;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Business.Infrastructure.Storage;

namespace PirryLedger.Business.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBusiness(
        this IServiceCollection services,
        string connectionString,
        ImageStorageConfiguration imageStorage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(imageStorage);

        services.AddDbContext<BusinessDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();

        // El bucket de imagenes (ADR 015). El cliente del SDK es seguro para
        // usar entre varios hilos, asi que hay una sola instancia.
        services.AddSingleton<IImageStore>(_ => new R2ImageStore(imageStorage));

        // Hoy la notificacion al Administrador es vacia: se encola y un proceso
        // aparte la manda (regla de seguridad). La implementacion real llega en
        // la semana 10 (RF-NEG-07).
        services.AddSingleton<IInvoiceEditedNotifier, NoOpInvoiceEditedNotifier>();

        // RF-NEG-02: cada capacidad del negocio es un caso de uso que el API
        // resuelve por su tipo. La factura se busca y se guarda por el mismo
        // repositorio; el reloj, la autoridad de permisos y el gateway de
        // solicitudes (ADR 012) los aportan sus propias piezas.
        services.AddScoped<IssueInvoice>();
        services.AddScoped<CreateProduct>();
        services.AddScoped<UpdateProduct>();
        services.AddScoped<SetProductActive>();
        services.AddScoped<ListProducts>();
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
