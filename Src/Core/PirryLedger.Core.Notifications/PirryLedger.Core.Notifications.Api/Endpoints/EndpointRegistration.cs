using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.Notifications.Api;

// Los endpoints de la pieza pertenecen a la pieza, no al Host (RD-01).
public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapNotifications(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/notifications", ([FromServices] ManageNotificationInbox inbox, CancellationToken cancellationToken) =>
            RunAsync(async () => Results.Ok(await inbox.ListAsync(cancellationToken))))
            .WithAccess(Operation.ListOwnNotifications);
        endpoints.MapPost("/api/notifications/{id:guid}/read", (Guid id,
            [FromServices] ManageNotificationInbox inbox, CancellationToken cancellationToken) => RunAsync(async () =>
            {
                await inbox.MarkReadAsync(id, cancellationToken);
                return Results.NoContent();
            })).WithAccess(Operation.MarkNotificationRead);
        endpoints.MapPost("/api/notifications/read-all", ([FromServices] ManageNotificationInbox inbox,
            CancellationToken cancellationToken) => RunAsync(async () =>
                Results.Ok(new { marked = await inbox.MarkAllReadAsync(cancellationToken) })))
            .WithAccess(Operation.MarkAllNotificationsRead);
        return endpoints;
    }

    private static async Task<IResult> RunAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (NotificationNotFoundException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status404NotFound);
        }
        catch (NotificationSessionRequiredException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status401Unauthorized);
        }
    }
}
