using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// El modulo se registra a si mismo. El Host solo llama a este metodo y no
// necesita saber que DbContext, repositorios o casos de uso existen aqui (RD-01).
public static class DependencyInjection
{
    public static IServiceCollection AddAccessControl(
        this IServiceCollection services,
        string connectionString,
        string urlBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlBase);

        services.AddDbContext<AccessControlDbContext>(opciones =>
            opciones.UseNpgsql(connectionString));

        // RF-CA-02: una sola implementacion de hash en el sistema. La registrada
        // aqui es la unica capa que ve el paquete de Argon2id.
        services.AddSingleton<ParametrosDeArgon2>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IActivationTokenRepository, ActivationTokenRepository>();

        services.AddScoped<RegisterUser>(proveedor => new RegisterUser(
            proveedor.GetRequiredService<IUserRepository>(),
            proveedor.GetRequiredService<IActivationTokenRepository>(),
            proveedor.GetRequiredService<IPasswordHasher>(),
            proveedor.GetRequiredService<IEmailQueue>(),
            proveedor.GetRequiredService<IClock>(),
            urlBase));

        services.AddScoped<ActivateAccount>();

        // RF-CA-17. No necesita IPasswordHasher: el reenvio no toca la contrasena.
        services.AddScoped<ResendActivationLink>(proveedor => new ResendActivationLink(
            proveedor.GetRequiredService<IUserRepository>(),
            proveedor.GetRequiredService<IActivationTokenRepository>(),
            proveedor.GetRequiredService<IEmailQueue>(),
            proveedor.GetRequiredService<IClock>(),
            urlBase));

        return services;
    }
}