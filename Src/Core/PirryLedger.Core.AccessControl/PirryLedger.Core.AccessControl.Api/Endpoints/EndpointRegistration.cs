using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Api;

// Los endpoints de la pieza pertenecen a la pieza, no al Host (RD-01). Aqui solo
// se traduce HTTP a una llamada de caso de uso: ninguna regla de negocio vive en
// esta capa (RD-02).
public static class EndpointRegistration
{
    // Decision propia: el enlace de activacion caduca en 24 horas. entities.md no
    // lo fija. Es tiempo de sobra para que el destinatario lo abra y corto para
    // que un enlace olvidado en un Email no sirva dentro de un mes.
    private static readonly TimeSpan TokenValidity = TimeSpan.FromHours(24);

    public static IEndpointRouteBuilder MapAccessControl(this IEndpointRouteBuilder endpoints)
    {
        // RF-CA-01, RF-CA-15 y RF-NOT-08.
        endpoints.MapPost("/api/auth/register", async (
            RegisterRequest request,
            RegisterUser register,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            // RD-07 y RD-08: todo rechazo sale como un message controlado. Ni una
            // traza, ni una consulta SQL, ni el Email o la contrasena del
            // User en la respuesta.
            try
            {
                await register.ExecuteAsync(
                    request.Name,
                    request.Email,
                    request.Password,
                    TokenValidity,
                    cancellationToken);
            }
            catch (DuplicateEmailException)
            {
                // 409 con el mismo cuerpo que un Email nuevo. El flujo no dice si
                // ese Email ya estaba registrado.
                return Results.Json(
                    new ErrorResponse("No pudimos completar el registro con esos datos."),
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (RegistrationRejectedException error)
            {
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // 202: el Email sale por la cola, no ahora. La cuenta existe pero esta
            // inactiva hasta que se abra el enlace.
            return Results.Json(
                new ActivatedResponse(Activated: false),
                statusCode: StatusCodes.Status202Accepted);
        })
        .WithAccess(Operation.RegisterUser);

        // RF-CA-16: abrir el enlace activa la cuenta.
        endpoints.MapGet("/activate", async (
            string? token,
            ActivateAccount activate,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await activate.ExecuteAsync(token ?? string.Empty, cancellationToken);
            }
            catch (ActivationRejectedException error)
            {
                // El mismo 400 para un enlace usado, vencido o inexistente: no se
                // revela si ese enlace existio alguna vez.
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Json(new ActivatedResponse(Activated: true));
        })
        .WithAccess(Operation.ActivateAccount);

        // RF-CA-17: reenviar el enlace de activacion.
        //
        // El cuerpo de la respuesta es el mismo que en el registro y el codigo es
        // 202 tambien. Si el Email no existe, la cuenta ya esta activa o el
        // Email esta mal escrito, la respuesta no cambia ni un byte: no se puede
        // usar este endpoint para averiguar quien tiene cuenta.
        endpoints.MapPost("/api/auth/resend-activation", async (
            ResendActivationRequest request,
            ResendActivationLink resend,
            CancellationToken cancellationToken) =>
        {
            await resend.ExecuteAsync(
                request.Email,
                TokenValidity,
                cancellationToken);

            return Results.Json(
                new SentResponse(Sent: true),
                statusCode: StatusCodes.Status202Accepted);
        })
        .WithAccess(Operation.ResendActivation);

        endpoints.MapPost("/api/auth/recovery", async (
            PasswordRecoveryRequest request,
            [FromServices] PasswordRecovery recovery,
            CancellationToken cancellationToken) =>
        {
            await recovery.RequestAsync(request.Email, cancellationToken);
            return Results.Json(new SentResponse(true), statusCode: StatusCodes.Status202Accepted);
        }).WithAccess(Operation.RequestRecovery);

        endpoints.MapPost("/api/auth/reset-password", async (
            ResetPasswordRequest request,
            [FromServices] PasswordRecovery recovery,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await recovery.CompleteAsync(request.Code, request.Password, cancellationToken);
                return Results.NoContent();
            }
            catch (RecoveryRejectedException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
            catch (RegistrationRejectedException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
        }).WithAccess(Operation.ResetPassword);

        // RF-CA-03: iniciar Session. Devuelve el token en claro UNA vez; el cliente
        // lo envia en Authorization: Bearer y ya nunca se vuelve a pedir.
        endpoints.MapPost("/api/auth/login", async (
            LoginRequest request,
            Login login,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var token = await login.ExecuteAsync(
                    request.Email,
                    request.Password,
                    cancellationToken);

                return Results.Json(
                    new TokenResponse(token),
                    statusCode: StatusCodes.Status200OK);
            }
            catch (CredentialsRejectedException error)
            {
                // Email inexistente y contrasena erronea: mismo 401, mismo cuerpo.
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (AccountBlockedException error)
            {
                // Decision de Franklin: el message de bloqueo es el MISMO que el de
                // credenciales, porque un message propio confirmaria que ese Email
                // existe. El cliente ve un 401 igual que en los demas fallos.
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (AccountNotActivatedException error)
            {
                // Unico message distinto, y solo se alcanza con la contrasena
                // correcta (RF-CA-15).
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
        })
        .WithAccess(Operation.Login);

        // RF-CA-18: Close Session. 204 siempre, exista o no el token, para que no
        // se pueda comprobar si una credencial fue valida alguna vez.
        endpoints.MapPost("/api/auth/logout", async (
            HttpRequest httpRequest,
            Logout logout,
            CancellationToken cancellationToken) =>
        {
            await logout.ExecuteAsync(
                TokenBearer.Read(httpRequest) ?? string.Empty,
                cancellationToken);

            return Results.NoContent();
        })
        .WithAccess(Operation.Logout);

        // RF-CA-07: el User autenticado y su role.
        //
        // Devuelve SOLO Name, Email y Role. Nunca el hash, nunca el token y
        // nunca CredentialVersion: por mucho que se sabe de la Session, eso no le
        // sirve a nadie y no debe viajar.
        endpoints.MapGet("/me", async (
            HttpRequest httpRequest,
            Authenticate authenticate,
            CancellationToken cancellationToken) =>
        {
            AuthenticatedUser user;

            try
            {
                user = await authenticate.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);
            }
            catch (InvalidSessionException error)
            {
                // 401: no hay Session valida.
                //
                // Aqui no se captura InsufficientRoleException porque /me no exige
                // ningun role: solo pregunta quien es. Un 403 en este endpoint
                // seria imposible, y un catch para una excepcion que no puede
                // ocurrir solo confunde a quien lo lea.
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Json(new MeResponse(user.Name, user.Email, user.Role.ToString()));
        })
        .WithAccess(Operation.QueryOwnSession);

        MapUserAdministration(endpoints);

        endpoints.MapPost("/api/auth/change-password", async (
            ChangePasswordRequest request,
            HttpRequest httpRequest,
            [FromServices] ChangeOwnPassword changePassword,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await changePassword.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    request.CurrentPassword,
                    request.NewPassword,
                    cancellationToken);
                return Results.NoContent();
            }
            catch (InvalidSessionException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (RegistrationRejectedException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
        }).WithAccess(Operation.ChangeOwnPassword);

        return endpoints;
    }

    // RF-CA-21, RF-CA-08 y RF-CA-20: las cuatro rutas de Administrator.
    //
    // RF-CA-05: aqui no se compara ningun role. Cada route se declara con
    // WithAccess(Operation.X) y el filtro de acceso consulta RoleRequirements, el
    // punto unico. Este bloque solo traduce excepciones a codigos de estado.
    //
    // Los catch de InvalidSession y InsufficientRole se repiten en cada route, y no
    // hay un helper: un unico helper seria una funcion con cuatro ramas y un tipo
    // de retorno, mas dificil de leer que el try catch que se ve. Ademas el 401
    // tiene que salir antes que el 403 en todas, que es justo el orden que la
    // rubrica revisa.
    private static void MapUserAdministration(IEndpointRouteBuilder endpoints)
    {
        // RF-CA-21: listar usuarios con su role y su estado.
        endpoints.MapGet("/api/admin/users", async (
            HttpRequest httpRequest,
            ListUsers list,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var users = await list.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    cancellationToken);

                return Results.Ok(users);
            }
            catch (InvalidSessionException error)
            {
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InsufficientRoleException error)
            {
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithAccess(Operation.ListUsers);

        // RF-CA-08: cambiar el role de un User.
        endpoints.MapPost("/api/admin/users/{id:guid}/role", async (
            Guid id,
            ChangeRoleRequest request,
            HttpRequest httpRequest,
            ChangeUserRole changeRole,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await changeRole.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    id,
                    request.Role,
                    cancellationToken);

                return Results.NoContent();
            }
            catch (InvalidSessionException error)
            {
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InsufficientRoleException error)
            {
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status403Forbidden);
            }
            catch (AdministrationOperationRejectedException error)
            {
                // 400: el role es valido pero la operation no se puede hacer
                // (cambiarse el role a si mismo, User inexistente, Role que no
                // existe). No es 403: el role de quien llama SI alcanzaba.
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithAccess(Operation.ChangeUserRole);

        // RF-CA-20: Deactivate y Reactivate. La misma route y el mismo caso de uso
        // con un parametro distinto, porque Deactivate y Reactivate son la misma
        // Operation con dos finales.
        endpoints.MapPost("/api/admin/users/{id:guid}/deactivate", async (
            Guid id,
            HttpRequest httpRequest,
            DeactivateUser deactivate,
            CancellationToken cancellationToken) =>
        {
            return await ChangeStateAsync(httpRequest, deactivate, id, Operation.DeactivateUser, cancellationToken);
        })
        .WithAccess(Operation.DeactivateUser);

        endpoints.MapPost("/api/admin/users/{id:guid}/reactivate", async (
            Guid id,
            HttpRequest httpRequest,
            DeactivateUser deactivate,
            CancellationToken cancellationToken) =>
        {
            return await ChangeStateAsync(httpRequest, deactivate, id, Operation.ReactivateUser, cancellationToken);
        })
        .WithAccess(Operation.ReactivateUser);

        endpoints.MapPost("/api/admin/users/{id:guid}/force-reset", async (
            Guid id,
            HttpRequest httpRequest,
            [FromServices] ForcePasswordReset reset,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await reset.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    id,
                    cancellationToken);
                return Results.NoContent();
            }
            catch (InvalidSessionException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InsufficientRoleException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status403Forbidden);
            }
            catch (AdministrationOperationRejectedException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
        }).WithAccess(Operation.ForceReset);
    }

    // El cuerpo de los dos endpoints de estado. Es el unico caso donde un helper
    // si paga: los dos tendrian exactamente el mismo try catch, cuatro ramas
    // cada uno, y la unica diferencia seria una palabra. Duplicar eso invites a
    // que uno se actualice y el otro no.
    private static async Task<IResult> ChangeStateAsync(
        HttpRequest httpRequest,
        DeactivateUser useCase,
        Guid userId,
        Operation operation,
        CancellationToken cancellationToken)
    {
        try
        {
            // El mismo caso de uso para los dos finales. El metodo decide por que
            // Operation se entra: Deactivate o Reactivate. Asi el punto unico de
            // RF-CA-05 decide el role de cada uno y no el endpoint.
            if (operation == Operation.ReactivateUser)
            {
                await useCase.ReactivateAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    userId,
                    cancellationToken);
            }
            else
            {
                await useCase.ExecuteAsync(
                    TokenBearer.Read(httpRequest) ?? string.Empty,
                    userId,
                    cancellationToken);
            }

            return Results.NoContent();
        }
        catch (InvalidSessionException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (InsufficientRoleException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AdministrationOperationRejectedException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
