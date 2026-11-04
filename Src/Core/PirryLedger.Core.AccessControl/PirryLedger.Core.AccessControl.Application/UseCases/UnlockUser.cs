using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-19 y docs/adr/013-admin-created-users.md, decision 6: un Administrator
// desbloquea una cuenta que quedo bloqueada por cinco intentos fallidos, sin
// esperar los quince minutos.
//
// Dos decisiones que no son obvias:
//
// 1. Es idempotente. Desbloquear dos veces devuelve 204 las dos veces, porque el
//    Administrator pide un estado y el estado ya era el pedido. No se comprueba
//    si estaba bloqueada: preguntarlo antes solo anadiria una condicion de
//    carrera y un rechazo que no aporta nada.
// 2. No invalida sesiones. No toca el hash ni CredentialVersion: una cuenta
//    bloqueada no tiene sesiones validas que invalidar, y una desbloqueada que
//    tenia una sesion abierta la conserva.
public sealed class UnlockUser
{
    private readonly IUserRepository _users;
    private readonly Authenticate _authenticate;

    public UnlockUser(IUserRepository users, Authenticate authenticate)
    {
        _users = users;
        _authenticate = authenticate;
    }

    public async Task ExecuteAsync(
        string plainToken,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authenticate.ExecuteWithRoleAsync(
            plainToken,
            RoleRequirements.RequiredRole(Operation.UnlockUser),
            cancellationToken);

        var user = await _users.FindByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new AdministrationOperationRejectedException("No existe ese usuario.");
        }

        user.Unlock();

        await _users.SaveAsync(user, cancellationToken);
    }
}
