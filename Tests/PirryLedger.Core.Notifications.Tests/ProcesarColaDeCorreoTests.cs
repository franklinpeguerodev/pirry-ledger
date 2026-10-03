using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class ProcesarColaDeCorreoTests
{
    private static readonly DateTime AhoraUtc =
        new(2026, 10, 2, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Envia_los_pendientes_y_no_repite_un_correo_enviado()
    {
        var correo = CrearCorreo();
        var repositorio = new RepositorioEnMemoria(correo);
        var transporte = new TransporteEnMemoria();
        var procesador = CrearProcesador(repositorio, transporte);

        var primeraEjecucion = await procesador.EjecutarAsync();
        var segundaEjecucion = await procesador.EjecutarAsync();

        Assert.Equal(1, primeraEjecucion.Pendientes);
        Assert.Equal(1, primeraEjecucion.Enviados);
        Assert.Empty(primeraEjecucion.Fallidos);
        Assert.Equal(0, segundaEjecucion.Pendientes);
        Assert.Equal(0, segundaEjecucion.Enviados);
        Assert.Empty(segundaEjecucion.Fallidos);
        Assert.Single(transporte.Enviados);
        Assert.Equal(EstadoCorreo.Enviado, correo.Estado);
        Assert.Equal(1, correo.Intentos);
        Assert.Equal(AhoraUtc, correo.FechaEnvioUtc);
        Assert.Equal(2, repositorio.Guardados);
    }

    [Fact]
    public async Task Devuelve_a_pendiente_un_correo_que_falla_y_conserva_el_error_en_el_resultado()
    {
        var correo = CrearCorreo();
        var repositorio = new RepositorioEnMemoria(correo);
        var transporte = new TransporteEnMemoria
        {
            Error = new InvalidOperationException("SMTP no disponible.")
        };
        var procesador = CrearProcesador(repositorio, transporte);

        var resultado = await procesador.EjecutarAsync();

        var fallo = Assert.Single(resultado.Fallidos);
        Assert.Equal(1, resultado.Pendientes);
        Assert.Equal(0, resultado.Enviados);
        Assert.Equal("ana@example.com", fallo.Destinatario);
        Assert.Equal("SMTP no disponible.", fallo.Error);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal(1, correo.Intentos);
        Assert.Null(correo.FechaEnvioUtc);
    }

    [Fact]
    public async Task Rechaza_un_lote_no_positivo()
    {
        var procesador = CrearProcesador(
            new RepositorioEnMemoria(),
            new TransporteEnMemoria());

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => procesador.EjecutarAsync(0));

        Assert.Equal("lote", exception.ParamName);
    }

    private static ProcesarColaDeCorreo CrearProcesador(
        RepositorioEnMemoria repositorio,
        TransporteEnMemoria transporte) =>
        new(repositorio, transporte, new RelojFijo(AhoraUtc));

    private static CorreoEnCola CrearCorreo() =>
        CorreoEnCola.Crear(
            "ana@example.com",
            "Prueba",
            "Cuerpo",
            AhoraUtc.AddMinutes(-1));

    private sealed class RepositorioEnMemoria : ICorreoEnColaRepository
    {
        private readonly List<CorreoEnCola> _correos;

        public RepositorioEnMemoria(params CorreoEnCola[] correos)
        {
            _correos = [.. correos];
        }

        public int Guardados { get; private set; }

        public Task<IReadOnlyList<CorreoEnCola>> ReclamarPendientesAsync(
            int limite,
            CancellationToken cancellationToken = default)
        {
            var pendientes = _correos
                .Where(correo => correo.Estado == EstadoCorreo.Pendiente)
                .Take(limite)
                .ToList();

            foreach (var correo in pendientes)
            {
                correo.Reclamar();
            }

            return Task.FromResult<IReadOnlyList<CorreoEnCola>>(pendientes);
        }

        public Task GuardarAsync(CancellationToken cancellationToken = default)
        {
            Guardados++;
            return Task.CompletedTask;
        }

        public Task<int> ContarPendientesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_correos.Count(correo =>
                correo.Estado == EstadoCorreo.Pendiente));
    }

    private sealed class TransporteEnMemoria : IEmailTransporter
    {
        public List<(string Destinatario, string Asunto, string Cuerpo)> Enviados { get; } = [];

        public Exception? Error { get; init; }

        public Task EnviarAsync(
            string destinatario,
            string asunto,
            string cuerpo,
            CancellationToken cancellationToken = default)
        {
            if (Error is not null)
            {
                throw Error;
            }

            Enviados.Add((destinatario, asunto, cuerpo));
            return Task.CompletedTask;
        }
    }

    private sealed class RelojFijo(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
