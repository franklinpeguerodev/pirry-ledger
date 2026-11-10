using PirryLedger.Core.Notifications.Infrastructure;

namespace PirryLedger.Core.Notifications.Tests;

// ADR 019: la composicion usa una bandeja local (Mailpit) que no pide
// credenciales. Antes, una configuracion sin usuario y contrasena contaba como
// "no configurada" y el correo no se enviaba nunca.
public sealed class PruebasDeLaConfiguracionSmtp
{
    [Fact]
    public void UnServidorSinCredencialesEstaConfigurado()
    {
        var configuration = Create(user: string.Empty, password: string.Empty);

        Assert.False(configuration.HasCredentials);
        Assert.True(configuration.IsConfigured);
    }

    [Fact]
    public void UnServidorConCredencialesCompletasEstaConfigurado()
    {
        var configuration = Create(user: "cuenta@ejemplo.com", password: "secreto");

        Assert.True(configuration.HasCredentials);
        Assert.True(configuration.IsConfigured);
    }

    [Fact]
    public void UnServidorConSoloUsuarioNoEstaConfigurado()
    {
        var configuration = Create(user: "cuenta@ejemplo.com", password: string.Empty);

        Assert.True(configuration.HasCredentials);
        Assert.False(configuration.IsConfigured);
    }

    [Fact]
    public void UnServidorSinRemitenteNoEstaConfigurado()
    {
        var configuration = Create(
            user: string.Empty,
            password: string.Empty,
            from: string.Empty);

        Assert.False(configuration.IsConfigured);
    }

    [Fact]
    public void UnServidorSinNombreNoEstaConfigurado()
    {
        var configuration = Create(
            server: string.Empty,
            user: string.Empty,
            password: string.Empty);

        Assert.False(configuration.IsConfigured);
    }

    [Fact]
    public void LaConfiguracionVaciaNoEstaConfigurada()
    {
        Assert.False(SmtpConfiguration.Empty.IsConfigured);
    }

    private static SmtpConfiguration Create(
        string server = "mailpit",
        string user = "",
        string password = "",
        string from = "pirry-ledger@ejemplo.com") =>
        new(server, 1025, SslMode.None, user, password, from);
}
