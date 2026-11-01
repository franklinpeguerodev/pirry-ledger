using Microsoft.EntityFrameworkCore;
using PirryLedger.Business.Application.UseCases;
using PirryLedger.Business.Domain.Entities;
using PirryLedger.Business.Domain.Enums;
using PirryLedger.Business.Infrastructure.Persistence;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.Contracts.Permissions;
using PirryLedger.Core.Permissions.Application.Services;
using PirryLedger.Core.Permissions.Application.UseCases;
using PirryLedger.Core.Permissions.Domain.Entities;
using PirryLedger.Core.Permissions.Domain.Enums;
using PirryLedger.Core.Permissions.Infrastructure.Persistence;
using PirryLedger.Core.Permissions.Infrastructure.Security;

namespace PirryLedger.Integration.Tests;

// Seccion 1.4 del enunciado. Compone las dos piezas reales, Business y
// Permissions, sin levantar la aplicacion y sin base de datos: cada una usa su
// DbContext real sobre EF InMemory y sus propios repositorios reales.
//
// Cada "operacion" abre un contexto nuevo, como el scope de una peticion HTTP.
// No es un detalle de estilo: CancelInvoice cambia la factura a Cancelled en
// memoria, consume la concesion y solo despues guarda; si el contexto fuera
// compartido, un rechazo podria dejar el cambio a medias y la prueba no
// distinguiria "no se guardo" de "se guardo y no lo leimos".
internal sealed class EscenarioDeIntegracion
{
    private const string ContrasenaValida = "abc12345";
    private const string CorreoAdministrador = "admin@ejemplo.com";
    private const string CorreoCajero = "cajero@ejemplo.com";

    private readonly DbContextOptions<BusinessDbContext> _opcionesNegocio;
    private readonly DbContextOptions<PermissionsDbContext> _opcionesPermisos;

    public EscenarioDeIntegracion()
    {
        Clock = new FakeClock(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));

        // Argon2id real con los mismos parametros que la aplicacion: la
        // contrasena no es un detalle simulado de la prueba.
        var hasher = new Argon2idPasswordHasher(new Argon2Parameters(
            MemoryKb: 19456,
            Iterations: 2,
            Parallelism: 1));

        Users = new InMemoryUserRepository();
        Administrador = Users.Create(
            "Admin",
            CorreoAdministrador,
            hasher.Hash(ContrasenaValida),
            Clock.UtcNow,
            active: true,
            role: Role.Administrator);
        Cajero = Users.Create(
            "Cajero",
            CorreoCajero,
            hasher.Hash(ContrasenaValida),
            Clock.UtcNow,
            active: true,
            role: Role.Standard);

        Sessions = new InMemorySessionRepository();

        // Login real: el token del Administrator que aprueba sale del mismo
        // caso de uso que en produccion, no de un token inventado.
        Login = new Login(Users, Sessions, hasher, Clock, TimeSpan.FromHours(8));

        // Las opciones se construyen una sola vez por escenario: el nombre
        // identifica la base en memoria y es lo que hace que dos contextos
        // sucesivos vean los mismos datos.
        _opcionesNegocio = new DbContextOptionsBuilder<BusinessDbContext>()
            .UseInMemoryDatabase($"negocio-{Guid.NewGuid()}")
            .Options;

        _opcionesPermisos = new DbContextOptionsBuilder<PermissionsDbContext>()
            .UseInMemoryDatabase($"permisos-{Guid.NewGuid()}")
            .Options;
    }

    public FakeClock Clock { get; }

    public InMemoryUserRepository Users { get; }

    public InMemorySessionRepository Sessions { get; }

    public Login Login { get; }

    public User Administrador { get; }

    public User Cajero { get; }

    // RF-NEG-02: emite una factura de una linea y la cobra en efectivo, para
    // que quede en Paid, que es el estado desde el que la anulacion exige
    // permiso.
    public async Task<Guid> EmitirFacturaPagadaAsync(decimal total)
    {
        await using var negocio = new BusinessDbContext(_opcionesNegocio);
        var facturas = new InvoiceRepository(negocio);

        var emitida = await new IssueInvoice(facturas, Clock).ExecuteAsync(
            new[] { new InvoiceItemInput("Combo", total, 1) },
            isDelivery: false);

        await new CollectInvoicePayment(facturas, Clock).ExecuteAsync(
            emitida.Id,
            new[] { new InvoicePaymentInput(PaymentMethod.Cash, total, total) },
            Cajero.Id);

        return emitida.Id;
    }

    // RF-NEG-06 + ADR 011 + ADR 012: la anulacion con la autoridad, la puerta de
    // solicitudes y los repositorios reales de Permissions.
    public async Task<Invoice> AnularAsync(Guid facturaId)
    {
        await using var negocio = new BusinessDbContext(_opcionesNegocio);
        await using var permisos = new PermissionsDbContext(_opcionesPermisos);

        return await CrearAnulacion(negocio, permisos).ExecuteAsync(facturaId, Cajero.Id);
    }

    // RF-GP-05: un Administrator real aprueba la solicitud pendiente de esa
    // factura. El token se emite con Login y el role lo decide Authenticate,
    // como en el endpoint.
    public async Task AprobarSolicitudDeAnulacionAsync(Guid facturaId)
    {
        var token = await Login.ExecuteAsync(CorreoAdministrador, ContrasenaValida);

        await using var permisos = new PermissionsDbContext(_opcionesPermisos);

        var solicitud = await SolicitudPendienteAsync(permisos, facturaId)
            ?? throw new InvalidOperationException(
                "No hay una solicitud pendiente de anulacion para esa factura.");

        var aprobacion = new ApprovePermissionRequest(
            new PermissionRequestRepository(permisos),
            new PermissionGrantRepository(permisos),
            new Authenticate(
                Sessions,
                Users,
                Clock,
                new EfPermissionAuthority(new PermissionGrantRepository(permisos))),
            Clock);

        await aprobacion.ExecuteAsync(token, solicitud.Id);
    }

    public async Task<InvoiceState> EstadoDeLaFacturaAsync(Guid facturaId)
    {
        await using var negocio = new BusinessDbContext(_opcionesNegocio);

        var factura = await new InvoiceRepository(negocio).GetByIdAsync(facturaId)
            ?? throw new InvalidOperationException("La factura no existe.");

        return factura.State;
    }

    // Las solicitudes de anulacion de esa factura, leidas del almacen real de
    // Permissions con un contexto nuevo.
    public async Task<IReadOnlyList<PermissionRequest>> SolicitudesAsync(Guid facturaId)
    {
        var nombre = PermissionNames.CancelInvoice(facturaId);

        await using var permisos = new PermissionsDbContext(_opcionesPermisos);

        return await permisos.PermissionRequests
            .Where(solicitud => solicitud.RequestedPermission == nombre)
            .OrderBy(solicitud => solicitud.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<int> SolicitudesPendientesAsync(Guid facturaId) =>
        (await SolicitudesAsync(facturaId))
            .Count(solicitud => solicitud.State == PermissionRequestState.Pending);

    // Las concesiones del cajero en el almacen real de Permissions. Cero
    // significa que la concesion se consumio o que nunca existio.
    public async Task<int> ConcesionesDelCajeroAsync()
    {
        await using var permisos = new PermissionsDbContext(_opcionesPermisos);

        return await permisos.PermissionGrants
            .CountAsync(concesion => concesion.UserId == Cajero.Id);
    }

    private CancelInvoice CrearAnulacion(
        BusinessDbContext negocio,
        PermissionsDbContext permisos)
    {
        var concesiones = new PermissionGrantRepository(permisos);
        var autoridad = new EfPermissionAuthority(concesiones);

        var solicitudes = new PermissionRequestRepository(permisos);
        var puerta = new PermissionRequestGateway(solicitudes, autoridad, Clock);

        return new CancelInvoice(new InvoiceRepository(negocio), autoridad, puerta);
    }

    private static Task<PermissionRequest?> SolicitudPendienteAsync(
        PermissionsDbContext permisos,
        Guid facturaId)
    {
        var nombre = PermissionNames.CancelInvoice(facturaId);

        return permisos.PermissionRequests.FirstOrDefaultAsync(solicitud =>
            solicitud.RequestedPermission == nombre
            && solicitud.State == PermissionRequestState.Pending);
    }
}
