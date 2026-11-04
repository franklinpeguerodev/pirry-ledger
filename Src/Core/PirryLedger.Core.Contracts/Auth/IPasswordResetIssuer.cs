namespace PirryLedger.Core.Contracts.Auth;

// ADR 014: emitir el codigo de recuperacion de contrasena para un usuario.
//
// Vive en Core.Contracts porque lo necesita la pieza de Permissions: al aprobar
// una solicitud ResetPassword tiene que disparar la emision del codigo, y el
// codigo es de AccessControl. Sin este contrato, Permissions tendria que escribir
// en las tablas de AccessControl o AccessControl tendria que depender de
// Permissions.Application; las dos cosas rompen RD-03.
//
// No recibe token: quien llama ya resolvio la sesion (el Administrator que
// aprueba) y pasa la identidad del usuario destino.
public interface IPasswordResetIssuer
{
    // Invalida los codigos pendientes del usuario, genera uno nuevo de un solo
    // uso y lo encola por correo (RF-NOT-08). El codigo en claro solo existe
    // dentro del cuerpo del correo.
    Task IssueAsync(Guid userId, CancellationToken cancellationToken = default);
}
