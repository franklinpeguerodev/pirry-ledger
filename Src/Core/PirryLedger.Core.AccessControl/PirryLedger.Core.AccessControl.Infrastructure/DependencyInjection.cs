using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// El modulo se registra a si mismo. El Host solo llama a este metodo y no
// necesita saber que DbContext, repositorios o casos de uso existen aqui (RD-01).
public static class DependencyInjection
{
    public static IServiceCollection AddAccessControl(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AccessControlDbContext>(opciones =>
            opciones.UseNpgsql(connectionString));

        // RF-CA-02: una sola implementacion de hash en el sistema. La registrada
        // aqui es la unica capa que ve el paquete de Argon2id.
        services.AddSingleton<ParametrosDeArgon2>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

        return services;
    }
}