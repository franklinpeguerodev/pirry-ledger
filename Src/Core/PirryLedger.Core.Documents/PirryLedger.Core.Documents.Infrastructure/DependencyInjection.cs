using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Documents.Application;
using PirryLedger.Core.Documents.Application.Ports;
using PirryLedger.Core.Documents.Application.UseCases;
using PirryLedger.Core.Documents.Infrastructure.Persistence;
using PirryLedger.Core.Documents.Infrastructure.Storage;

namespace PirryLedger.Core.Documents.Infrastructure;

// La pieza se registra a si misma. El Host solo llama a este metodo y no
// necesita saber que DbContext, repositorio o almacenamiento existen aqui
// (RD-01).
public static class DependencyInjection
{
    public static IServiceCollection AddDocuments(
        this IServiceCollection services,
        string connectionString,
        DocumentStorageConfiguration? storage = null,
        DocumentUploadOptions? uploadOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<DocumentDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IDocumentRepository, DocumentRepository>();

        var configuration = storage ?? DocumentStorageConfiguration.Empty;

        services.AddSingleton(uploadOptions ?? DocumentUploadOptions.Default);

        if (configuration.IsConfigured)
        {
            services.AddSingleton(configuration);
            services.AddSingleton<IDocumentStore, SeaweedDocumentStore>();
        }
        else
        {
            // Sin configuracion la aplicacion arranca igual y las operaciones
            // de documentos responden 503 controlado (ADR 017, decision 9).
            services.AddSingleton<IDocumentStore, UnavailableDocumentStore>();
        }

        // Casos de uso. Authenticate e IClock vienen de AccessControl y del
        // Host: la pieza no los crea y no sabe como se construyen.
        services.AddScoped<UploadDocument>(provider => new UploadDocument(
            provider.GetRequiredService<IDocumentRepository>(),
            provider.GetRequiredService<IDocumentStore>(),
            provider.GetRequiredService<Authenticate>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<DocumentUploadOptions>()));

        services.AddScoped<ListDocuments>(provider => new ListDocuments(
            provider.GetRequiredService<IDocumentRepository>(),
            provider.GetRequiredService<Authenticate>()));

        services.AddScoped<DownloadDocument>(provider => new DownloadDocument(
            provider.GetRequiredService<IDocumentRepository>(),
            provider.GetRequiredService<IDocumentStore>(),
            provider.GetRequiredService<Authenticate>()));

        services.AddScoped<DeleteDocument>(provider => new DeleteDocument(
            provider.GetRequiredService<IDocumentRepository>(),
            provider.GetRequiredService<Authenticate>(),
            provider.GetRequiredService<IClock>()));

        return services;
    }
}
