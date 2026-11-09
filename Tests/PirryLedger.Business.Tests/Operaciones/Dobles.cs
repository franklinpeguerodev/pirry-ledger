using PirryLedger.Business.Application.Exceptions;
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

    public Task<Invoice?> GetByIdAsync(
        Guid id,
        Guid? branchId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            _invoices.TryGetValue(id, out var invoice)
            && (branchId is null || invoice.BranchId == branchId)
                ? invoice
                : null);

    public Task<IReadOnlyList<Invoice>> ListAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Invoice>>(
            _invoices.Values
                .Where(invoice => branchId is null || invoice.BranchId == branchId)
                .ToList());

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

    // ADR 014: concesion directa. La suite de Business no ejercita el cambio de
    // contrasena, pero el contrato crece y el doble tiene que cumplirlo.
    public Task GrantAsync(
        Guid userId,
        string permissionName,
        Guid grantedByUserId,
        CancellationToken cancellationToken = default)
    {
        _granted.Add((userId, permissionName));

        return Task.CompletedTask;
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

// Catalogo de productos en memoria (ADR 015).
internal sealed class FakeProductRepository : IProductRepository
{
    private readonly Dictionary<Guid, Product> _products = new();
    private long _revision;

    public void Seed(Product product) => _products[product.Id] = product;

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_products.TryGetValue(id, out var product) ? product : null);

    public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Product>>([.. _products.Values]);

    public Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        _products[product.Id] = product;
        return Task.CompletedTask;
    }

    // La revision del conjunto de imagenes que se lleva el cambio de estado.
    public Task<long> SetActiveAsync(
        Product product,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        _revision++;
        product.SetActive(isActive);
        product.MarkImageSetChanged(_revision);

        return Task.FromResult(_revision);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

// El historial del conjunto de imagenes en memoria (ADR 015). Lleva su propio
// contador de revisiones, como el repositorio de verdad.
internal sealed class FakeProductImageRepository : IProductImageRepository
{
    private readonly Dictionary<Guid, List<ProductImage>> _images = new();
    private long _revision;

    public bool FailOnSet { get; set; }

    public void Seed(ProductImage image) => Add(image);

    public Task<ProductImage?> FindLiveAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        ProductImage? live = null;

        if (_images.TryGetValue(productId, out var items))
        {
            live = items.LastOrDefault(image => image.IsLive);
        }

        return Task.FromResult(live);
    }

    public Task<IReadOnlyList<ProductImage>> ListLiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductImage>>(
            [.. _images.Values.SelectMany(items => items).Where(image => image.IsLive)]);

    public Task<IReadOnlyList<ProductImage>> ListUpsertsSinceAsync(long since, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductImage>>(
            [.. _images.Values.SelectMany(items => items)
                .Where(image => image.IsLive && image.RevisionAdded > since)]);

    public Task<IReadOnlyList<string>> ListRemovedKeysSinceAsync(long since, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(
            [.. _images.Values.SelectMany(items => items)
                .Where(image => !image.IsLive && image.RevisionRemoved > since)
                .Select(image => image.ImageKey)]);

    public Task<long> CurrentRevisionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_revision);

    public Task<ImageSetChange> SetLiveImageAsync(
        ProductImage image,
        DateTime retiredAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (FailOnSet)
        {
            throw new InvalidOperationException("La base no pudo guardar el cambio.");
        }

        _revision++;
        image.Enter(_revision);

        var previous = FindLiveAsync(image.ProductId).GetAwaiter().GetResult();
        string? retiredKey = null;

        if (previous is not null)
        {
            previous.Retire(_revision, retiredAtUtc);
            retiredKey = previous.ImageKey;
        }

        Add(image);

        return Task.FromResult(new ImageSetChange(_revision, retiredKey));
    }

    public Task<ImageSetChange> RetireLiveImageAsync(
        Guid productId,
        DateTime retiredAtUtc,
        CancellationToken cancellationToken = default)
    {
        _revision++;

        var live = FindLiveAsync(productId).GetAwaiter().GetResult();

        if (live is null)
        {
            return Task.FromResult(new ImageSetChange(_revision, null));
        }

        live.Retire(_revision, retiredAtUtc);

        return Task.FromResult(new ImageSetChange(_revision, live.ImageKey));
    }

    private void Add(ProductImage image)
    {
        if (!_images.TryGetValue(image.ProductId, out var items))
        {
            items = [];
            _images[image.ProductId] = items;
        }

        items.Add(image);
    }
}

// El almacenamiento de imagenes en memoria (ADR 015). Anota lo que se sube y
// lo que se borra, para comprobar el orden de las operaciones.
internal sealed class FakeImageStore : IImageStore
{
    public List<(string Key, byte[] Content, string ContentType)> Uploads { get; } = [];

    public List<string> Deleted { get; } = [];

    public bool FailOnPut { get; set; }

    public Task PutAsync(
        string key,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (FailOnPut)
        {
            throw new ImageStorageUnavailableException();
        }

        Uploads.Add((key, content, contentType));
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Deleted.Add(key);
        return Task.FromResult(true);
    }

    public string CreateDownloadUrl(string key, string contentType, DateTime expiresAtUtc) =>
        $"https://falso/{key}?firma=prueba";
}
