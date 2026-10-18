using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-18: cerrar sesion.
//
// Cierra SOLO la sesion cuyo token se presenta. Un logout no cierra las demas
// sesiones del mismo usuario: un empleado puede tener el movil y el portatil
// abiertos a la vez, y cerrar uno no deberia echarlo del otro.
//
// No lanza excepcion cuando el token no existe ni cuando la sesion ya estaba
// cerrada. El endpoint responde 204 siempre, porque un `204` condicional
// permitiria comprobar si un token fue valido alguna vez.
public sealed class Logout
{
    private readonly ISessionRepository _sessions;
    private readonly IClock _clock;

    public Logout(ISessionRepository sessions, IClock clock)
    {
        _sessions = sessions;
        _clock = clock;
    }

    public async Task ExecuteAsync(string plainToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainToken))
        {
            return;
        }

        await _sessions.CloseByHashAsync(
            SessionTokenGenerator.CalculateHash(plainToken.Trim()),
            _clock.UtcNow,
            cancellationToken);
    }
}
