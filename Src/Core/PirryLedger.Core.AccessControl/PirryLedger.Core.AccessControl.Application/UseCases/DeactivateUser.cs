using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-20: un Administrator desactiva y reactiva usuarios.
//
// La session abierta del user desactivado deja de servir por el mecanismo de
// CredentialVersion, sin tocar ninguna fila de session: es el dominio quien sube el
// numero (user.Deactivate). Este caso de uso solo guarda el cambio.
//
// Cuatro rechazos, y el orden importa:
//
// 1. Role insuficiente: el 403 de RF-CA-06, igual que en las otras operaciones.
// 2. Desactivarse a si mismo. El enunciado lo pide explicitamente, y el motivo
//    es que un Administrator que se desactiva se queda sin su propia
//    administracion y no puede deshacerlo desde la aplicacion.
// 3. Cuenta ya inactiva. Va antes que el cuarto a proposito, para que el message
//    describa lo que de verdad pasa.
// 4. Quedarse sin ningun Administrator Active. Esta regla NO la pide el
//    enunciado: se decidio a proposito el 2026-10-01, porque Deactivate al
//    ultimo Administrator deja el sistema sin administracion y despues no hay
//    forma de arreglarlo por la API, solo por intervencion manual en la base.
public sealed class DeactivateUser
{
    private readonly IUserRepository _users;
    private readonly Authenticate _authenticate;

    public DeactivateUser(IUserRepository users, Authenticate authenticate)
    {
        _users = users;
        _authenticate = authenticate;
    }

    public async Task ExecuteAsync(
        string plainToken,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var caller = await _authenticate.ExecuteWithRoleAsync(
            plainToken,
            RoleRequirements.RequiredRole(Operation.DeactivateUser),
            cancellationToken);

        if (caller.Id == userId)
        {
            throw new AdministrationOperationRejectedException(
                "Un Administrator no puede desactivar su propia cuenta.");
        }

        var user = await _users.FindByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new AdministrationOperationRejectedException("No existe ese usuario.");
        }

        // Si la cuenta ya esta inactiva, se dice eso y no otra cosa.
        //
        // Esta comprobacion va ANTES del guardia del ultimo Administrator a
        // proposito. Con el orden contrario, Deactivate dos veces la misma cuenta
        // respondia "no se puede desactivar al ultimo Administrator Active"
        // aunque la cuenta ya estuvera inactiva y quedara otro Administrator
        // Active: un message que no describe lo que paso.
        if (!user.Active)
        {
            throw new AdministrationOperationRejectedException(
                "La cuenta ya esta inactiva.");
        }

        // La cuenta del ultimo Administrator se comprueba ANTES de Deactivate,
        // no despues: si se desactivara primero y el conteo saliera mal, la
        // cuenta quedaria inactiva y el sistema sin administracion, que es
        // justo lo que esta regla existe para evitar.
        //
        // Solo cuenta Administradores ACTIVOS. Un Administrator ya desactivado no
        // puede volver a entrar a Reactivate a nadie, asi que no cuenta como
        // respaldo.
        //
        // Sobre su alcance: con el rechazo de auto-desactivacion, quien llama es
        // siempre un Administrator Active distinto del objetivo, asi que cuando
        // el objetivo esta Active hay al menos dos. Es decir, la regla se
        // sostiene hoy en el rechazo de auto-desactivacion y esta es la segunda
        // linea de defensa, no la que evita el problema por si sola. Se conserva
        // porque el enunciado no la pide y porque el dia que se relaje el rechazo
        // de auto-desactivacion, esta es la que impide dejar el sistema sin
        // administracion.
        if (user.Role == Role.Administrator)
        {
            var activeAdministrators = await _users.CountActiveAdministratorsAsync(cancellationToken);

            if (activeAdministrators <= 1)
            {
                throw new AdministrationOperationRejectedException(
                    "No se puede desactivar al ultimo Administrator Active de la aplicacion.");
            }
        }

        // La entidad lanza si la cuenta ya esta inactiva. Con la comprobacion de
        // arriba esto solo puede ocurrir si otra peticion la desactivo entre
        // medias, pero se captura igualmente: sin el catch seria un 500 con
        // traza (RD-07, RD-08).
        try
        {
            user.Deactivate();
        }
        catch (InvalidOperationException)
        {
            throw new AdministrationOperationRejectedException(
                "La cuenta ya esta inactiva.");
        }

        await _users.SaveAsync(user, cancellationToken);
    }

    // Reactivate es la misma operation vista al reves, y por eso comparte caso de
    // uso. Lo que NO comparte son las reglas: Reactivate no tiene los dos
    // rechazos de Deactivate, porque no hay nada que romper al devolver una
    // cuenta a la vida.
    public async Task ReactivateAsync(
        string plainToken,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authenticate.ExecuteWithRoleAsync(
            plainToken,
            RoleRequirements.RequiredRole(Operation.ReactivateUser),
            cancellationToken);

        var user = await _users.FindByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new AdministrationOperationRejectedException("No existe ese usuario.");
        }

        // La entidad lanza si la cuenta ya esta activa. Se captura aqui y se
        // traduce al rechazo controlado de la capa de aplicacion.
        //
        // Sin este catch, la excepcion de la entidad llegaria al endpoint como un
        // 500 con traza (RD-07 y RD-08). Y no es un caso teorico: Reactivate dos
        // veces seguidas, o Reactivate a alguien que nunca se desactivo, es
        // exatamente lo que un revisor hace para ver si el endpoint se rompe.
        try
        {
            user.Reactivate();
        }
        catch (InvalidOperationException)
        {
            throw new AdministrationOperationRejectedException(
                "La cuenta ya esta activa.");
        }

        await _users.SaveAsync(user, cancellationToken);
    }
}
