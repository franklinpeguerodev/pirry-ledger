using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Api;

// Los endpoints de la pieza pertenecen a la pieza, no al Host (RD-01). Aqui solo
// se traduce HTTP a una llamada de caso de uso: ninguna regla de negocio vive en
// esta capa (RD-02).
public static class EndpointRegistration
{
    // Decision propia: el enlace de activacion caduca en 24 horas. entities.md no
    // lo fija. Es tiempo de sobra para que el destinatario lo abra y corto para
    // que un enlace olvidado en un correo no sirva dentro de un mes.
    private static readonly TimeSpan ValidezDelToken = TimeSpan.FromHours(24);

    public static IEndpointRouteBuilder MapAccessControl(this IEndpointRouteBuilder endpoints)
    {
        // RF-CA-01, RF-CA-15 y RF-NOT-08.
        endpoints.MapPost("/api/auth/register", async (
            RegisterRequest peticion,
            RegisterUser registrar,
            IClock reloj,
            CancellationToken cancellationToken) =>
        {
            // RD-07 y RD-08: todo rechazo sale como un mensaje controlado. Ni una
            // traza, ni una consulta SQL, ni el correo o la contrasena del
            // usuario en la respuesta.
            try
            {
                await registrar.EjecutarAsync(
                    peticion.Nombre,
                    peticion.Correo,
                    peticion.Contrasena,
                    ValidezDelToken,
                    cancellationToken);
            }
            catch (DuplicateEmailException)
            {
                // 409 con el mismo cuerpo que un correo nuevo. El flujo no dice si
                // ese correo ya estaba registrado.
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

            // 202: el correo sale por la cola, no ahora. La cuenta existe pero esta
            // inactiva hasta que se abra el enlace.
            return Results.Json(
                new ActivadoResponse(Activado: false),
                statusCode: StatusCodes.Status202Accepted);
        });

        // RF-CA-16: abrir el enlace activa la cuenta.
        endpoints.MapGet("/activar", async (
            string? token,
            ActivateAccount activar,
            IClock reloj,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await activar.EjecutarAsync(token ?? string.Empty, cancellationToken);
            }
            catch (ActivationRejectedException error)
            {
                // El mismo 400 para un enlace usado, vencido o inexistente: no se
                // revela si ese enlace existio alguna vez.
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Json(new ActivadoResponse(Activado: true));
        });

        // RF-CA-17: reenviar el enlace de activacion.
        //
        // El cuerpo de la respuesta es el mismo que en el registro y el codigo es
        // 202 tambien. Si el correo no existe, la cuenta ya esta activa o el
        // correo esta mal escrito, la respuesta no cambia ni un byte: no se puede
        // usar este endpoint para averiguar quien tiene cuenta.
        endpoints.MapPost("/api/auth/reenviar-activacion", async (
            ResendActivationRequest peticion,
            ResendActivationLink reenviar,
            CancellationToken cancellationToken) =>
        {
            await reenviar.EjecutarAsync(
                peticion.Correo,
                ValidezDelToken,
                cancellationToken);

            return Results.Json(
                new EnviadoResponse(Enviado: true),
                statusCode: StatusCodes.Status202Accepted);
        });

        return endpoints;
    }
}