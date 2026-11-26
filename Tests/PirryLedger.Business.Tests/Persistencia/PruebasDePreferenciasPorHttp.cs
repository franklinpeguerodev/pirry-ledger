using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Tests.Imagenes;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Notifications.Application;

namespace PirryLedger.Business.Tests.Persistencia;

public sealed class PruebasDePreferenciasPorHttp
{
    [Fact]
    public async Task SoloAdministradorConfiguraSuPreferenciaYUnCuerpoVacioNoLaDesactiva()
    {
        var store = new Almacen();
        await using var server = new ServidorDePruebasHttp(services =>
        {
            services.AddSingleton<INotificationDeliveryStore>(store);
            services.AddSingleton<IApplicationTransaction>(new Transaccion());
            services.AddScoped<ManageNotificationPreference>();
        });
        var admin = server.TokenDeAdministrator();
        var ownAdmin = (await server.Usuarios.FindByEmailAsync("admin@ejemplo.com"))!;
        var otherAdmin = server.TokenDeAdministrator("otro-admin@ejemplo.com");
        var standard = server.TokenDeStandard();
        var client = server.Cliente;
        var path = "/api/notifications/email-preference";

        var anonymous = await client.GetAsync(path);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", standard);
        var forbidden = await client.PutAsJsonAsync(path, new { emailEnabled = false });
        var forbiddenRead = await client.GetAsync(path);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin);
        var initial = await client.GetFromJsonAsync<JsonElement>(path);
        var empty = await client.PutAsJsonAsync(path, new { });
        Assert.Empty(store.Values);
        var updated = await client.PutAsJsonAsync(path, new { emailEnabled = false, userId = Guid.NewGuid() });
        var current = await client.GetFromJsonAsync<JsonElement>(path);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherAdmin);
        var independent = await client.GetFromJsonAsync<JsonElement>(path);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);
        Assert.True(initial.GetProperty("emailEnabled").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.False(current.GetProperty("emailEnabled").GetBoolean());
        Assert.True(independent.GetProperty("emailEnabled").GetBoolean());
        Assert.Single(store.Values);
        Assert.Equal(ownAdmin.Id, store.Values.Single().Key);
    }

    private sealed class Transaccion : IApplicationTransaction
    {
        public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    }
    private sealed class Almacen : INotificationDeliveryStore
    {
        public Dictionary<Guid, bool> Values { get; } = new();
        public Task<bool> EmailEnabledAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Values.GetValueOrDefault(userId, true));
        public Task SetEmailPreferenceAsync(Guid userId, bool enabled, DateTime changedAtUtc, CancellationToken cancellationToken)
        {
            Values[userId] = enabled;
            return Task.CompletedTask;
        }
        public Task DeliverAsync(string key, NotificationRecipient recipient, string subject, string body, DateTime at, bool email, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
