using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-09 y ADR 014, decision 4: el atajo del Administrator cuando el usuario
// olvido su contrasena.
//
// El Administrator que llama ES la aprobacion, asi que no pasa por una solicitud:
// invalida la contrasena anterior, concede ResetPassword con GrantAsync y emite
// el codigo. Ese codigo es lo unico que puede establecer la siguiente contrasena
// utilizable.
//
// GrantAsync hace upsert por (usuario, permiso), asi que repetir force-reset no
// acumula concesiones.
public sealed class ForcePasswordReset
{
    private readonly IUserRepository _users;
    private readonly Authenticate _auth;
    private readonly IPermissionAuthority _permissions;
    private readonly IPasswordResetIssuer _issuer;
    private readonly IClock _clock;
    private readonly IPasswordHasher _hasher;

    public ForcePasswordReset(
        IUserRepository users,
        Authenticate auth,
        IPermissionAuthority permissions,
        IPasswordResetIssuer issuer,
        IClock clock,
        IPasswordHasher hasher)
    {
        _users = users;
        _auth = auth;
        _permissions = permissions;
        _issuer = issuer;
        _clock = clock;
        _hasher = hasher;
    }

    public async Task ExecuteAsync(string token, Guid userId, CancellationToken cancellationToken = default)
    {
        var caller = await _auth.ExecuteWithRoleAsync(
            token,
            RoleRequirements.RequiredRole(Operation.ForceReset),
            cancellationToken);

        var user = await _users.FindByIdAsync(userId, cancellationToken)
            ?? throw new AdministrationOperationRejectedException("No existe ese usuario.");

        // Se invalida la contrasena anterior de inmediato: el hash se reemplaza
        // por uno aleatorio que nadie conoce. Solo el codigo emitido abajo
        // permite establecer la siguiente contrasena utilizable.
        user.ChangePassword(_hasher.Hash(OneTimeCodeGenerator.Generate()), _clock.UtcNow);
        await _users.SaveAsync(user, cancellationToken);

        // La concesion va antes de emitir el codigo: el codigo se emite porque
        // la concesion existe, y reset-password la consume. Si el envio falla
        // despues, el Administrador repite force-reset.
        await _permissions.GrantAsync(user.Id, PermissionNames.ResetPassword, caller.Id, cancellationToken);

        await _issuer.IssueAsync(user.Id, cancellationToken);
    }
}
