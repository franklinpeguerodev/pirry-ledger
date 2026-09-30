using Microsoft.Extensions.DependencyInjection;

namespace PirryLedger.Core.AccessControl.Infrastructure;

// El modulo se registra a si mismo. El Host solo llama a este metodo y no
// necesita saber que DbContext, repositorios o casos de uso existen aqui (RD-01).
public static class DependencyInjection
{
    public static IServiceCollection AddAccessControl(this IServiceCollection services)
    {
        return services;
    }
}
