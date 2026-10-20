using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Permissions.Application.Ports;
using PirryLedger.Core.Permissions.Application.UseCases;
using PirryLedger.Core.Permissions.Infrastructure.Persistence;
using PirryLedger.Core.Permissions.Infrastructure.Security;

namespace PirryLedger.Core.Permissions.Infrastructure;

// El modulo se registra a si mismo. El Host solo llama a este metodo y no
// necesita saber que DbContext, repositorios o autoridades existen aqui (RD-01).
public static class DependencyInjection
{
    public static IServiceCollection AddPermissions(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<PermissionsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IPermissionRequestRepository, PermissionRequestRepository>();
        services.AddScoped<IPermissionGrantRepository, PermissionGrantRepository>();

        // IPermissionAuthority es el contrato que vive en Core.Contracts.
        // Aqui registramos la implementacion que vive en Infrastructure. El
        // resto del sistema pide IPermissionAuthority por DI.
        services.AddScoped<IPermissionAuthority, EfPermissionAuthority>();

        // Casos de uso. Cada uno recibe sus dependencias por el constructor;
        // aqui se conectan los puertos a sus implementaciones y los servicios
        // de Application de AccessControl (Authenticate, IClock) que la pieza
        // reusa via Core.Contracts.
        services.AddScoped<CreatePermissionRequest>(provider => new CreatePermissionRequest(
            provider.GetRequiredService<IPermissionRequestRepository>(),
            provider.GetRequiredService<IPermissionAuthority>(),
            provider.GetRequiredService<Authenticate>(),
            provider.GetRequiredService<IClock>()));

        services.AddScoped<ListOwnPermissionRequests>(provider => new ListOwnPermissionRequests(
            provider.GetRequiredService<IPermissionRequestRepository>(),
            provider.GetRequiredService<Authenticate>()));

        services.AddScoped<ListPendingPermissionRequests>(provider => new ListPendingPermissionRequests(
            provider.GetRequiredService<IPermissionRequestRepository>(),
            provider.GetRequiredService<Authenticate>()));

        services.AddScoped<ApprovePermissionRequest>(provider => new ApprovePermissionRequest(
            provider.GetRequiredService<IPermissionRequestRepository>(),
            provider.GetRequiredService<IPermissionGrantRepository>(),
            provider.GetRequiredService<Authenticate>(),
            provider.GetRequiredService<IClock>()));

        services.AddScoped<RejectPermissionRequest>(provider => new RejectPermissionRequest(
            provider.GetRequiredService<IPermissionRequestRepository>(),
            provider.GetRequiredService<Authenticate>(),
            provider.GetRequiredService<IClock>()));

        return services;
    }
}