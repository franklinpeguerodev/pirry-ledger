using Microsoft.AspNetCore.Routing;

namespace PirryLedger.Core.AccessControl.Api;

// Los endpoints de la pieza pertenecen a la pieza, no al Host (RD-01).
public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapAccessControl(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
