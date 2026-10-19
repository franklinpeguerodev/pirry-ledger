using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Enums;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class EmailQueueItemTests
{
    private static readonly DateTime CreatedAtUtc =
        new(2026, 10, 2, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Crear_inicia_un_correo_pendiente_sin_intentos_ni_fecha_de_envio()
    {
        var email = EmailQueueItem.Create(
            "ana@example.com",
            "Activacion",
            "Abre el enlace.",
            CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, email.Id);
        Assert.Equal(EmailStatus.Pending, email.Status);
        Assert.Equal(0, email.Attempts);
        Assert.Equal(CreatedAtUtc, email.CreatedAtUtc);
        Assert.Null(email.SentAtUtc);
        Assert.Null(email.LastError);
    }

    [Fact]
    public void Crear_rechaza_un_destinatario_vacio()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            EmailQueueItem.Create(" ", "Asunto", "Body", CreatedAtUtc));

        Assert.Equal("recipient", exception.ParamName);
    }

    [Fact]
    public void Un_correo_enviado_no_se_puede_reclamar_otra_vez()
    {
        var email = CreateEmail();
        email.Claim();
        email.MarkSent(CreatedAtUtc.AddMinutes(1));

        var exception = Assert.Throws<InvalidOperationException>(() => email.Claim());

        Assert.Contains("Pending", exception.Message);
        Assert.Equal(EmailStatus.Sent, email.Status);
        Assert.Equal(1, email.Attempts);
    }

    private static EmailQueueItem CreateEmail() =>
        EmailQueueItem.Create(
            "ana@example.com",
            "Prueba",
            "Body",
            CreatedAtUtc);
}
