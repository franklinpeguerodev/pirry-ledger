using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Api;

// RF-CA-09: el limite por IP de los flujos que mandan correo.
//
// Por que un middleware y no un filtro de endpoint: el limite no depende de la
// sesion ni del cuerpo, solo de la ruta y de la IP, y tiene que cortar antes de
// que el caso de uso encole nada. Un middleware despues del enrutado ve el
// endpoint resuelto y su RequestRateLimitMetadata, y se salta entero las rutas
// que no lo declaran.
//
// El reloj entra por IClock (RD-11) y no por DateTime.UtcNow: asi la ventana de
// diez minutos se puede recorrer en una prueba sin esperar diez minutos.
//
// Limitacion conocida y deliberada: la IP es la de la conexion. Detras de un
// proxy inverso habria que confiar en X-Forwarded-For, y hoy el despliegue es
// directo (ADR 005). Cuando haya proxy, se decide entonces y se documenta.
public sealed class RequestRateLimitMiddleware
{
    // Un solo mensaje para el 429. No dice cuantas solicitudes se permiten ni
    // cuando se reinicia la ventana: eso viaja en Retry-After, que es el dato
    // que el cliente necesita para reintentar.
    private const string RejectedMessage =
        "Demasiadas solicitudes. Intente de nuevo mas tarde.";

    // Cuantas ventanas se guardan antes de barrer las vencidas. Con el volumen
    // de Pirry (dos sucursales) no se llega nunca, pero un middleware que
    // guarda una entrada por IP sin limite es una fuga de memoria esperando.
    private const int PruneThreshold = 1024;

    private readonly RequestDelegate _next;
    private readonly Dictionary<Key, Window> _windows = [];
    private readonly object _gate = new();

    public RequestRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var limit = endpoint?.Metadata.GetMetadata<RequestRateLimitMetadata>()?.Limit;

        // Ruta sin limite declarado: no se toca nada. La mayoria de las rutas
        // caen aqui, y por eso el middleware no tiene una lista de rutas.
        if (endpoint is null || limit is null)
        {
            await _next(context);
            return;
        }

        var now = context.RequestServices.GetRequiredService<IClock>().UtcNow;
        var key = new Key(ClientKey(context), endpoint);

        TimeSpan remaining;

        // El estado es del proceso: el middleware se crea una vez y atiende
        // todas las peticiones, asi que el diccionario se toca desde varios
        // hilos. El lock es barato y el cuerpo critico es aritmetica.
        lock (_gate)
        {
            if (_windows.Count > PruneThreshold)
            {
                PruneExpired(now);
            }

            if (!_windows.TryGetValue(key, out var window)
                || now >= window.ExpiresAtUtc)
            {
                window = new Window(now + limit.Window);
                _windows[key] = window;
            }

            window.Count++;

            remaining = window.Count <= limit.MaxRequests
                ? TimeSpan.Zero
                : window.ExpiresAtUtc - now;
        }

        if (remaining == TimeSpan.Zero)
        {
            await _next(context);
            return;
        }

        await RejectAsync(context, remaining);
    }

    // La IP de la conexion. Si el servidor no la expone (no pasa con Kestrel),
    // se agrupa bajo una clave fija: mejor limitar de mas que dejar el flujo
    // abierto sin limite.
    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

    private void PruneExpired(DateTime now)
    {
        foreach (var key in _windows
            .Where(pair => now >= pair.Value.ExpiresAtUtc)
            .Select(pair => pair.Key)
            .ToList())
        {
            _windows.Remove(key);
        }
    }

    private static async Task RejectAsync(HttpContext context, TimeSpan remaining)
    {
        // Retry-After en segundos, redondeado hacia arriba y nunca menor que 1:
        // un cero diria al cliente que reintente ya, y volveria a ser rechazado.
        var seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        await context.Response.WriteAsJsonAsync(new ErrorResponse(RejectedMessage));
    }

    // La clave de la ventana es la IP mas el endpoint: la recuperacion y el
    // reenvio de activacion tienen su propio contador, y una ruta nueva que
    // declare el mismo limite tampoco lo comparte con las demas.
    private readonly record struct Key(string Client, Endpoint Endpoint);

    private sealed class Window(DateTime expiresAtUtc)
    {
        public DateTime ExpiresAtUtc { get; } = expiresAtUtc;

        public int Count { get; set; }
    }
}
