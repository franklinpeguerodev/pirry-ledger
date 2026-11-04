using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-12 y docs/adr/014-password-change-permission.md.
//
// Un Standard con sesion no cambia su contrasena sin que un Administrator lo
// haya aprobado. El orden de los pasos es parte de la decision:
//
// 1. autenticar la sesion;
// 2. validar la politica de la contrasena nueva (RF-CA-14);
// 3. cargar el User y verificar la contrasena actual;
// 4. si es Standard, consumir la concesion ChangePassword;
// 5. si no habia concesion, pedirla y rechazar;
// 6. cambiar la contrasena y guardar.
//
// El paso 3 va antes del 4 a proposito: con la contrasena actual equivocada no
// se consume nada. Si se consumiera primero, un error de tecleo gastaria una
// concesion que un Administrator tuvo que aprobar.
//
// El Administrator esta exento (ADR 014, decision 6): su unica salida es no
// depender de otro Administrator, porque con uno solo el sistema se encerraria.
public sealed class ChangeOwnPassword
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly Authenticate _auth;
    private readonly IPermissionAuthority _permissions;
    private readonly IPermissionRequestGateway _requests;
    private readonly IClock _clock;

    public ChangeOwnPassword(
        IUserRepository users,
        IPasswordHasher hasher,
        Authenticate auth,
        IPermissionAuthority permissions,
        IPermissionRequestGateway requests,
        IClock clock)
    {
        _users = users;
        _hasher = hasher;
        _auth = auth;
        _permissions = permissions;
        _requests = requests;
        _clock = clock;
    }

    public async Task ExecuteAsync(
        string token,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var authenticated = await _auth.ExecuteAsync(token, cancellationToken);
        var policy = PasswordPolicy.Evaluate(newPassword);
        if (!policy.IsValid)
        {
            throw new RegistrationRejectedException(policy.Reason!);
        }

        var user = await _users.FindByIdAsync(authenticated.Id, cancellationToken)
            ?? throw new InvalidSessionException();
        if (!_hasher.Verify(currentPassword, user.PasswordHash))
        {
            throw new RegistrationRejectedException("La contrasena actual no es correcta.");
        }

        if (user.Role == Role.Standard
            && !await _permissions.ConsumeAsync(user.Id, PermissionNames.ChangePassword, cancellationToken))
        {
            // Sin concesion: se pide una y se rechaza. El mensaje distingue los
            // dos casos, igual que ADR 012 con la anulacion de facturas.
            var outcome = await _requests.RequestAsync(
                user.Id,
                PermissionNames.ChangePassword,
                cancellationToken);

            throw outcome == PermissionRequestOutcome.Created
                ? new PasswordChangeApprovalRequiredException()
                : new PasswordChangeApprovalPendingException();
        }

        user.ChangePassword(_hasher.Hash(newPassword), _clock.UtcNow);
        await _users.SaveAsync(user, cancellationToken);
    }
}
