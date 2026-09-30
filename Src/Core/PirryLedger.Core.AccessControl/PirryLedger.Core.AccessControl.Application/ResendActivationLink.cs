using PirryLedger.Core.AccessControl.Domain;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-17: reenviar el enlace de activacion.
//
// Este caso de uso devuelve SIEMPRE lo mismo: o encola un correo nuevo y borra
// los tokens viejos, o no hace absolutamente nada. No lanza excepcion, no devuelve
// un booleano y no escribe en ningun log que correo se conoce. El endpoint, por
// tanto, tampoco puede distinguir los dos casos, porque no tiene con que.
//
// Por que importa mas de lo que parece: esta operacion es la que dice si una
// direccion de correo esta registrada en el sistema. Un "no encontramos ese
// correo" ya es una respuesta, y en un negocio con clientes reales esa respuesta
// se puede usar para listar a quien tiene cuenta.
public sealed class ResendActivationLink
{
    private readonly IUserRepository _usuarios;
    private readonly IActivationTokenRepository _tokens;
    private readonly IEmailQueue _colaDeCorreo;
    private readonly IClock _reloj;
    private readonly string _urlBase;

    public ResendActivationLink(
        IUserRepository usuarios,
        IActivationTokenRepository tokens,
        IEmailQueue colaDeCorreo,
        IClock reloj,
        string urlBase)
    {
        _usuarios = usuarios;
        _tokens = tokens;
        _colaDeCorreo = colaDeCorreo;
        _reloj = reloj;
        _urlBase = urlBase;
    }

    public async Task EjecutarAsync(
        string correo,
        TimeSpan duracionDeValidez,
        CancellationToken cancellationToken = default)
    {
        // El correo se valida igual que en el registro, pero un correo mal formado
        // no genera ninguna exception: seria otra forma de distinguir "esto no es
        // un correo" de "esto no existe en el sistema".
        if (!EmailValidator.Evaluar(correo).EsValido)
        {
            return;
        }

        var usuario = await _usuarios.BuscarPorCorreoAsync(correo.Trim(), cancellationToken);

        // Tres casos invisibles desde fuera: correo inexistente, cuenta ya activa y
        // cuenta pendiente. Los tres devuelven sin hacer nada ni decir nada.
        //
        // Si la cuenta ya esta activa, no se manda nada: el enlace viejo ya no
        // funciona y no tiene sentido reenviar uno que tampoco.
        if (usuario is null || usuario.Activo)
        {
            return;
        }

        // Decision de Franklin (RF-CA-17): el enlace anterior deja de valer. Se
        // borran los tokens sin usar y se emite uno nuevo, de modo que el correo
        // viejo y el nuevo nunca pueden ser validos a la vez.
        await _tokens.InvalidarPendientesAsync(usuario.Id, cancellationToken);

        var tokenEnClaro = OneTimeCodeGenerator.Generar();

        var token = TokenActivacion.Crear(
            usuario.Id,
            OneTimeCodeGenerator.CalcularHash(tokenEnClaro),
            _reloj.UtcNow,
            duracionDeValidez);

        await _tokens.GuardarAsync(token, cancellationToken);

        // RF-NOT-08: se encola, no se envia. La operacion no espera al servidor SMTP.
        await _colaDeCorreo.EnqueueAsync(
            new QueuedEmail(
                usuario.Correo,
                CorreoDeActivacion.Asunto,
                CorreoDeActivacion.ArmarCuerpo(_urlBase, tokenEnClaro)),
            cancellationToken);
    }
}