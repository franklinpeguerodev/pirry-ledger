using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Permissions.Application.Ports;
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

        return services;
    }
}