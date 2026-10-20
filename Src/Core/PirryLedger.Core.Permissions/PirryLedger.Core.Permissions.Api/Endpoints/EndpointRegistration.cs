using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Permissions.Api.Models;
using PirryLedger.Core.Permissions.Application.Exceptions;
using PirryLedger.Core.Permissions.Application.UseCases;

namespace PirryLedger.Core.Permissions.Api;

// Las rutas de la pieza pertenecen a la pieza, no al Host (RD-01). Aqui solo
// se traduce HTTP a una llamada de caso de uso: ninguna regla de negocio vive
// en esta capa (RD-02).
public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapPermissions(this IEndpointRouteBuilder endpoints)
    {
        // RF-GP-01: crear una solicitud de permiso. Responde 202 con el id
        // de la solicitud para que el cliente pueda seguir su estado.
        endpoints.MapPost("/api/permissions/requests", async (
            CreatePermissionRequestBody request,
            CreatePermissionRequest useCase,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var id = await useCase.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    request.RequestedPermission ?? string.Empty,
                    cancellationToken);

                return Results.Json(
                    new CreatedPermissionRequestResponse(id),
                    statusCode: StatusCodes.Status202Accepted);
            }
            catch (PermissionRequestRejectedException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithAccess(Operation.CreatePermissionRequest);

        // RF-GP-02 (caso propio): un usuario autenticado ve sus propias
        // solicitudes. El filtro (AnySession) deja pasar a cualquier
        // usuario con sesion valida; el caso de uso filtra por RequesterId.
        endpoints.MapGet("/api/permissions/requests/mine", async (
            ListOwnPermissionRequests useCase,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
        {
            var requests = await useCase.ExecuteAsync(
                TokenBearer.Read(httpRequest) ?? string.Empty,
                cancellationToken);

            return Results.Ok(requests);
        })
        .WithAccess(Operation.ListOwnPermissionRequests);

        // RF-GP-02 (caso del rol): solo Administrators ven las solicitudes
        // pendientes globales. El filtro declara la operation con
        // AccessLevel.Administrator, asi que un Estandar recibe 403 antes
        // de llegar aqui (RD-06).
        endpoints.MapGet("/api/permissions/requests/pending", async (
            ListPendingPermissionRequests useCase,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
        {
            var requests = await useCase.ExecuteAsync(
                TokenBearer.Read(httpRequest) ?? string.Empty,
                cancellationToken);

            return Results.Ok(requests);
        })
        .WithAccess(Operation.ListPendingPermissionRequests);

        // RF-GP-03 + RF-GP-05 + RF-GP-07: aprobar es una sola operacion que
        // pasa por tres transiciones (Pending->Approved->Applied y el INSERT
        // de la concesion). Responde 204.
        endpoints.MapPost("/api/permissions/requests/{id:guid}/approve", async (
            Guid id,
            ApprovePermissionRequest useCase,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await useCase.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    id,
                    cancellationToken);

                return Results.NoContent();
            }
            catch (PermissionRequestRejectedException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
            catch (SelfResolutionRejectedException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
            catch (InvalidPermissionRequestTransitionApplicationException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithAccess(Operation.ApprovePermissionRequest);

        // RF-GP-04 + RF-GP-07: rechazar exige motivo escrito. Responde 204.
        endpoints.MapPost("/api/permissions/requests/{id:guid}/reject", async (
            Guid id,
            RejectPermissionRequestBody request,
            RejectPermissionRequest useCase,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await useCase.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    id,
                    request.Reason ?? string.Empty,
                    cancellationToken);

                return Results.NoContent();
            }
            catch (PermissionRequestRejectedException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
            catch (SelfResolutionRejectedException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
            catch (InvalidPermissionRequestTransitionApplicationException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithAccess(Operation.RejectPermissionRequest);

        // RF-GP-05: ruta de demostracion del sistema de permisos. No
        // consulta base de datos: devuelve datos fijos. El filtro es el
        // que consulta IPermissionAuthority para decidir si el llamante
        // tiene ViewSalesSummary concedido.
        endpoints.MapGet("/api/permissions/sales-summary", () =>
            Results.Ok(new
            {
                date = "2026-10-20",
                totalInvoices = 12,
                totalAmount = 3500.00,
                branch = "Sucursal Principal"
            }))
        .WithAccess(Operation.ViewSalesSummary);

        return endpoints;
    }
}