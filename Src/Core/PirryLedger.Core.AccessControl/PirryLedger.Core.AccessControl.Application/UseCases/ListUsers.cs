using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;

namespace PirryLedger.Core.AccessControl.Application;

// Lo que el endpoint devuelve de cada User. RF-CA-21: "el listado nunca
// incluye hashes ni tokens".
//
// Es un record aparte de User a proposito. Si el endpoint devolviera la
// entidad, cualquier campo nuevo que se le anada a User se filtraria solo, sin
// que nadie se acuerde de revisarlo. Con este record, el hash de la contrasena no
// tiene por donde salir: no es que se esconda, es que no existe en el tipo.
public sealed record ListedUser(
    Guid Id,
    string Name,
    string Email,
    Role Role,
    bool Active,
    DateTime CreatedAtUtc);

// RF-CA-21: un Administrator lista los usuarios con su role y su estado.
//
// El role lo resuelve el punto unico de RF-CA-05, no este caso de uso: la
// Requirement esta declarada en RoleRequirements y aqui solo se pide.
public sealed class ListUsers
{
    private readonly IUserRepository _users;
    private readonly Authenticate _authenticate;

    public ListUsers(IUserRepository users, Authenticate authenticate)
    {
        _users = users;
        _authenticate = authenticate;
    }

    // Acepta el token, no el User ya validado, porque el rechazo de role tiene
    // que ocurrir dentro del caso de uso: si el endpoint llamara a
    // Authenticate por su cuenta y luego a este metodo, la Requirement de role
    // quedaria repartida entre los dos sitios (RF-CA-05).
    public async Task<IReadOnlyList<ListedUser>> ExecuteAsync(
        string plainToken,
        CancellationToken cancellationToken = default)
    {
        // Lanza InsufficientRoleException (403) si el role no alcanza, y
        // InvalidSessionException (401) si no hay Session. El endpoint traduce
        // cada una, asi que no hace falta comprobar nada mas aqui.
        await _authenticate.ExecuteWithRoleAsync(
            plainToken,
            RoleRequirements.RequiredRole(Operation.ListUsers),
            cancellationToken);

        var users = await _users.ListAsync(cancellationToken);

        // La proyeccion a ListedUser es lo que quita el hash de la
        // respuesta. Se hace aqui y no en el endpoint para que el endpoint no
        // pueda devolver la entidad por accidente.
        return users
            .Select(user => new ListedUser(
                user.Id,
                user.Name,
                user.Email,
                user.Role,
                user.Active,
                user.CreatedAtUtc))
            .ToList();
    }
}
