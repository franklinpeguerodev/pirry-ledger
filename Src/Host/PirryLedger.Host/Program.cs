using PirryLedger.Core.Notifications.Api;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.Permissions.Api;
using PirryLedger.Core.Permissions.Infrastructure;
using PirryLedger.Core.Documents.Api;
using PirryLedger.Core.Documents.Application;
using PirryLedger.Core.Documents.Infrastructure;
using PirryLedger.Core.Documents.Infrastructure.Storage;
using PirryLedger.Business.Infrastructure;
using PirryLedger.Business.Api;
// Host: composicion y arranque. Sin logica de negocio (RD-02) y sin controllers:
// cada pieza expone los suyos (RD-01).
using DotNetEnv;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Infrastructure;
using PirryLedger.Host;

const string SendMailCommand = "--send-mail";
const string MigrateCommand = "--migrate";

// Archivo .env de desarrollo local: si existe, se cargan sus variables ANTES de
// construir la configuracion, que las lee del entorno (RD-10: al final siguen
// siendo variables de entorno, no un mecanismo distinto). NoClobber: una
// variable ya definida en la terminal o en el sistema no la toca el archivo,
// asi que un despliegue con variables reales sigue mandando. Si no hay archivo,
// no pasa nada y siguen mandando las definidas de otra forma (README, paso 4).
LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// RF-CA-21: el listado de usuarios dice el role de cada uno, y un role
// serializado como numero (0 = Administrator, 1 = Standard) no lo dice:
// depende de que el cliente sepa el orden del enum. Con este converter el
// role viaja como texto ("Administrator" / "Standard"), que es lo que dice el
// README y lo que el enunciado pide.
//
// Afecta a todos los enums que se serialicen. Hoy solo se serializa Role:
// PermissionRequestState se convierte a texto a mano en el mapeo de la pieza
// de permisos, y Operation, AccessLevel y RoleRequirements no viajan nunca
// en una respuesta.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Un solo reloj en todo el sistema (RD-11): las dos piezas reciben IClock y
// ninguna llama a DateTime.UtcNow por su cuenta.
builder.Services.AddSingleton<IClock, SystemClock>();

// Las dos piezas reciben la misma cadena de conexion: una sola base de datos,
// tablas separadas por prefijo. Ninguna de las dos ve los contextos de la otra.
var connection = ReadRequiredConnection(builder.Configuration);

var baseUrl = ReadRequiredBaseUrl(builder.Configuration);
var port = ReadRequiredPort(builder.Configuration);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddAccessControl(connection, baseUrl);
builder.Services.AddNotifications(
    connection,
    EnvironmentConfiguration.ReadSmtp(builder.Configuration) ?? SmtpConfiguration.Empty);
builder.Services.AddPermissions(connection);

// Almacenamiento de imagenes (ADR 015). Las siete variables son obligatorias:
// si falta una, la aplicacion no arranca y dice cual. Se lee antes de
// construir el contenedor para no registrar un modulo a medio configurar.
var imageStorage = EnvironmentConfiguration.ReadImageStorage(
    builder.Configuration,
    out var missingImageVariable);

if (imageStorage is null)
{
    Console.Error.WriteLine(
        $"Falta la variable de entorno {missingImageVariable}. " +
        "El README explica como definirla.");
    Environment.Exit(1);
}

// ADR 015: el limite general del cuerpo de una peticion. Kestrel acepta 30
// MB por defecto y las rutas de JSON del negocio reciben cuerpos pequenos:
// una foto de 5 MiB no cabe en el general, asi que la ruta de subida levanta
// el limite solo para ella.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024;
});

// El cuerpo de un formulario multipart tiene su propio limite: el maximo de
// la imagen mas el margen de las cabeceras del formulario.
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = imageStorage.MaxBytes + (64 * 1024);
});

builder.Services.AddBusiness(connection, imageStorage);

// Manejador de documentos (RF-DOC-01..04, ADR 017). La configuracion del
// almacenamiento es opcional: si falta, la aplicacion arranca y las operaciones
// de documentos responden 503 controlado; el resto del proyecto no se entera.
builder.Services.AddDocuments(
    connection,
    EnvironmentConfiguration.ReadDocumentStorage(builder.Configuration),
    EnvironmentConfiguration.ReadDocumentUploadOptions(builder.Configuration));

var app = builder.Build();

// ADR 018: aplica el esquema de todas las piezas y termina. Es el comando que
// ejecuta el servicio "migrate" de la composicion, antes de que arranque la
// API y antes de los seeds: el esquema primero, los datos de arranque despues.
if (args.Any(argument => string.Equals(argument, MigrateCommand, StringComparison.OrdinalIgnoreCase)))
{
    await ApplyMigrations(app);
    return;
}

await EnsureGlobalBranchSeed(app);

// Primer Administrator (docs/adr/002-first-administrator.md). Se ejecuta en el
// arranque y es idempotente: si ya hay un Administrator, no hace nada y la
// aplicacion sigue igual. Solo corre si estan las variables, para que una base
// de desarrollo sin ellas no produzca ruido en cada arranque.
await SeedFirstAdministrator(app);

// El emisor de Email es un comando, no un endpoint ni un proceso que arranque
// solo (RF-NOT-09). Se lanza cuando alguien lo lanza, y correrlo dos veces no
// duplica ningun envio (RF-NOT-12).
if (args.Any(argument => string.Equals(argument, SendMailCommand, StringComparison.OrdinalIgnoreCase)))
{
    await using var scope = app.Services.CreateAsyncScope();

    var processor = scope.ServiceProvider.GetRequiredService<ProcessEmailQueue>();
    var result = await processor.ExecuteAsync();

    Console.WriteLine($"Correos tomados: {result.Pending}");
    Console.WriteLine($"Correos enviados: {result.Sent}");

    if (result.Failures.Count == 0)
    {
        Console.WriteLine("Correos fallidos: 0");
    }
    else
    {
        Console.WriteLine($"Correos fallidos: {result.Failures.Count}");

        foreach (var failure in result.Failures)
        {
            Console.WriteLine($"  - {failure.Recipient}: {failure.Error}");
        }
    }

    if (result.Pending == 0)
    {
        Console.WriteLine("No habia correos pendientes de enviar.");
    }
    else if (result.Sent == 0)
    {
        Console.WriteLine(
            "Los correos siguen en la cola como pendientes y se volveran a intentar la proxima vez.");
    }
    else
    {
        Console.WriteLine(
            "Los correos enviados no se volveran a enviar aunque se ejecute el comando otra vez.");
    }

    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// El proxy de Render termina TLS y reenvía por HTTP a Kestrel.

// RD-07 y RD-08: la frontera de error del API. Este middleware envuelve la
// ejecucion de la ruta y convierte cualquier excepcion en un rechazo
// controlado con el formato {"message": "..."}, sin trazas, sin rutas de
// fichero y sin cabeceras de la peticion. Va fuera del filtro de acceso
// porque el cuerpo de la peticion lo lee el framework antes de los filtros:
// un JSON mal formado lo lanza por encima de ellos.
//
// El Host solo lo registra: la regla vive en la pieza que define el formato
// de error (PirryLedger.Core.AccessControl.Api). El Host es composicion.
app.UseRouting();
app.Use(ApiErrorHandling.HandleErrorsAsync);

// RF-CA-05: una ruta de negocio sin ConAcceso no se ejecuta. Va despues del
// enrutado (para que endpoint ya este resuelto) y antes de los endpoints.
app.Use(PirryLedger.Core.AccessControl.Api.AccessFilter.RequireDeclaredOperationAsync);

// RF-CA-09: limite de solicitudes por IP en los flujos que mandan correo. Va
// despues del enrutado porque lee el RequestRateLimitMetadata del endpoint
// resuelto, y antes de los endpoints para no ejecutar el caso de uso cuando ya
// se sabe que la peticion se rechaza.
app.UseMiddleware<PirryLedger.Core.AccessControl.Api.RequestRateLimitMiddleware>();

app.MapAccessControl();
app.MapNotifications();
app.MapPermissions();
app.MapBusiness();
app.MapDocuments();

app.Run();

// ADR 018: recorre los pasos de migracion registrados por las piezas y para en
// el primero que falla. El Host no conoce el DbContext de ninguna pieza: solo
// pide IModuleMigration y lo ejecuta.
static async Task ApplyMigrations(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();

    var migrations = scope.ServiceProvider.GetServices<IModuleMigration>();

    if (!migrations.Any())
    {
        Console.Error.WriteLine("No hay pasos de migracion registrados. Código: MIGRATION_MISSING.");
        Environment.Exit(1);
    }

    foreach (var migration in migrations)
    {
        try
        {
            await migration.ApplyAsync(CancellationToken.None);

            Console.WriteLine($"Migraciones aplicadas: {migration.Name}.");
        }

        catch (Exception error)
        {
            // El codigo de salida hace que la composicion no arranque la API.
            // Se escribe la pieza y un codigo corto, y el mensaje del fallo para
            // poder diagnosticarlo. Nada de esto sale en una respuesta de la
            // API (RD-08).
            Console.Error.WriteLine(
                $"No se pudieron aplicar las migraciones de {migration.Name}. " +
                $"Código: MIGRATION_FAILED. {error.Message}");

            Environment.Exit(1);
        }
    }
}

// Busca un .env desde el directorio de trabajo hacia arriba y lo carga en el
// proceso. No es un error que falte: no encontrarlo significa que las variables
// vienen de otro sitio (entorno del User o de la terminal).
static void LoadEnvFile()
{
    var directory = Directory.GetCurrentDirectory();

    while (!string.IsNullOrWhiteSpace(directory))
    {
        var file = Path.Combine(directory, ".env");

        if (File.Exists(file))
        {
            Env.NoClobber().Load(file);
            return;
        }

        directory = Directory.GetParent(directory)?.FullName;
    }
}

// Lee la cadena de conexion de ConnectionStrings__PirryLedger. Si falta, avisa
// por el Name de la variable y para: un error de configuracion no puede ser
// una excepcion con traza (RD-08).
static string ReadRequiredConnection(IConfiguration configuration)
{
    var value = configuration.GetConnectionString(DatabaseConfiguration.ConnectionName);

    if (string.IsNullOrWhiteSpace(value))
    {
        Console.Error.WriteLine(
            $"Falta la variable de entorno {DatabaseConfiguration.ConnectionVariable}. " +
            "El README explica como definirla.");
        Environment.Exit(1);
    }

    return value;
}

// La dirección pública se usa para generar los enlaces de activación.
static string ReadRequiredBaseUrl(IConfiguration configuration)
{
    var value = configuration[EnvironmentConfiguration.BaseUrlVariable];

    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
        string.IsNullOrEmpty(uri.Host) ||
        !string.IsNullOrEmpty(uri.UserInfo) ||
        !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment))
    {
        Console.Error.WriteLine(
            $"Define {EnvironmentConfiguration.BaseUrlVariable} con una URL pública HTTP o HTTPS sin credenciales, consulta ni fragmento.");
        Environment.Exit(1);
    }

    return value.Trim().TrimEnd('/');
}

static int ReadRequiredPort(IConfiguration configuration)
{
    var value = configuration[EnvironmentConfiguration.PortVariable];

    if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
    {
        Console.Error.WriteLine(
            $"Define {EnvironmentConfiguration.PortVariable} con un puerto entero entre 1 y 65535.");
        Environment.Exit(1);
    }

    return port;
}

// Si PIRRY_LEDGER_FIRST_ADMIN_EMAIL y ..._PASSWORD estan definidas, crea el
// primer Administrator. Es opcional a proposito: sin ellas no hay
// Administrator, que es justo lo que el seed viene a resolver, asi que exigir
// las variables solo molestaria a quien solo quiere registrarse.
//
// Los tres valores van por una variable cada uno y el Name no se imprime con
// su valor (RD-08, RD-10). El Name por defecto es "Administrator" para que
// basten dos variables, aunque el Name se puede cambiar por una tercera.
static async Task SeedFirstAdministrator(WebApplication app)
{
    var email = app.Configuration[EnvironmentConfiguration.FirstAdminEmailVariable];
    var password = app.Configuration[EnvironmentConfiguration.FirstAdminPasswordVariable];

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return;
    }

    var defaultName = "Administrator";
    var name = app.Configuration[EnvironmentConfiguration.FirstAdminNameVariable];

    if (string.IsNullOrWhiteSpace(name))
    {
        name = defaultName;
    }

    // Ambito propio porque IUserRepository es scoped y el caso de uso recibe un
    // IUserRepository ya resuelto.
    await using var scope = app.Services.CreateAsyncScope();

    var seeder = scope.ServiceProvider.GetRequiredService<SeedFirstAdministrator>();

    try
    {
        var created = await seeder.ExecuteAsync(name, email, password);

        // Un solo mensaje. En el caso normal no dice nada, para no ensuciar el
        // arranque de una base que ya tiene su Administrator.
        if (created)
        {
            Console.WriteLine("Se creo el primer Administrator desde las variables de entorno.");
        }
    }

    catch (FirstAdministratorNotSeededException error)
    {
        // El mensaje de la excepcion ya es apto para ver: dice el problema sin el
        // Email ni la contrasena (RD-08). No se aborta el arranque, porque la
        // aplicacion sirve igual para registrarse; lo que no habra es
        // Administrator.
        Console.Error.WriteLine($"No se pudo crear el primer Administrator: {error.Message}");
    }
}

static async Task EnsureGlobalBranchSeed(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<EnsureGlobalBranch>();
    await seeder.ExecuteAsync();
}
