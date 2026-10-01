using PirryLedger.Core.AccessControl.Domain;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// Los tres rechazos de este caso de uso. Son tipos distintos y no excepciones
// genericas porque cada uno lleva un mensaje distinto al cliente, y aun asi
// ninguno revela si el correo existe.
public sealed class CredencialesRechazadasException : Exception
{
    public CredencialesRechazadasException(string mensaje) : base(mensaje)
    {
    }
}

public sealed class CuentaBloqueadaException : Exception
{
    public CuentaBloqueadaException(string mensaje) : base(mensaje)
    {
    }
}

public sealed class CuentaNoActivadaException : Exception
{
    public CuentaNoActivadaException(string mensaje) : base(mensaje)
    {
    }
}

// RF-CA-03 y RF-CA-19. Decision de credencial en docs/adr/001-credencial-de-sesion.md.
//
// El orden de las comprobaciones es la parte importante de este archivo, y no es
// arbitrario:
//
// 1. Bloqueo. Se mira antes de verificar la contrasena porque RF-CA-19 exige que
//    el sexto intento se rechace AUN CON la contrasena correcta. Verificar el hash
//    primero daria un `401` de credenciales y no de bloqueo, que es otra respuesta.
// 2. Hash senuelo. Si el correo no existe se verifica igualmente contra un hash
//    falso, para que el tiempo de respuesta no diga si el correo esta dado de
//    alta. Sin esto, un `401` en 5 ms delata que el correo no existe.
// 3. Verificacion real de la contrasena.
// 4. Cuenta inactiva. Solo se informa DESPUES de que la contrasena sea correcta,
//    porque si se dijera antes, bastaria con conocer un correo registrado para
//    enterarse de que existe, sin saber la contrasena.
public sealed class Login
{
    // Decision de Franklin. El mensaje durante el bloqueo NO dice que la cuenta
    // esta bloqueada: confirmarlo seria decir que el correo existe, y con esto el
    // endpoint no podria distinguir "bloqueado" de "mal contrasena" ni de "no
    // existe". Se responde con el mismo mensaje de credenciales incorrectas.
    private const string MensajeDeCredenciales = "Correo o contrasena incorrectos.";
    private const string MensajeDeBloqueo = "Correo o contrasena incorrectos.";
    private const string MensajeDeCuentaInactiva = "La cuenta no esta activada.";

    private const int MaximoIntentos = 5;
    private static readonly TimeSpan DuracionDelBloqueo = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _usuarios;
    private readonly ISessionRepository _sesiones;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _reloj;
    private readonly TimeSpan _duracionDeLaSesion;

    public Login(
        IUserRepository usuarios,
        ISessionRepository sesiones,
        IPasswordHasher hasher,
        IClock reloj,
        TimeSpan duracionDeLaSesion)
    {
        _usuarios = usuarios;
        _sesiones = sesiones;
        _hasher = hasher;
        _reloj = reloj;
        _duracionDeLaSesion = duracionDeLaSesion;
    }

    // Devuelve el token EN CLARO, que es lo unico que sale hacia el cliente y lo
    // unico que no se vuelve a guardar en ningun sitio.
    public async Task<string> EjecutarAsync(string correo, string contrasena, CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.UtcNow;

        var correoNormalizado = (correo ?? string.Empty).Trim().ToLowerInvariant();
        var usuario = await _usuarios.BuscarPorCorreoAsync(correoNormalizado, cancellationToken);

        // 1. Bloqueo (RF-CA-19). Se comprueba sobre el usuario real. Un correo que
        // no existe no puede estar bloqueado, y por eso cae en el hash senuelo
        // de abajo en lugar de recibir el mensaje de bloqueo: si lo recibiera,
        // bastaria con five intentos fallidos para descubrir que el correo existe.
        if (usuario is not null && usuario.EstaBloqueado(ahora))
        {
            throw new CuentaBloqueadaException(MensajeDeBloqueo);
        }

        // 2. Hash senuelo. Se verifica SIEMPRE, exista o no el usuario, para que el
        // tiempo de respuesta sea el mismo en los dos casos.
        var hashParaVerificar = usuario?.HashDeContrasena ?? _hasher.CrearHashSenaluelo();
        var contrasenaCorrecta = _hasher.Verificar(contrasena ?? string.Empty, hashParaVerificar);

        // 3. Credenciales incorrectas. El mensaje es el mismo tanto si el correo no
        // existia como si la contrasena estaba mal, para no revelar cual de las
        // dos cosas fallo (RF-CA-03).
        if (usuario is null || !contrasenaCorrecta)
        {
            // RF-CA-19: el contador vive en el usuario, no en memoria del proceso.
            // Asi el bloqueo aguanta un reinicio de la aplicacion.
            if (usuario is not null)
            {
                usuario.RegistrarIntentoFallido(MaximoIntentos, ahora, DuracionDelBloqueo);
                await _usuarios.GuardarAsync(usuario, cancellationToken);
            }

            throw new CredencialesRechazadasException(MensajeDeCredenciales);
        }

        // 4. RF-CA-15: la cuenta tiene que estar activada. Este mensaje SÍ es
        // distinto del anterior, y solo se alcanza con la contrasena correcta.
        if (!usuario.Activo)
        {
            throw new CuentaNoActivadaException(MensajeDeCuentaInactiva);
        }

        // RF-CA-19: un acierto pone el contador en cero.
        usuario.RegistrarIntentoCorrecto();

        // Decision de Franklin: caducidad ABSOLUTA de 8 horas, no deslizante.
        var tokenEnClaro = SessionTokenGenerator.Generar();

        var sesion = Sesion.Crear(
            usuario.Id,
            SessionTokenGenerator.CalcularHash(tokenEnClaro),
            ahora,
            _duracionDeLaSesion,
            usuario.CredencialVersion);

        await _sesiones.GuardarAsync(sesion, cancellationToken);
        await _usuarios.GuardarAsync(usuario, cancellationToken);

        return tokenEnClaro;
    }
}