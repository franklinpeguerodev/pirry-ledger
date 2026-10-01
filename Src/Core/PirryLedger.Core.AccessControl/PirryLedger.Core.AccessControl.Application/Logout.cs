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
    private readonly ISessionRepository _sesiones;
    private readonly IClock _reloj;

    public Logout(ISessionRepository sesiones, IClock reloj)
    {
        _sesiones = sesiones;
        _reloj = reloj;
    }

    public async Task EjecutarAsync(string tokenEnClaro, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenEnClaro))
        {
            return;
        }

        await _sesiones.CerrarPorHashAsync(
            SessionTokenGenerator.CalcularHash(tokenEnClaro.Trim()),
            _reloj.UtcNow,
            cancellationToken);
    }
}