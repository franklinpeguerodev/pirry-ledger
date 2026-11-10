using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PirryLedger.Core.AccessControl.Api;

// RF-CA-09: limite de solicitudes por IP para los dos flujos publicos que
// mandan correo. Sin el, cualquiera puede pedir recuperaciones o reenvios en
// bucle: cada uno encola un correo y, en el caso de la recuperacion de un
// Standard, crea una solicitud de permiso.
//
// La ventana es fija, no deslizante: el contador arranca con la primera
// solicitud y se reinicia entero al cumplirse el plazo. Es lo mas simple que
// cumple el requisito y lo que se puede explicar sin ambiguedad. Una ventana
// deslizante castigaria menos a quien se equivoca al escribir su correo, pero
// guarda una marca de tiempo por solicitud y no aporta nada con este volumen
// (dos sucursales, dos o tres empleados cada una).
public sealed class RequestRateLimit
{
    // Los dos flujos que mandan correo a una direccion que el cliente elige.
    // Cinco en diez minutos: alcanza para que alguien se equivoque al escribir
    // su correo un par de veces y no alcanza para usarlo como ariete.
    public static RequestRateLimit PasswordRecovery { get; } =
        new(5, TimeSpan.FromMinutes(10));

    public static RequestRateLimit ActivationResend { get; } =
        new(5, TimeSpan.FromMinutes(10));

    // La sincronizacion de las imagenes del catalogo (ADR 015). No manda
    // correo, pero una peticion por peticion firma el catalogo entero, y una
    // sesion valida puede pedirla en bucle.
    //
    // Veinte en diez minutos: con una tablet por sucursal, la IP identifica al
    // mostrador, asi que el limite no bloquea a otro empleado de la misma
    // sucursal. Y una tablet que abre la aplicacion unas pocas veces no llega
    // a ese numero.
    public static RequestRateLimit ProductImageSync { get; } =
        new(20, TimeSpan.FromMinutes(10));

    public RequestRateLimit(int maxRequests, TimeSpan window)
    {
        if (maxRequests < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRequests));
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        MaxRequests = maxRequests;
        Window = window;
    }

    public int MaxRequests { get; }

    public TimeSpan Window { get; }
}

// El limite que declara una ruta. Vive en los metadatos del endpoint, igual que
// OperationMetadata, para que el middleware lo lea del endpoint resuelto y no
// de una lista de rutas escrita aparte que se puede desincronizar.
public sealed class RequestRateLimitMetadata(RequestRateLimit limit)
{
    public RequestRateLimit Limit { get; } = limit;
}

public static class RequestRateLimitExtensions
{
    // Se encadena despues de WithAccess: la ruta declara que operacion es y,
    // si manda correo, cuanto se puede pedir. El middleware no conoce ninguna
    // ruta por su nombre.
    public static RouteHandlerBuilder RequireRequestRateLimit(
        this RouteHandlerBuilder builder,
        RequestRateLimit limit)
    {
        builder.Add(b => b.Metadata.Add(new RequestRateLimitMetadata(limit)));

        return builder;
    }
}
