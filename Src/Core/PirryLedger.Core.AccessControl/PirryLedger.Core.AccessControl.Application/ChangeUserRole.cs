using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-08: cambiar el rol de un usuario, reservado al Administrador.
//
// Dos rechazos distintos, y la diferencia importa:
//
// - Si quien llama es un Estandar, el rechazo es el 403 de RF-CA-06, y sale de
//   Autenticar.EjecutarConRolAsync. Un Estandar no puede cambiar NINGUN rol,
//   ni el suyo propio.
// - Si quien llama es Administrador pero se cambia el rol a si mismo, el
//   rechazo es otro: es una operacion invalida, no falta de permisos. Cambiarte
//   el rol a ti mismo te deja encerrado fuera de la administracion en un solo
//   paso, sin que nadie mas lo decida.
//
// Que se pueda cambiar el propio rol no es un caso forgotten por la prisa: es la
// unica forma de que un Administrador se quede sin su propio rol, y a partir de
// ahi ya no puede deshacerlo.
public sealed class ChangeUserRole
{
    private readonly IUserRepository _usuarios;
    private readonly Autenticar _autenticar;

    public ChangeUserRole(IUserRepository usuarios, Autenticar autenticar)
    {
        _usuarios = usuarios;
        _autenticar = autenticar;
    }

    public async Task EjecutarAsync(
        string tokenEnClaro,
        Guid usuarioId,
        string nuevoRol,
        CancellationToken cancellationToken = default)
    {
        var quienLlama = await _autenticar.EjecutarConRolAsync(
            tokenEnClaro,
            ExigenciasDeRol.RolRequerido(Operacion.CambiarRolDeUsuario),
            cancellationToken);

        // RD-07: el rol llega como texto y se convierte aqui. El endpoint no
        // hace Enum.Parse a proposito: si lo hiciera, un JSON con "rol": "inventado"
        // lanzaria una excepcion sin manejar dentro de la capa HTTP, que es donde
        // RD-07 dice que no puede pasar.
        //
        // TryParse devuelve false en vez de lanzar, y por eso el rechazo sale
        // como un 400 controlado con este mensaje.
        if (!Enum.TryParse(nuevoRol, ignoreCase: true, out Rol rol) || !Enum.IsDefined(rol))
        {
            throw new OperacionDeAdministracionRechazadaException("El rol indicado no existe.");
        }

        // El que cambia su propio rol queda sin poder deshacerlo. Se rechaza
        // aunque sea el unico caso en que el rol permite hacerlo.
        if (quienLlama.Id == usuarioId)
        {
            throw new OperacionDeAdministracionRechazadaException(
                "Un Administrador no puede cambiar su propio rol.");
        }

        var usuario = await _usuarios.BuscarPorIdAsync(usuarioId, cancellationToken);

        if (usuario is null)
        {
            throw new OperacionDeAdministracionRechazadaException("No existe ese usuario.");
        }

        usuario.CambiarRol(rol);

        await _usuarios.GuardarAsync(usuario, cancellationToken);
    }
}