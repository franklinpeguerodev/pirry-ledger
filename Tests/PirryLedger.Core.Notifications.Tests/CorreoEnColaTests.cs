using PirryLedger.Core.Notifications.Domain;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class CorreoEnColaTests
{
    private static readonly DateTime FechaCreacionUtc =
        new(2026, 10, 2, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Crear_inicia_un_correo_pendiente_sin_intentos_ni_fecha_de_envio()
    {
        var correo = CorreoEnCola.Crear(
            "ana@example.com",
            "Activacion",
            "Abre el enlace.",
            FechaCreacionUtc);

        Assert.NotEqual(Guid.Empty, correo.Id);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal(0, correo.Intentos);
        Assert.Equal(FechaCreacionUtc, correo.FechaCreacionUtc);
        Assert.Null(correo.FechaEnvioUtc);
        Assert.Null(correo.UltimoError);
    }

    [Fact]
    public void Crear_rechaza_un_destinatario_vacio()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CorreoEnCola.Crear(" ", "Asunto", "Cuerpo", FechaCreacionUtc));

        Assert.Equal("destinatario", exception.ParamName);
    }

    [Fact]
    public void Un_correo_enviado_no_se_puede_reclamar_otra_vez()
    {
        var correo = CrearCorreo();
        correo.Reclamar();
        correo.MarcarEnviado(FechaCreacionUtc.AddMinutes(1));

        var exception = Assert.Throws<InvalidOperationException>(() => correo.Reclamar());

        Assert.Contains("Pendiente", exception.Message);
        Assert.Equal(EstadoCorreo.Enviado, correo.Estado);
        Assert.Equal(1, correo.Intentos);
    }

    private static CorreoEnCola CrearCorreo() =>
        CorreoEnCola.Crear(
            "ana@example.com",
            "Prueba",
            "Cuerpo",
            FechaCreacionUtc);
}
