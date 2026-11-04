using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-09: limite de solicitudes por IP en los flujos que mandan correo.
//
// Se prueba el middleware solo, con un DefaultHttpContext y el Clock falso: no
// hace falta levantar el Host ni PostgreSQL (RD-12), y la ventana de diez
// minutos se recorre avanzando el reloj, sin esperar.
public sealed class PruebasDeLimiteDeSolicitudes
{
    private static readonly DateTime utcNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string IpA = "10.0.0.1";
    private const string IpB = "10.0.0.2";
    private const string Recovery = "/api/auth/recovery";
    private const string ResendActivation = "/api/auth/resend-activation";
    private const string Login = "/api/auth/login";

    [Fact]
    public async Task LaSextaSolicitudDeLaMismaIpSeRechazaCon429YRetryAfter()
    {
        var escenario = new Escenario();

        for (var intento = 0; intento < 5; intento++)
        {
            var permitida = await escenario.EjecutarAsync(IpA, Recovery);
            Assert.Equal(StatusCodes.Status200OK, permitida.StatusCode);
        }

        // Act
        var rechazo = await escenario.EjecutarAsync(IpA, Recovery);

        // Assert: 429 con Retry-After y el mensaje del API.
        Assert.Equal(StatusCodes.Status429TooManyRequests, rechazo.StatusCode);
        Assert.Equal("600", rechazo.Headers.RetryAfter.ToString());
        Assert.Contains("Demasiadas solicitudes", Escenario.Body(rechazo));

        // La sexta no llego al endpoint: no encolo ningun correo.
        Assert.Equal(5, escenario.Passed);
    }

    [Fact]
    public async Task LaVentanaSeReiniciaAlCumplirseLosDiezMinutos()
    {
        var escenario = new Escenario();

        for (var intento = 0; intento < 5; intento++)
        {
            await escenario.EjecutarAsync(IpA, Recovery);
        }

        await escenario.EjecutarAsync(IpA, Recovery);
        Assert.Equal(5, escenario.Passed);

        // Act: justo al cumplirse la ventana, el contador vuelve a cero.
        escenario.Clock.Advance(TimeSpan.FromMinutes(10));
        var despues = await escenario.EjecutarAsync(IpA, Recovery);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, despues.StatusCode);
        Assert.Equal(6, escenario.Passed);
    }

    [Fact]
    public async Task OtraIpNoSeVeAfectadaPorElLimiteDeLaPrimera()
    {
        var escenario = new Escenario();

        for (var intento = 0; intento < 5; intento++)
        {
            await escenario.EjecutarAsync(IpA, Recovery);
        }

        await escenario.EjecutarAsync(IpA, Recovery);

        // Act
        var otra = await escenario.EjecutarAsync(IpB, Recovery);

        // Assert: el limite es por IP, no global.
        Assert.Equal(StatusCodes.Status200OK, otra.StatusCode);
        Assert.Equal(6, escenario.Passed);
    }

    [Fact]
    public async Task ElRetryAfterCuentaLoQueQuedaDeLaVentana()
    {
        var escenario = new Escenario();

        for (var intento = 0; intento < 5; intento++)
        {
            await escenario.EjecutarAsync(IpA, Recovery);
        }

        // Act: se agotan tres de los diez minutos antes del sexto intento.
        escenario.Clock.Advance(TimeSpan.FromMinutes(3));
        var rechazo = await escenario.EjecutarAsync(IpA, Recovery);

        // Assert: quedan siete minutos, no los diez completos.
        Assert.Equal(StatusCodes.Status429TooManyRequests, rechazo.StatusCode);
        Assert.Equal("420", rechazo.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task LaRecuperacionYElReenvioTienenCadaUnoSuPropioContador()
    {
        var escenario = new Escenario();

        for (var intento = 0; intento < 5; intento++)
        {
            await escenario.EjecutarAsync(IpA, Recovery);
        }

        await escenario.EjecutarAsync(IpA, Recovery);

        // Act: la misma IP pide un reenvio de activacion.
        var reenvio = await escenario.EjecutarAsync(IpA, ResendActivation);

        // Assert: el limite de la recuperacion no consume el del reenvio.
        Assert.Equal(StatusCodes.Status200OK, reenvio.StatusCode);
        Assert.Equal(6, escenario.Passed);
    }

    [Fact]
    public async Task UnaRutaSinLimiteDeclaradoNoSeVeAfectada()
    {
        var escenario = new Escenario();

        // Act: veinte intentos a una ruta que no manda correo (login).
        for (var intento = 0; intento < 20; intento++)
        {
            var respuesta = await escenario.EjecutarAsync(IpA, Login);
            Assert.Equal(StatusCodes.Status200OK, respuesta.StatusCode);
        }

        // Assert: todas llegaron al endpoint.
        Assert.Equal(20, escenario.Passed);
    }

    // El middleware se crea una vez, como lo crea el Host, y los endpoints
    // tambien: la clave de la ventana incluye el endpoint resuelto, asi que
    // crear uno nuevo por peticion desactivaria el limite sin que se note.
    private sealed class Escenario
    {
        private readonly FakeClock _clock;
        private readonly RequestRateLimitMiddleware _middleware;
        private readonly Dictionary<string, Endpoint> _endpoints;
        private int _passed;

        public Escenario()
        {
            _clock = new FakeClock(utcNow);
            _middleware = new RequestRateLimitMiddleware(_ =>
            {
                _passed++;
                return Task.CompletedTask;
            });

            _endpoints = new Dictionary<string, Endpoint>
            {
                [Recovery] = CrearEndpoint(Recovery, RequestRateLimit.PasswordRecovery),
                [ResendActivation] = CrearEndpoint(ResendActivation, RequestRateLimit.ActivationResend),
                [Login] = CrearEndpoint(Login, limit: null),
            };
        }

        public FakeClock Clock => _clock;

        // Cuantas peticiones llegaron al endpoint (no fueron rechazadas).
        public int Passed => _passed;

        public async Task<HttpResponse> EjecutarAsync(string ip, string path)
        {
            var context = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection()
                    .AddSingleton<IClock>(_clock)
                    .BuildServiceProvider(),
            };

            context.Request.Path = path;
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            context.Response.Body = new MemoryStream();
            context.SetEndpoint(_endpoints[path]);

            await _middleware.InvokeAsync(context);

            return context.Response;
        }

        public static string Body(HttpResponse response)
        {
            response.Body.Position = 0;

            using var reader = new StreamReader(response.Body, leaveOpen: true);

            return reader.ReadToEnd();
        }

        private static Endpoint CrearEndpoint(string path, RequestRateLimit? limit)
        {
            var metadata = limit is null
                ? new EndpointMetadataCollection()
                : new EndpointMetadataCollection(new RequestRateLimitMetadata(limit));

            return new Endpoint(_ => Task.CompletedTask, metadata, path);
        }
    }
}
