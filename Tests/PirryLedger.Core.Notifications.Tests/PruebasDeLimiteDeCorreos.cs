using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class PruebasDeLimiteDeCorreos
{
    [Fact]
    public void ElQuintoFalloTerminaElCorreoSinPermitirOtroIntento()
    {
        var email = EmailQueueItem.Create("prueba@ejemplo.com", "Aviso", "Contenido",
            new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            email.Claim();
            email.Release();
        }

        Assert.Equal("Failed", email.Status.ToString());
        Assert.Equal(5, email.Attempts);
        Assert.Throws<InvalidOperationException>(() => email.Claim());
        Assert.Equal(5, email.Attempts);
        Assert.Null(email.SentAtUtc);
    }

    [Theory]
    [InlineData(EmailStatus.Sent)]
    [InlineData(EmailStatus.Uncertain)]
    [InlineData(EmailStatus.Cancelled)]
    public void EstadoTerminalRechazaNuevoIntentoSinCambios(EmailStatus final)
    {
        var now = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        var email = EmailQueueItem.ForNotification(Guid.NewGuid(), Guid.NewGuid(), "prueba@ejemplo.com", "Aviso", "Contenido", now);
        email.Claim(now);
        if (final == EmailStatus.Sent) email.MarkSent(now);
        else if (final == EmailStatus.Uncertain) email.MarkUncertain(now);
        else email.CancelNotification(now);

        Assert.Throws<InvalidOperationException>(() => email.Claim(now));

        Assert.Equal(final, email.Status);
        Assert.Equal(1, email.Attempts);
        Assert.Equal(now, email.StatusChangedAtUtc);
        Assert.Equal(final == EmailStatus.Sent ? now : (DateTime?)null, email.SentAtUtc);
    }

    [Fact]
    public void FechaSinUtcSeRechazaSinIniciarIntento()
    {
        var email = EmailQueueItem.Create("prueba@ejemplo.com", "Aviso", "Contenido",
            new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc));

        Assert.Throws<ArgumentException>(() => email.Claim(new DateTime(2026, 11, 21)));

        Assert.Equal(0, email.Attempts);
        Assert.Equal(EmailStatus.Pending, email.Status);
        Assert.Null(email.LastAttemptAtUtc);
    }
}
