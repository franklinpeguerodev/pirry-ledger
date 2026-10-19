using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
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
    private readonly IUserRepository _users;
    private readonly IActivationTokenRepository _tokens;
    private readonly IEmailQueue _emailQueue;
    private readonly IClock _clock;
    private readonly string _baseUrl;

    public ResendActivationLink(
        IUserRepository users,
        IActivationTokenRepository tokens,
        IEmailQueue emailQueue,
        IClock clock,
        string baseUrl)
    {
        _users = users;
        _tokens = tokens;
        _emailQueue = emailQueue;
        _clock = clock;
        _baseUrl = baseUrl;
    }

    public async Task ExecuteAsync(
        string email,
        TimeSpan validityDuration,
        CancellationToken cancellationToken = default)
    {
        // El correo se valida igual que en el registro, pero un correo mal formado
        // no genera ninguna exception: seria otra forma de distinguir "esto no es
        // un correo" de "esto no existe en el sistema".
        if (!EmailValidator.Evaluate(email).IsValid)
        {
            return;
        }

        var user = await _users.FindByEmailAsync(email.Trim(), cancellationToken);

        // Tres casos invisibles desde fuera: correo inexistente, cuenta ya activa y
        // cuenta pendiente. Los tres devuelven sin hacer nada ni decir nada.
        //
        // Si la cuenta ya esta activa, no se manda nada: el enlace viejo ya no
        // funciona y no tiene sentido reenviar uno que tampoco.
        if (user is null || user.Active)
        {
            return;
        }

        // Decision de Franklin (RF-CA-17): el enlace anterior deja de valer. Se
        // borran los tokens sin usar y se emite uno nuevo, de modo que el correo
        // viejo y el nuevo nunca pueden ser validos a la vez.
        await _tokens.InvalidatePendingAsync(user.Id, cancellationToken);

        var plainToken = OneTimeCodeGenerator.Generate();

        var token = ActivationToken.Create(
            user.Id,
            OneTimeCodeGenerator.CalculateHash(plainToken),
            _clock.UtcNow,
            validityDuration);

        await _tokens.SaveAsync(token, cancellationToken);

        // RF-NOT-08: se encola, no se envia. La operacion no espera al servidor SMTP.
        await _emailQueue.EnqueueAsync(
            new QueuedEmail(
                user.Email,
                ActivationEmail.Subject,
                ActivationEmail.BuildBody(_baseUrl, plainToken)),
            cancellationToken);
    }
}
