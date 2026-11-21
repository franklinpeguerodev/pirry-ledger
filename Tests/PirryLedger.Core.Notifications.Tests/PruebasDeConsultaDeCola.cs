using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Notifications.Application;

namespace PirryLedger.Core.Notifications.Tests;

public sealed class PruebasDeConsultaDeCola
{
    [Fact]
    public async Task EstandarNoConsultaRepositorioNiObtieneDatos()
    {
        var repository = new Consulta();
        var service = new InspectEmailQueue(repository, new Alcance());

        await Assert.ThrowsAsync<EmailQueueInspectionDeniedException>(() => service.ExecuteAsync(new EmailQueueFilter()));

        Assert.Equal(0, repository.Calls);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task FiltroInvalidoSeRechazaSinConsultar(int offset, int limit)
    {
        var repository = new Consulta();
        var service = new InspectEmailQueue(repository, new Alcance { IsGlobal = true });

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(new EmailQueueFilter(Offset: offset, Limit: limit)));

        Assert.Equal(0, repository.Calls);
    }

    private sealed class Alcance : IBranchScopeAccessor
    {
        public bool IsGlobal { get; set; }
        public Guid? UserId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
        public Guid? BranchId { get; set; }
    }
    private sealed class Consulta : IEmailQueueInspection
    {
        public int Calls { get; private set; }
        public Task<EmailQueuePage> ListAsync(EmailQueueFilter filter, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new EmailQueuePage([], 0));
        }
    }
}
