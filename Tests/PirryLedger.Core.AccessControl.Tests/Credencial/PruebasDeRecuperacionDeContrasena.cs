using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.Contracts.Notifications;
using Xunit;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

public sealed class PruebasDeRecuperacionDeContrasena
{
    [Fact]
    public async Task SolicitudDesconocidaYConocidaRespondenIgual()
    {
        var Clock = new FakeClock(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        var Users = new FakeUserRepository();
        var queue = new InMemoryEmailQueue();
        var caso = new PasswordRecovery(
            Users,
            new InMemoryRecoveryCodeRepository(),
            queue,
            Clock,
            new FakePasswordHasher(),
            TimeSpan.FromMinutes(15));

        await caso.RequestAsync("no-existe@ejemplo.com");
        Users.Create("Ana", "ana@ejemplo.com", "old", Clock.UtcNow, active: true);
        await caso.RequestAsync("ana@ejemplo.com");

        Assert.Single(queue.Emails);
        Assert.Contains("15 minutos", queue.Emails[0].Body);
    }

    [Fact]
    public async Task CodigoEsDeUnSoloUsoYExpiraEnQuinceMinutos()
    {
        var Clock = new FakeClock(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        var Users = new FakeUserRepository();
        var user = Users.Create("Ana", "ana@ejemplo.com", "old", Clock.UtcNow, active: true);
        var codes = new InMemoryRecoveryCodeRepository();
        var queue = new InMemoryEmailQueue();
        var caso = new PasswordRecovery(Users, codes, queue, Clock, new FakePasswordHasher(), TimeSpan.FromMinutes(15));

        await caso.RequestAsync(user.Email);
        var Code = queue.Emails[0].Body.Split('\n', StringSplitOptions.TrimEntries)
            .First(line => line.Length == OneTimeCodeGenerator.LengthInBytes * 2);

        await caso.CompleteAsync(Code, "new12345");
        await Assert.ThrowsAsync<RecoveryRejectedException>(() => caso.CompleteAsync(Code, "other123"));

        Clock.Advance(TimeSpan.FromMinutes(15));
        await Assert.ThrowsAsync<RecoveryRejectedException>(() => caso.CompleteAsync(Code, "other123"));
    }

    private sealed class InMemoryEmailQueue : IEmailQueue
    {
        public List<QueuedEmail> Emails { get; } = [];

        public Task EnqueueAsync(QueuedEmail email, CancellationToken cancellationToken = default)
        {
            Emails.Add(email);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryRecoveryCodeRepository : IRecoveryCodeRepository
    {
        private readonly List<RecoveryCode> _codes = [];

        public Task<RecoveryCode?> FindByHashAsync(string codeHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(_codes.FirstOrDefault(code => code.CodeHash == codeHash));

        public Task InvalidatePendingAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            _codes.RemoveAll(code => code.UserId == userId && !code.IsUsed);
            return Task.CompletedTask;
        }

        public Task SaveAsync(RecoveryCode code, CancellationToken cancellationToken = default)
        {
            _codes.RemoveAll(existing => existing.Id == code.Id);
            _codes.Add(code);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string password, string storedHash) => storedHash == Hash(password);

        public string CreateDummyHash() => "dummy";
    }
}
