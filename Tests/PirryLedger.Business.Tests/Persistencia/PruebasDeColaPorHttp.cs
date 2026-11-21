using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Tests.Imagenes;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;
using PirryLedger.Core.Notifications.Infrastructure;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.Notifications.Domain.Errors;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDeColaPorHttp
{
    [Fact]
    public async Task RevalidarDestinatarioNoReutilizaAdministradorAnteriorTrasDesactivacion()
    {
        var options = new DbContextOptionsBuilder<AccessControlDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var first = new AccessControlDbContext(options);
        var now = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        var admin = User.CreateAsAdministrator("Administrador", "admin@ejemplo.com", "hash-de-prueba", now);
        first.Users.Add(admin);
        await first.SaveChangesAsync();
        var services = new ServiceCollection();
        services.AddAccessControl("Host=unused;Database=unused", "https://example.invalid");
        services.AddSingleton(first);
        await using var provider = services.BuildServiceProvider();
        var repository = provider.GetRequiredService<IUserRepository>();
        var directory = provider.GetRequiredService<PirryLedger.Core.Contracts.Notifications.INotificationRecipientDirectory>();
        await repository.FindByIdAsync(admin.Id);
        var before = await directory.FindActiveAsync(admin.Id);
        await using (var other = new AccessControlDbContext(options))
        {
            var current = await other.Users.SingleAsync(user => user.Id == admin.Id);
            current.Deactivate();
            await other.SaveChangesAsync();
        }

        var after = await directory.FindActiveAsync(admin.Id);

        Assert.NotNull(before);
        Assert.True(before.IsAdministrator);
        Assert.Null(after);
        Assert.True(first.Entry(admin).Entity.Active);
    }

    [Fact]
    public async Task AdministradorFiltraMetadatosSinCuerposYEstandarNoConsulta()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new NotificationsDbContext(options);
        var now = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        var email = EmailQueueItem.Create("prueba@ejemplo.com", "Aviso", "CUERPO PRIVADO", now);
        email.Claim(now);
        email.Release("Error heredado privado", now);
        context.EmailQueueItems.Add(email);
        await context.SaveChangesAsync();
        await using var server = new ServidorDePruebasHttp(services =>
        {
            services.AddSingleton<IEmailQueueInspection>(new EmailQueueInspection(context));
            services.AddScoped<InspectEmailQueue>();
        });
        var client = server.Cliente;
        var path = "/api/notifications/email-queue";

        var anonymous = await client.GetAsync(path);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.TokenDeStandard());
        var forbidden = await client.GetAsync(path);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.TokenDeAdministrator());
        var page = await client.GetFromJsonAsync<EmailQueuePage>(path + "?status=Pending&fromUtc=2026-11-21T00:00:00Z&limit=1");
        var absent = await client.GetFromJsonAsync<EmailQueuePage>(path + "?status=Sent");
        var invalid = await client.GetAsync(path + "?status=999");
        var badLimit = await client.GetAsync(path + "?limit=101");
        var badDates = await client.GetAsync(path + "?fromUtc=2026-11-22T00:00:00Z&toUtc=2026-11-21T00:00:00Z");
        var content = await client.GetStringAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(1, page!.Total);
        Assert.Equal(email.Id, Assert.Single(page.Items).Id);
        Assert.Equal(EmailQueueErrors.Unknown, page.Items[0].LastError);
        Assert.Equal(now, page.Items[0].LastAttemptAtUtc);
        Assert.Equal(0, absent!.Total);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badLimit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badDates.StatusCode);
        Assert.DoesNotContain("CUERPO PRIVADO", content);
        Assert.DoesNotContain("Error heredado privado", content);
        Assert.Equal(1, await context.EmailQueueItems.CountAsync());
        Assert.Equal(1, email.Attempts);
    }
}
