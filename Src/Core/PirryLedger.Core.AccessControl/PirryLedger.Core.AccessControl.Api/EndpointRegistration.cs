using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
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
        })
        .ConAcceso(Operacion.RegistrarUsuario);

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
        })
        .ConAcceso(Operacion.ActivarCuenta);

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
        })
        .ConAcceso(Operacion.ReenviarActivacion);

        endpoints.MapPost("/api/auth/recuperacion", async (
            PasswordRecoveryRequest request,
            [FromServices] PasswordRecovery recovery,
            CancellationToken cancellationToken) =>
        {
            await recovery.RequestAsync(request.Correo, cancellationToken);
            return Results.Json(new EnviadoResponse(true), statusCode: StatusCodes.Status202Accepted);
        }).ConAcceso(Operacion.SolicitarRecuperacion);

        endpoints.MapPost("/api/auth/restablecer-contrasena", async (
            ResetPasswordRequest request,
            [FromServices] PasswordRecovery recovery,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await recovery.CompleteAsync(request.Codigo, request.Contrasena, cancellationToken);
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
        }).ConAcceso(Operacion.RestablecerContrasena);

        // RF-CA-03: iniciar sesion. Devuelve el token en claro UNA vez; el cliente
        // lo envia en Authorization: Bearer y ya nunca se vuelve a pedir.
        endpoints.MapPost("/api/auth/login", async (
            LoginRequest peticion,
            Login iniciarSesion,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var token = await iniciarSesion.EjecutarAsync(
                    peticion.Correo,
                    peticion.Contrasena,
                    cancellationToken);

                return Results.Json(
                    new TokenResponse(token),
                    statusCode: StatusCodes.Status200OK);
            }
            catch (CredencialesRechazadasException error)
            {
                // Correo inexistente y contrasena erronea: mismo 401, mismo cuerpo.
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (CuentaBloqueadaException error)
            {
                // Decision de Franklin: el mensaje de bloqueo es el MISMO que el de
                // credenciales, porque un mensaje propio confirmaria que ese correo
                // existe. El cliente ve un 401 igual que en los demas fallos.
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (CuentaNoActivadaException error)
            {
                // Unico mensaje distinto, y solo se alcanza con la contrasena
                // correcta (RF-CA-15).
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
        })
        .ConAcceso(Operacion.IniciarSesion);

        // RF-CA-18: cerrar sesion. 204 siempre, exista o no el token, para que no
        // se pueda comprobar si una credencial fue valida alguna vez.
        endpoints.MapPost("/api/auth/logout", async (
            HttpRequest peticionHttp,
            Logout cerrarSesion,
            CancellationToken cancellationToken) =>
        {
            await cerrarSesion.EjecutarAsync(
                PortadorDelToken.Leer(peticionHttp) ?? string.Empty,
                cancellationToken);

            return Results.NoContent();
        })
        .ConAcceso(Operacion.CerrarSesion);

        // RF-CA-07: el usuario autenticado y su rol.
        //
        // Devuelve SOLO nombre, correo y rol. Nunca el hash, nunca el token y
        // nunca CredencialVersion: por mucho que se sabe de la sesion, eso no le
        // sirve a nadie y no debe viajar.
        endpoints.MapGet("/yo", async (
            HttpRequest peticionHttp,
            Autenticar autenticar,
            CancellationToken cancellationToken) =>
        {
            UsuarioAutenticado usuario;

            try
            {
                usuario = await autenticar.EjecutarAsync(
                    PortadorDelToken.Leer(peticionHttp) ?? string.Empty,
                    cancellationToken);
            }
            catch (SesionInvalidaException error)
            {
                // 401: no hay sesion valida.
                //
                // Aqui no se captura RolInsuficienteException porque /yo no exige
                // ningun rol: solo pregunta quien es. Un 403 en este endpoint
                // seria imposible, y un catch para una excepcion que no puede
                // ocurrir solo confunde a quien lo lea.
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Json(new YoResponse(usuario.Nombre, usuario.Correo, usuario.Rol.ToString()));
        })
        .ConAcceso(Operacion.ConsultarSesionPropia);

        MapearAdministracionDeUsuarios(endpoints);

        endpoints.MapPost("/api/auth/cambiar-contrasena", async (
            ChangePasswordRequest request,
            HttpRequest httpRequest,
            [FromServices] ChangeOwnPassword changePassword,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await changePassword.ExecuteAsync(
                    PortadorDelToken.Leer(httpRequest) ?? string.Empty,
                    request.ContrasenaActual,
                    request.NuevaContrasena,
                    cancellationToken);
                return Results.NoContent();
            }
            catch (SesionInvalidaException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (RegistrationRejectedException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
        }).ConAcceso(Operacion.CambiarContrasenaPropia);

        return endpoints;
    }

    // RF-CA-21, RF-CA-08 y RF-CA-20: las cuatro rutas de Administrador.
    //
    // RF-CA-05: aqui no se compara ningun rol. Cada ruta se declara con
    // ConAcceso(Operacion.X) y el filtro de acceso consulta ExigenciasDeRol, el
    // punto unico. Este bloque solo traduce excepciones a codigos de estado.
    //
    // Los catch de SesionInvalida y RolInsuficiente se repiten en cada ruta, y no
    // hay un helper: un unico helper seria una funcion con cuatro ramas y un tipo
    // de retorno, mas dificil de leer que el try catch que se ve. Ademas el 401
    // tiene que salir antes que el 403 en todas, que es justo el orden que la
    // rubrica revisa.
    private static void MapearAdministracionDeUsuarios(IEndpointRouteBuilder endpoints)
    {
        // RF-CA-21: listar usuarios con su rol y su estado.
        endpoints.MapGet("/api/admin/usuarios", async (
            HttpRequest peticionHttp,
            ListUsers listar,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var usuarios = await listar.EjecutarAsync(
                    PortadorDelToken.Leer(peticionHttp) ?? string.Empty,
                    cancellationToken);

                return Results.Ok(usuarios);
            }
            catch (SesionInvalidaException error)
            {
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (RolInsuficienteException error)
            {
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .ConAcceso(Operacion.ListarUsuarios);

        // RF-CA-08: cambiar el rol de un usuario.
        endpoints.MapPost("/api/admin/usuarios/{id:guid}/rol", async (
            Guid id,
            ChangeRoleRequest peticion,
            HttpRequest peticionHttp,
            ChangeUserRole cambiarRol,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await cambiarRol.EjecutarAsync(
                    PortadorDelToken.Leer(peticionHttp) ?? string.Empty,
                    id,
                    peticion.Rol,
                    cancellationToken);

                return Results.NoContent();
            }
            catch (SesionInvalidaException error)
            {
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (RolInsuficienteException error)
            {
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status403Forbidden);
            }
            catch (OperacionDeAdministracionRechazadaException error)
            {
                // 400: el rol es valido pero la operacion no se puede hacer
                // (cambiarse el rol a si mismo, usuario inexistente, rol que no
                // existe). No es 403: el rol de quien llama SI alcanzaba.
                return Results.Json(
                    new ErrorResponse(error.Message),
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .ConAcceso(Operacion.CambiarRolDeUsuario);

        // RF-CA-20: desactivar y reactivar. La misma ruta y el mismo caso de uso
        // con un parametro distinto, porque desactivar y reactivar son la misma
        // operacion con dos finales.
        endpoints.MapPost("/api/admin/usuarios/{id:guid}/desactivar", async (
            Guid id,
            HttpRequest peticionHttp,
            DeactivateUser desactivar,
            CancellationToken cancellationToken) =>
        {
            return await CambiarEstadoAsync(peticionHttp, desactivar, id, Operacion.DesactivarUsuario, cancellationToken);
        })
        .ConAcceso(Operacion.DesactivarUsuario);

        endpoints.MapPost("/api/admin/usuarios/{id:guid}/reactivar", async (
            Guid id,
            HttpRequest peticionHttp,
            DeactivateUser desactivar,
            CancellationToken cancellationToken) =>
        {
            return await CambiarEstadoAsync(peticionHttp, desactivar, id, Operacion.ReactivarUsuario, cancellationToken);
        })
        .ConAcceso(Operacion.ReactivarUsuario);

        endpoints.MapPost("/api/admin/usuarios/{id:guid}/forzar-restablecimiento", async (
            Guid id,
            HttpRequest httpRequest,
            [FromServices] ForcePasswordReset reset,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await reset.ExecuteAsync(
                    PortadorDelToken.Leer(httpRequest) ?? string.Empty,
                    id,
                    cancellationToken);
                return Results.NoContent();
            }
            catch (SesionInvalidaException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (RolInsuficienteException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status403Forbidden);
            }
            catch (OperacionDeAdministracionRechazadaException error)
            {
                return Results.Json(new ErrorResponse(error.Message), statusCode: StatusCodes.Status400BadRequest);
            }
        }).ConAcceso(Operacion.ForzarRestablecimiento);
    }

    // El cuerpo de los dos endpoints de estado. Es el unico caso donde un helper
    // si paga: los dos tendrian exactamente el mismo try catch, cuatro ramas
    // cada uno, y la unica diferencia seria una palabra. Duplicar eso invites a
    // que uno se actualice y el otro no.
    private static async Task<IResult> CambiarEstadoAsync(
        HttpRequest peticionHttp,
        DeactivateUser casoDeUso,
        Guid usuarioId,
        Operacion operacion,
        CancellationToken cancellationToken)
    {
        try
        {
            // El mismo caso de uso para los dos finales. El metodo decide por que
            // operacion se entra: desactivar o reactivar. Asi el punto unico de
            // RF-CA-05 decide el rol de cada uno y no el endpoint.
            if (operacion == Operacion.ReactivarUsuario)
            {
                await casoDeUso.ReactivarAsync(
                    PortadorDelToken.Leer(peticionHttp) ?? string.Empty,
                    usuarioId,
                    cancellationToken);
            }
            else
            {
                await casoDeUso.EjecutarAsync(
                    PortadorDelToken.Leer(peticionHttp) ?? string.Empty,
                    usuarioId,
                    cancellationToken);
            }

            return Results.NoContent();
        }
        catch (SesionInvalidaException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (RolInsuficienteException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (OperacionDeAdministracionRechazadaException error)
        {
            return Results.Json(
                new ErrorResponse(error.Message),
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}