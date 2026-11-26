using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.Reports.Application;

namespace PirryLedger.Core.Reports.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReports(this IServiceCollection services)
    {
        services.AddScoped<GetPermissionRequestReport>();
        return services;
    }
}
