using PirryLedger.Core.AccessControl.Domain;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-20: un Administrador desactiva y reactiva usuarios.
//
// La sesion abierta del usuario desactivado deja de servir por el mecanismo de
// CredencialVersion, sin tocar ninguna fila de Sesion: es el dominio quien sube el
// numero (Usuario.Desactivar). Este caso de uso solo guarda el cambio.
//
// Cuatro rechazos, y el orden importa:
//
// 1. Rol insuficiente: el 403 de RF-CA-06, igual que en las otras operaciones.
// 2. Desactivarse a si mismo. El enunciado lo pide explicitamente, y el motivo
//    es que un Administrador que se desactiva se queda sin su propia
//    administracion y no puede deshacerlo desde la aplicacion.
// 3. Cuenta ya inactiva. Va antes que el cuarto a proposito, para que el mensaje
//    describa lo que de verdad pasa.
// 4. Quedarse sin ningun Administrador activo. Esta regla NO la pide el
//    enunciado: se decidio a proposito el 2026-10-01, porque desactivar al
//    ultimo Administrador deja el sistema sin administracion y despues no hay
//    forma de arreglarlo por la API, solo por intervencion manual en la base.
public sealed class DeactivateUser
{
    private readonly IUserRepository _usuarios;
    private readonly Autenticar _autenticar;

    public DeactivateUser(IUserRepository usuarios, Autenticar autenticar)
    {
        _usuarios = usuarios;
        _autenticar = autenticar;
    }

    public async Task EjecutarAsync(
        string tokenEnClaro,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var quienLlama = await _autenticar.EjecutarConRolAsync(
            tokenEnClaro,
            ExigenciasDeRol.RolRequerido(Operacion.DesactivarUsuario),
            cancellationToken);

        if (quienLlama.Id == usuarioId)
        {
            throw new OperacionDeAdministracionRechazadaException(
                "Un Administrador no puede desactivar su propia cuenta.");
        }

        var usuario = await _usuarios.BuscarPorIdAsync(usuarioId, cancellationToken);

        if (usuario is null)
        {
            throw new OperacionDeAdministracionRechazadaException("No existe ese usuario.");
        }

        // Si la cuenta ya esta inactiva, se dice eso y no otra cosa.
        //
        // Esta comprobacion va ANTES del guardia del ultimo Administrador a
        // proposito. Con el orden contrario, desactivar dos veces la misma cuenta
        // respondia "no se puede desactivar al ultimo Administrador activo"
        // aunque la cuenta ya estuvera inactiva y quedara otro Administrador
        // activo: un mensaje que no describe lo que paso.
        if (!usuario.Activo)
        {
            throw new OperacionDeAdministracionRechazadaException(
                "La cuenta ya esta inactiva.");
        }

        // La cuenta del ultimo Administrador se comprueba ANTES de desactivar,
        // no despues: si se desactivara primero y el conteo saliera mal, la
        // cuenta quedaria inactiva y el sistema sin administracion, que es
        // justo lo que esta regla existe para evitar.
        //
        // Solo cuenta Administradores ACTIVOS. Un Administrador ya desactivado no
        // puede volver a entrar a reactivar a nadie, asi que no cuenta como
        // respaldo.
        //
        // Sobre su alcance: con el rechazo de auto-desactivacion, quien llama es
        // siempre un Administrador activo distinto del objetivo, asi que cuando
        // el objetivo esta activo hay al menos dos. Es decir, la regla se
        // sostiene hoy en el rechazo de auto-desactivacion y esta es la segunda
        // linea de defensa, no la que evita el problema por si sola. Se conserva
        // porque el enunciado no la pide y porque el dia que se relaje el rechazo
        // de auto-desactivacion, esta es la que impide dejar el sistema sin
        // administracion.
        if (usuario.Rol == Rol.Administrador)
        {
            var administradoresActivos = await _usuarios.ContarAdministradoresActivosAsync(cancellationToken);

            if (administradoresActivos <= 1)
            {
                throw new OperacionDeAdministracionRechazadaException(
                    "No se puede desactivar al ultimo Administrador activo de la aplicacion.");
            }
        }

        // La entidad lanza si la cuenta ya esta inactiva. Con la comprobacion de
        // arriba esto solo puede ocurrir si otra peticion la desactivo entre
        // medias, pero se captura igualmente: sin el catch seria un 500 con
        // traza (RD-07, RD-08).
        try
        {
            usuario.Desactivar();
        }
        catch (InvalidOperationException)
        {
            throw new OperacionDeAdministracionRechazadaException(
                "La cuenta ya esta inactiva.");
        }

        await _usuarios.GuardarAsync(usuario, cancellationToken);
    }

    // Reactivar es la misma operacion vista al reves, y por eso comparte caso de
    // uso. Lo que NO comparte son las reglas: reactivar no tiene los dos
    // rechazos de desactivar, porque no hay nada que romper al devolver una
    // cuenta a la vida.
    public async Task ReactivarAsync(
        string tokenEnClaro,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        await _autenticar.EjecutarConRolAsync(
            tokenEnClaro,
            ExigenciasDeRol.RolRequerido(Operacion.ReactivarUsuario),
            cancellationToken);

        var usuario = await _usuarios.BuscarPorIdAsync(usuarioId, cancellationToken);

        if (usuario is null)
        {
            throw new OperacionDeAdministracionRechazadaException("No existe ese usuario.");
        }

        // La entidad lanza si la cuenta ya esta activa. Se captura aqui y se
        // traduce al rechazo controlado de la capa de aplicacion.
        //
        // Sin este catch, la excepcion de la entidad llegaria al endpoint como un
        // 500 con traza (RD-07 y RD-08). Y no es un caso teorico: reactivar dos
        // veces seguidas, o reactivar a alguien que nunca se desactivo, es
        // exatamente lo que un revisor hace para ver si el endpoint se rompe.
        try
        {
            usuario.Reactivar();
        }
        catch (InvalidOperationException)
        {
            throw new OperacionDeAdministracionRechazadaException(
                "La cuenta ya esta activa.");
        }

        await _usuarios.GuardarAsync(usuario, cancellationToken);
    }
}