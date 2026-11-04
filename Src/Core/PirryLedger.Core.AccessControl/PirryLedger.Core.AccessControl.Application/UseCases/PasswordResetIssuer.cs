using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// ADR 014: emision del codigo de recuperacion. Es la implementacion del
// contrato IPasswordResetIssuer que usa la pieza de Permissions al aprobar una
// solicitud ResetPassword.
//
// Solo usa puertos (IUserRepository, IRecoveryCodeRepository, IEmailQueue) y el
// reloj: no conoce HTTP ni la pieza de Permissions. Eso es lo que permite que
// Permissions dependa del contrato y no de esta clase (RD-03).
//
// Invalida los codigos pendientes antes de emitir el nuevo: dos codigos validos a
// la vez serian dos formas de restablecer la misma cuenta.
public sealed class PasswordResetIssuer : IPasswordResetIssuer
{
    private readonly IUserRepository _users;
    private readonly IRecoveryCodeRepository _codes;
    private readonly IEmailQueue _emailQueue;
    private readonly IClock _clock;
    private readonly TimeSpan _validity;

    public PasswordResetIssuer(
        IUserRepository users,
        IRecoveryCodeRepository codes,
        IEmailQueue emailQueue,
        IClock clock,
        TimeSpan validity)
    {
        _users = users;
        _codes = codes;
        _emailQueue = emailQueue;
        _clock = clock;
        _validity = validity;
    }

    public async Task IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Un usuario que no existe es un error de programacion del llamante: la
        // solicitud aprobada apunta a un User real. Se responde con un rechazo
        // controlado y sin datos (RD-08), no con una traza.
        var user = await _users.FindByIdAsync(userId, cancellationToken)
            ?? throw new AdministrationOperationRejectedException("No existe ese usuario.");

        await _codes.InvalidatePendingAsync(user.Id, cancellationToken);

        var plainCode = OneTimeCodeGenerator.Generate();

        await _codes.SaveAsync(
            RecoveryCode.Create(
                user.Id,
                OneTimeCodeGenerator.CalculateHash(plainCode),
                _clock.UtcNow,
                _validity),
            cancellationToken);

        // RF-NOT-08: se encola y la operation termina. El codigo en claro solo
        // existe dentro del cuerpo del correo.
        await _emailQueue.EnqueueAsync(
            new QueuedEmail(
                user.Email,
                RecoveryEmail.Subject,
                RecoveryEmail.BuildBody(plainCode, _validity)),
            cancellationToken);
    }
}
