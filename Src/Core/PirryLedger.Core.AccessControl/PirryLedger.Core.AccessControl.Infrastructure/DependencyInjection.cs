using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// El modulo se registra a si mismo. El Host solo llama a este metodo y no
// necesita saber que DbContext, repositorios o casos de uso existen aqui (RD-01).
public static class DependencyInjection
{
    // Decision de Franklin. El ADR justifica por que absoluta y no deslizante.
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(8);
    private static readonly TimeSpan RecoveryCodeDuration = TimeSpan.FromMinutes(15);

    public static IServiceCollection AddAccessControl(
        this IServiceCollection services,
        string connectionString,
        string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        services.AddDbContext<AccessControlDbContext>(options =>
            options.UseNpgsql(connectionString));

        // RF-CA-02: una sola implementacion de hash en el sistema. La registrada
        // aqui es la unica capa que ve el paquete de Argon2id.
        services.AddSingleton<Argon2Parameters>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IActivationTokenRepository, ActivationTokenRepository>();
        services.AddScoped<IRecoveryCodeRepository, RecoveryCodeRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();

        services.AddScoped<RegisterUser>(provider => new RegisterUser(
            provider.GetRequiredService<IUserRepository>(),
            provider.GetRequiredService<IActivationTokenRepository>(),
            provider.GetRequiredService<IPasswordHasher>(),
            provider.GetRequiredService<IEmailQueue>(),
            provider.GetRequiredService<IClock>(),
            baseUrl));

        services.AddScoped<ActivateAccount>();

        // Decision de Franklin (docs/adr/001-session-credential.md): caducidad
        // ABSOLUTA de 8 horas. La Session no se renueva al usarla.
        services.AddScoped<Login>(provider => new Login(
            provider.GetRequiredService<IUserRepository>(),
            provider.GetRequiredService<ISessionRepository>(),
            provider.GetRequiredService<IPasswordHasher>(),
            provider.GetRequiredService<IClock>(),
            SessionDuration));

        services.AddScoped<Logout>();
        services.AddScoped<Authenticate>(provider => new Authenticate(
            provider.GetRequiredService<ISessionRepository>(),
            provider.GetRequiredService<IUserRepository>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IPermissionAuthority>()));

        // RF-CA-05, RF-CA-08, RF-CA-20 y RF-CA-21. Los tres casos de uso reciben
        // Authenticate y no el repositorio de sesiones: el rechazo de role tiene que
        // salir del punto unico (RoleRequirements) y no de una comprobacion que
        // cada endpoint pudiera escribir de su forma.
        services.AddScoped<ListUsers>();
        services.AddScoped<ChangeUserRole>();
        services.AddScoped<DeactivateUser>();

        // RF-CA-17. No necesita IPasswordHasher: el reenvio no toca la contrasena.
        services.AddScoped<ResendActivationLink>(provider => new ResendActivationLink(
            provider.GetRequiredService<IUserRepository>(),
            provider.GetRequiredService<IActivationTokenRepository>(),
            provider.GetRequiredService<IEmailQueue>(),
            provider.GetRequiredService<IClock>(),
            baseUrl));

        // Primer Administrator (docs/adr/002-first-administrator.md). El Host lo
        // resuelve y lo ejecuta en el arranque, no hay endpoint ni comando para
        // dispararlo: no es una operation de negocio, es parte del despliegue.
        services.AddScoped<SeedFirstAdministrator>();

        services.AddScoped<PasswordRecovery>(provider => new PasswordRecovery(
            provider.GetRequiredService<IUserRepository>(),
            provider.GetRequiredService<IRecoveryCodeRepository>(),
            provider.GetRequiredService<IEmailQueue>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IPasswordHasher>(),
            RecoveryCodeDuration));
        services.AddScoped<ForcePasswordReset>(provider => new ForcePasswordReset(
            provider.GetRequiredService<IUserRepository>(),
            provider.GetRequiredService<IRecoveryCodeRepository>(),
            provider.GetRequiredService<IEmailQueue>(),
            provider.GetRequiredService<Authenticate>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IPasswordHasher>(),
            RecoveryCodeDuration));
        services.AddScoped<ChangeOwnPassword>();

        return services;
    }
}
