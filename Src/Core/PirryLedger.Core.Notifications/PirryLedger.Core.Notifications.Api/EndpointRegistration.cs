using Microsoft.AspNetCore.Routing;

namespace PirryLedger.Core.Notifications.Api;

// Los endpoints de la pieza pertenecen a la pieza, no al Host (RD-01).
public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapNotifications(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
