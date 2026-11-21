using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Tests.Imagenes;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Domain.Entities;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDeBandejaPorHttp
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CadaRolSoloConsultaYMarcaSuBandejaConSesionValida(bool administrador)
    {
        var repository = new Memoria();
        await using var server = new ServidorDePruebasHttp(services =>
        {
            services.AddSingleton<INotificationInboxRepository>(repository);
            services.AddScoped<ManageNotificationInbox>();
        });
        var ownToken = administrador ? server.TokenDeAdministrator() : server.TokenDeStandard("propio@ejemplo.com");
        var ownUser = (await server.Usuarios.ListAsync()).Single();
        var otherToken = server.TokenDeStandard("otro@ejemplo.com");
        var otherUser = (await server.Usuarios.ListAsync()).Single(user => user.Id != ownUser.Id);
        var own = Notification.Create(ownUser.Id, "Propio", "Contenido", server.Clock.UtcNow);
        var other = Notification.Create(otherUser.Id, "Ajeno", "Contenido", server.Clock.UtcNow);
        repository.Items.AddRange([own, other]);
        var client = server.Cliente;

        var anonymous = await client.GetAsync("/api/notifications");
        var anonymousWrite = await client.PostAsync($"/api/notifications/{own.Id}/read", null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownToken);
        var listed = await client.GetFromJsonAsync<NotificationView[]>("/api/notifications");
        var foreign = await client.PostAsync($"/api/notifications/{other.Id}/read", null);
        var all = await client.PostAsync("/api/notifications/read-all", null);
        var firstRead = own.ReadAtUtc;
        server.Clock.Advance(TimeSpan.FromMinutes(1));
        var repeated = await client.PostAsync($"/api/notifications/{own.Id}/read", null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var otherView = await client.GetFromJsonAsync<NotificationView[]>("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousWrite.StatusCode);
        Assert.Equal(own.Id, Assert.Single(listed!).Id);
        Assert.Null(listed![0].ReadAtUtc);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.NotNull(firstRead);
        Assert.Equal(firstRead, own.ReadAtUtc);
        Assert.Null(other.ReadAtUtc);
        Assert.Equal(other.Id, Assert.Single(otherView!).Id);
    }

    private sealed class Memoria : INotificationInboxRepository
    {
        public List<Notification> Items { get; } = new();
        public Task<IReadOnlyList<Notification>> ListOwnAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Notification>>(Items.Where(notification => notification.RecipientUserId == userId).ToArray());
        public Task<bool> MarkReadAsync(Guid userId, Guid id, DateTime at, CancellationToken cancellationToken)
        {
            var own = Items.SingleOrDefault(notification => notification.RecipientUserId == userId && notification.Id == id);
            own?.MarkRead(at);
            return Task.FromResult(own is not null);
        }
        public Task<int> MarkAllReadAsync(Guid userId, DateTime at, CancellationToken cancellationToken)
        {
            var own = Items.Where(notification => notification.RecipientUserId == userId && notification.ReadAtUtc is null).ToArray();
            foreach (var notification in own) notification.MarkRead(at);
            return Task.FromResult(own.Length);
        }
    }
}
