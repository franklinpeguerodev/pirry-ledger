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
    // Decision de Franklin. El ADR justifica por que absoluta y no deslizante.
    private static readonly TimeSpan DuracionDeLaSesion = TimeSpan.FromHours(8);
    private static readonly TimeSpan DuracionDelCodigoDeRecuperacion = TimeSpan.FromMinutes(15);

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
        services.AddScoped<IRecoveryCodeRepository, RecoveryCodeRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();

        services.AddScoped<RegisterUser>(proveedor => new RegisterUser(
            proveedor.GetRequiredService<IUserRepository>(),
            proveedor.GetRequiredService<IActivationTokenRepository>(),
            proveedor.GetRequiredService<IPasswordHasher>(),
            proveedor.GetRequiredService<IEmailQueue>(),
            proveedor.GetRequiredService<IClock>(),
            urlBase));

        services.AddScoped<ActivateAccount>();

        // Decision de Franklin (docs/adr/001-credencial-de-sesion.md): caducidad
        // ABSOLUTA de 8 horas. La sesion no se renueva al usarla.
        services.AddScoped<Login>(proveedor => new Login(
            proveedor.GetRequiredService<IUserRepository>(),
            proveedor.GetRequiredService<ISessionRepository>(),
            proveedor.GetRequiredService<IPasswordHasher>(),
            proveedor.GetRequiredService<IClock>(),
            DuracionDeLaSesion));

        services.AddScoped<Logout>();
        services.AddScoped<Autenticar>();

        // RF-CA-05, RF-CA-08, RF-CA-20 y RF-CA-21. Los tres casos de uso reciben
        // Autenticar y no el repositorio de sesiones: el rechazo de rol tiene que
        // salir del punto unico (ExigenciasDeRol) y no de una comprobacion que
        // cada endpoint pudiera escribir de su forma.
        services.AddScoped<ListUsers>();
        services.AddScoped<ChangeUserRole>();
        services.AddScoped<DeactivateUser>();

        // RF-CA-17. No necesita IPasswordHasher: el reenvio no toca la contrasena.
        services.AddScoped<ResendActivationLink>(proveedor => new ResendActivationLink(
            proveedor.GetRequiredService<IUserRepository>(),
            proveedor.GetRequiredService<IActivationTokenRepository>(),
            proveedor.GetRequiredService<IEmailQueue>(),
            proveedor.GetRequiredService<IClock>(),
            urlBase));

        // Primer Administrador (docs/adr/002-primer-administrador.md). El Host lo
        // resuelve y lo ejecuta en el arranque, no hay endpoint ni comando para
        // dispararlo: no es una operacion de negocio, es parte del despliegue.
        services.AddScoped<SeedFirstAdministrator>();

        services.AddScoped<PasswordRecovery>(provider => new PasswordRecovery(
            provider.GetRequiredService<IUserRepository>(),
            provider.GetRequiredService<IRecoveryCodeRepository>(),
            provider.GetRequiredService<IEmailQueue>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IPasswordHasher>(),
            DuracionDelCodigoDeRecuperacion));
        services.AddScoped<ForcePasswordReset>(provider => new ForcePasswordReset(
            provider.GetRequiredService<IUserRepository>(),
            provider.GetRequiredService<IRecoveryCodeRepository>(),
            provider.GetRequiredService<IEmailQueue>(),
            provider.GetRequiredService<Autenticar>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IPasswordHasher>(),
            DuracionDelCodigoDeRecuperacion));
        services.AddScoped<ChangeOwnPassword>();

        return services;
    }
}