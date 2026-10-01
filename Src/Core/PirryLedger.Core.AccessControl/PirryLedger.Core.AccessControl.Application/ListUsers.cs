using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Application;

// Lo que el endpoint devuelve de cada usuario. RF-CA-21: "el listado nunca
// incluye hashes ni tokens".
//
// Es un record aparte de Usuario a proposito. Si el endpoint devolviera la
// entidad, cualquier campo nuevo que se le anada a Usuario se filtraria solo, sin
// que nadie se acuerde de revisarlo. Con este record, el hash de la contrasena no
// tiene por donde salir: no es que se esconda, es que no existe en el tipo.
public sealed record UsuarioListado(
    Guid Id,
    string Nombre,
    string Correo,
    Rol Rol,
    bool Activo,
    DateTime FechaDeCreacionUtc);

// RF-CA-21: un Administrador lista los usuarios con su rol y su estado.
//
// El rol lo resuelve el punto unico de RF-CA-05, no este caso de uso: la
// exigencia esta declarada en ExigenciasDeRol y aqui solo se pide.
public sealed class ListUsers
{
    private readonly IUserRepository _usuarios;
    private readonly Autenticar _autenticar;

    public ListUsers(IUserRepository usuarios, Autenticar autenticar)
    {
        _usuarios = usuarios;
        _autenticar = autenticar;
    }

    // Acepta el token, no el usuario ya validado, porque el rechazo de rol tiene
    // que ocurrir dentro del caso de uso: si el endpoint llamara a
    // Autenticar por su cuenta y luego a este metodo, la exigencia de rol
    // quedaria repartida entre los dos sitios (RF-CA-05).
    public async Task<IReadOnlyList<UsuarioListado>> EjecutarAsync(
        string tokenEnClaro,
        CancellationToken cancellationToken = default)
    {
        // Lanza RolInsuficienteException (403) si el rol no alcanza, y
        // SesionInvalidaException (401) si no hay sesion. El endpoint traduce
        // cada una, asi que no hace falta comprobar nada mas aqui.
        await _autenticar.EjecutarConRolAsync(
            tokenEnClaro,
            ExigenciasDeRol.RolRequerido(Operacion.ListarUsuarios),
            cancellationToken);

        var usuarios = await _usuarios.ListarAsync(cancellationToken);

        // La proyeccion a UsuarioListado es lo que quita el hash de la
        // respuesta. Se hace aqui y no en el endpoint para que el endpoint no
        // pueda devolver la entidad por accidente.
        return usuarios
            .Select(usuario => new UsuarioListado(
                usuario.Id,
                usuario.Nombre,
                usuario.Correo,
                usuario.Rol,
                usuario.Activo,
                usuario.FechaDeCreacionUtc))
            .ToList();
    }
}