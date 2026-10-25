using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-08: cambiar el role de un user, reservado al Administrator.
//
// Dos rechazos distintos, y la diferencia importa:
//
// - Si quien llama es un Standard, el rechazo es el 403 de RF-CA-06, y sale de
//   Authenticate.ExecuteWithRoleAsync. Un Standard no puede cambiar NINGUN role,
//   ni el suyo propio.
// - Si quien llama es Administrator pero se cambia el role a si mismo, el
//   rechazo es otro: es una operation invalida, no falta de permisos. Cambiarte
//   el role a ti mismo te deja encerrado fuera de la administracion en un solo
//   paso, sin que nadie mas lo decida.
//
// Que se pueda cambiar el propio role no es un caso forgotten por la prisa: es la
// unica forma de que un Administrator se quede sin su propio role, y a partir de
// ahi ya no puede deshacerlo.
public sealed class ChangeUserRole
{
    private readonly IUserRepository _users;
    private readonly Authenticate _authenticate;

    public ChangeUserRole(IUserRepository users, Authenticate authenticate)
    {
        _users = users;
        _authenticate = authenticate;
    }

    public async Task ExecuteAsync(
        string plainToken,
        Guid userId,
        string newRole,
        CancellationToken cancellationToken = default)
    {
        var caller = await _authenticate.ExecuteWithRoleAsync(
            plainToken,
            RoleRequirements.RequiredRole(Operation.ChangeUserRole),
            cancellationToken);

        // RD-07: el role llega como texto y se convierte aqui. El endpoint no
        // hace Enum.Parse a proposito: si lo hiciera, un JSON con "Role": "inventado"
        // lanzaria una excepcion sin manejar dentro de la capa HTTP, que es donde
        // RD-07 dice que no puede pasar.
        //
        // TryParse devuelve false en vez de lanzar, y por eso el rechazo sale
        // como un 400 controlado con este message.
        if (!Enum.TryParse(newRole, ignoreCase: true, out Role role) || !Enum.IsDefined(typeof(Role), role))
        {
            throw new AdministrationOperationRejectedException("El role indicado no existe.");
        }

        // El que cambia su propio role queda sin poder deshacerlo. Se rechaza
        // aunque sea el unico caso en que el role permite hacerlo.
        if (caller.Id == userId)
        {
            throw new AdministrationOperationRejectedException(
                "Un Administrator no puede cambiar su propio role.");
        }

        var user = await _users.FindByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new AdministrationOperationRejectedException("No existe ese usuario.");
        }

        user.ChangeRole(role);

        await _users.SaveAsync(user, cancellationToken);
    }
}
