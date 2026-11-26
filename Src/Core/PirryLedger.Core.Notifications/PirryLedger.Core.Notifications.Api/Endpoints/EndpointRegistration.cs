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
        endpoints.MapGet("/api/notifications/email-queue", (string? status, DateTime? fromUtc, DateTime? toUtc,
            int? offset, int? limit, [FromServices] InspectEmailQueue inspection, CancellationToken cancellationToken) => RunAsync(async () =>
            {
                EmailStatus? selected = null;
                if (status is not null)
                {
                    if (!Enum.TryParse<EmailStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
                        return Results.BadRequest(new { message = "El estado de correo indicado no existe." });
                    selected = parsed;
                }
                return Results.Ok(await inspection.ExecuteAsync(new EmailQueueFilter(selected, fromUtc, toUtc,
                    offset ?? 0, limit ?? 50), cancellationToken));
            })).WithAccess(Operation.InspectEmailQueue);
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
        endpoints.MapGet("/api/notifications/email-preference", ([FromServices] ManageNotificationPreference preferences,
            CancellationToken cancellationToken) => RunAsync(async () =>
                Results.Ok(new { emailEnabled = await preferences.GetAsync(cancellationToken) })))
            .WithAccess(Operation.GetNotificationEmailPreference);
        endpoints.MapPut("/api/notifications/email-preference", (NotificationEmailPreferenceBody body,
            [FromServices] ManageNotificationPreference preferences, CancellationToken cancellationToken) => RunAsync(async () =>
            {
                if (body.EmailEnabled is not { } enabled)
                    return Results.BadRequest(new { message = "Debe indicar si desea habilitar el correo." });
                await preferences.SetAsync(enabled, cancellationToken);
                return Results.NoContent();
            })).WithAccess(Operation.UpdateNotificationEmailPreference);
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
        catch (NotificationPreferenceDeniedException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (EmailQueueInspectionDeniedException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { message = "Los filtros de cola no son válidos; use fechas UTC ordenadas, offset no negativo y limit entre 1 y 100." });
        }
    }
}

public sealed record NotificationEmailPreferenceBody(bool? EmailEnabled);
