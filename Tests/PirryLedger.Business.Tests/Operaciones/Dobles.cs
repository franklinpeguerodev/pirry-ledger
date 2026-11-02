using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Ports;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Tests.Operaciones;

// RD-12: las pruebas de los casos de uso no tocan base de datos ni SMTP. El
// repositorio guarda las facturas en memoria y las mutaciones del agregado se
// ven porque devuelve la misma instancia.
internal sealed class FakeInvoiceRepository : IInvoiceRepository
{
    private readonly Dictionary<Guid, Invoice> _invoices = new();

    public int SaveCount { get; private set; }

    public void Seed(Invoice invoice) => _invoices[invoice.Id] = invoice;

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_invoices.TryGetValue(id, out var invoice) ? invoice : null);

    public Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Invoice>>(_invoices.Values.ToList());

    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _invoices[invoice.Id] = invoice;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}

// Autoridad de permisos: conoce el catalogo y lleva las concesiones en memoria.
// ConsumeAsync cuenta las llamadas para probar que Issued no pide permiso.
internal sealed class FakePermissionAuthority : IPermissionAuthority
{
    private readonly HashSet<(Guid, string)> _granted = new();

    public int ConsumeCalls { get; private set; }

    public bool IsKnown(string permissionName) =>
        PermissionNames.IsCancelInvoice(permissionName)
        || permissionName == "ViewSalesSummary";

    public Task<bool> UserHasPermissionAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_granted.Contains((userId, permissionName)));

    public Task<bool> ConsumeAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        ConsumeCalls++;
        return Task.FromResult(_granted.Remove((userId, permissionName)));
    }

    public void Grant(Guid userId, string permissionName) =>
        _granted.Add((userId, permissionName));
}

// Gateway de solicitudes (ADR 012): una Pending por (usuario, permiso).
internal sealed class FakePermissionRequestGateway : IPermissionRequestGateway
{
    private readonly HashSet<(Guid, string)> _pending = new();

    public int CreatedCount { get; private set; }

    public void SeedPending(Guid requesterId, string permissionName) =>
        _pending.Add((requesterId, permissionName));

    public Task<PermissionRequestOutcome> RequestAsync(
        Guid requesterId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        if (_pending.Contains((requesterId, permissionName)))
        {
            return Task.FromResult(PermissionRequestOutcome.AlreadyPending);
        }

        _pending.Add((requesterId, permissionName));
        CreatedCount++;
        return Task.FromResult(PermissionRequestOutcome.Created);
    }
}

internal sealed class FakeInvoiceEditedNotifier : IInvoiceEditedNotifier
{
    public int Notifications { get; private set; }

    public void Notify(Invoice invoice, Guid editedByUserId, DateTime editedAtUtc) =>
        Notifications++;
}
