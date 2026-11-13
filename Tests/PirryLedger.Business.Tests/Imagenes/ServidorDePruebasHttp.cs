using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PirryLedger.Business.Api;
using PirryLedger.Business.Application;
using PirryLedger.Business.Application.Ports;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Ports;
using PirryLedger.Business.Tests.Operaciones;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Business.Tests.Imagenes;

// El servidor de pruebas HTTP de las rutas del catalogo (ADR 015).
//
// Levanta la aplicacion de verdad, con las rutas que produce MapBusiness y el
// filtro de acceso que produce WithAccess, pero con las persistencias
// sustituidas por dobles en memoria. No hay PostgreSQL, ni bucket, ni SMTP
// (RD-12).
//
// Lo que se prueba aqui no es un caso de uso, es la FRONTERA: que una peticion
// sin sesion reciba 401, que una peticion de un Standard reciba 403 y que el
// rechazo ocurra en el servidor, antes de que la ruta toque nada.
internal sealed class ServidorDePruebasHttp : IAsyncDisposable
{
    private static readonly DateTime Instante = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private const long MaxBytes = 5_242_880;

    private readonly WebApplication _app;

    public ServidorDePruebasHttp()
    {
        Clock = new RelojAvanzable(Instante);

        Productos = new FakeProductRepository();
        Imagenes = new FakeProductImageRepository();
        Store = new FakeImageStore();

        Usuarios = new RepositorioDeUsuariosEnMemoria();
        Sesiones = new RepositorioDeSesionesEnMemoria();

        var builder = WebApplication.CreateBuilder();

        // TestServer: el servidor vive en memoria y no ocupa un puerto.
        builder.WebHost.UseTestServer();

        // Un solo reloj para toda la aplicacion (RD-11). El middleware del
        // limite de tasa lo lee de aqui.
        builder.Services.AddSingleton<IClock>(Clock);

        // La sesion es real: el token se emite con el mismo generador y el
        // mismo SHA-256 que en produccion, y el filtro lo valida con las
        // mismas cinco condiciones.
        builder.Services.AddSingleton<ISessionRepository>(Sesiones);
        builder.Services.AddSingleton<IUserRepository>(Usuarios);
        builder.Services.AddSingleton<IPermissionAuthority>(new FakePermissionAuthority());
        builder.Services.AddScoped<IBranchScopeAccessor, BranchScopeAccessor>();
        builder.Services.AddScoped(provider => new Authenticate(
            Sesiones,
            Usuarios,
            Clock,
            provider.GetRequiredService<IPermissionAuthority>()));

        // El catalogo y sus imagenes, con los dobles de la suite.
        builder.Services.AddSingleton<IProductRepository>(Productos);
        builder.Services.AddSingleton<IProductImageRepository>(Imagenes);
        builder.Services.AddSingleton<IImageStore>(Store);
        builder.Services.AddSingleton(new ProductImageOptions(MaxBytes, 15));
        builder.Services.AddSingleton<IClassificationRepository, RepositorioDeClasificacionEnMemoria>();
        builder.Services.AddScoped<ManageClassification>();
        builder.Services.AddScoped<CreateProduct>();
        builder.Services.AddScoped<UpdateProduct>();
        builder.Services.AddScoped<SetProductActive>();
        builder.Services.AddScoped<ListProducts>();
        builder.Services.AddScoped<SetProductImage>();
        builder.Services.AddScoped<RemoveProductImage>();
        builder.Services.AddScoped<SyncProductImages>();

        // Los casos de uso de las facturas no se ejercitan en esta prueba, pero
        // Minimal API exige que el tipo de cada parametro este registrado al
        // construir la ruta. Se registran sin construir: si alguno se llegara a
        // ejecutar, la excepcion lo diria.
        foreach (var tipo in TiposDeLasRutasDeFactura())
        {
            builder.Services.AddSingleton(
                tipo,
                _ => RuntimeHelpers.GetUninitializedObject(tipo));
        }

        builder.Services.AddScoped<IBranchDirectory, DirectorioDeSucursalesEnMemoria>();

        _app = builder.Build();

        // El mismo orden de middleware que el Host (Program.cs): el
        // enrutado, la frontera de error, el filtro de acceso y el limite de
        // solicitudes por IP.
        _app.UseRouting();
        _app.Use(ApiErrorHandling.HandleErrorsAsync);
        _app.Use(AccessFilter.RequireDeclaredOperationAsync);
        _app.UseMiddleware<RequestRateLimitMiddleware>();

        _app.MapBusiness();

        _app.StartAsync().GetAwaiter().GetResult();

        Cliente = ((TestServer)_app.Services.GetRequiredService<IServer>()).CreateClient();
    }

    public HttpClient Cliente { get; }

    public RelojAvanzable Clock { get; }

    public FakeProductRepository Productos { get; }

    public FakeProductImageRepository Imagenes { get; }

    public FakeImageStore Store { get; }

    public RepositorioDeUsuariosEnMemoria Usuarios { get; }

    public RepositorioDeSesionesEnMemoria Sesiones { get; }

    // Crea un producto activo, para que la razon del rechazo sea el rol y no
    // otra cosa.
    public Product CrearProducto(string nombre = "Combo Pirri")
    {
        var producto = Product.Create(nombre, "Producto de prueba", 500m);
        Productos.Seed(producto);

        return producto;
    }

    // El token de un Administrator y el de un Standard. Las dos cuentas
    // quedan activas, con la sucursal que les corresponde y la version de
    // credencial que tiene la sesion.
    public string TokenDeAdministrator(string correo = "admin@ejemplo.com")
    {
        var usuario = User.CreateAsAdministrator(
            "Administrador",
            correo,
            "hash-que-no-se-usa-porque-aqui-no-hay-contrasena",
            Clock.UtcNow);

        return EmitirSesion(usuario);
    }

    public string TokenDeStandard(string correo = "cajero@ejemplo.com")
    {
        var usuario = User.Create(
            "Cajero",
            correo,
            "hash-que-no-se-usa-porque-aqui-no-hay-contrasena",
            Clock.UtcNow);

        usuario.AssignBranch(Guid.NewGuid());
        usuario.Activate();

        return EmitirSesion(usuario);
    }

    private string EmitirSesion(User usuario)
    {
        Usuarios.Seed(usuario);

        var token = SessionTokenGenerator.Generate();
        var hash = SessionTokenGenerator.CalculateHash(token);

        Sesiones.SaveAsync(Session.Create(
            usuario.Id,
            hash,
            Clock.UtcNow,
            TimeSpan.FromHours(8),
            usuario.CredentialVersion)).GetAwaiter().GetResult();

        return token;
    }

    private static Type[] TiposDeLasRutasDeFactura() =>
    [
        typeof(IssueInvoice),
        typeof(CollectInvoicePayment),
        typeof(LeaveInvoiceAsDebt),
        typeof(EditInvoiceItems),
        typeof(MarkInvoiceAsNotDelivered),
        typeof(CancelInvoice),
        typeof(ConfirmInvoiceTransfer),
        typeof(RejectInvoiceTransfer),
        typeof(RegisterDebtPayment),
        typeof(ListInvoices),
        typeof(GetInvoice),
        typeof(ListInvoiceEdits),
    ];

    public async ValueTask DisposeAsync()
    {
        Cliente.Dispose();
        await _app.DisposeAsync();
    }
}

// El reloj de esta prueba avanza a voluntad: la ventana del limite de
// solicitudes se recorre sin esperar diez minutos.
//
// El FakeClock de la suite es fijo a proposito, porque las pruebas de casos de
// uso no necesitan mover el tiempo. Este es otro doble para otro proposito.
internal sealed class RelojAvanzable : IClock
{
    public RelojAvanzable(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }

    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}

// Los dos repositorios de AccessControl, en memoria.
//
// Son una copia de los del proyecto de integracion: compartirlos exigiria un
// proyecto Shared, que no existe (decision de Franklin, 2026-11-09).
internal sealed class RepositorioDeUsuariosEnMemoria : IUserRepository
{
    private readonly List<User> _usuarios = [];

    public void Seed(User usuario) => _usuarios.Add(usuario);

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(usuario =>
            usuario.Email == email.Trim().ToLowerInvariant()));

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(usuario => usuario.Id == userId));

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>([.. _usuarios.OrderBy(usuario => usuario.Email)]);

    public Task SaveAsync(User user, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios.Count(usuario => usuario.Role == Role.Administrator && usuario.Active));

    public Task<int> CountActiveByBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios.Count(usuario => usuario.BranchId == branchId && usuario.Active));

    public Task<bool> AnyAdministratorExistsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios.Any(usuario => usuario.Role == Role.Administrator));
}

internal sealed class RepositorioDeSesionesEnMemoria : ISessionRepository
{
    private readonly List<Session> _sesiones = [];

    public Task SaveAsync(Session session, CancellationToken cancellationToken = default)
    {
        _sesiones.Add(session);
        return Task.CompletedTask;
    }

    public Task<Session?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sesiones.FirstOrDefault(sesion => sesion.TokenHash == tokenHash));

    public Task<bool> CloseByHashAsync(string tokenHash, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var sesion = _sesiones.FirstOrDefault(candidate => candidate.TokenHash == tokenHash);

        if (sesion is null || sesion.IsClosed)
        {
            return Task.FromResult(false);
        }

        sesion.Close(utcNow);
        return Task.FromResult(true);
    }
}

// El directorio de sucursales solo hace falta para construir la ruta de emitir
// factura, que no se ejecuta en estas pruebas.
internal sealed class DirectorioDeSucursalesEnMemoria : IBranchDirectory
{
    public Task<BranchScope?> FindAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        Task.FromResult<BranchScope?>(null);
}
