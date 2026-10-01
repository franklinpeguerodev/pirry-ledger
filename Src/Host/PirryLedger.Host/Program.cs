// Host: composicion y arranque. Sin logica de negocio (RD-02) y sin controllers:
// cada pieza expone los suyos (RD-01).
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.Contracts.Persistence;
using PirryLedger.Core.Contracts.Time;
using PirryLedger.Core.Notifications.Api;
using PirryLedger.Core.Notifications.Application;
using PirryLedger.Core.Notifications.Infrastructure;
using PirryLedger.Host;

const string ComandoEnviarCorreo = "--send-mail";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Un solo reloj en todo el sistema (RD-11): las dos piezas reciben IClock y
// ninguna llama a DateTime.UtcNow por su cuenta.
builder.Services.AddSingleton<IClock, SystemClock>();

// Las dos piezas reciben la misma cadena de conexion: una sola base de datos,
// tablas separadas por prefijo. Ninguna de las dos ve los contextos de la otra.
var conexion = LeerConexionObligatoria(builder.Configuration);

builder.Services.AddAccessControl(conexion, LeerUrlBaseObligatoria(builder.Configuration));
builder.Services.AddNotifications(
    conexion,
    ConfiguracionEntorno.LeerSmtp(builder.Configuration) ?? SmtpConfiguracion.Vacia);

var app = builder.Build();

// Primer Administrador (docs/adr/002-primer-administrador.md). Se ejecuta en el
// arranque y es idempotente: si ya hay un Administrador, no hace nada y la
// aplicacion sigue igual. Solo corre si estan las variables, para que una base
// de desarrollo sin ellas no produzca ruido en cada arranque.
await SembrarPrimerAdministrador(app);

// El emisor de correo es un comando, no un endpoint ni un proceso que arranque
// solo (RF-NOT-09). Se lanza cuando alguien lo lanza, y correrlo dos veces no
// duplica ningun envio (RF-NOT-12).
if (args.Any(argumento => string.Equals(argumento, ComandoEnviarCorreo, StringComparison.OrdinalIgnoreCase)))
{
    await using var alcance = app.Services.CreateAsyncScope();

    var procesador = alcance.ServiceProvider.GetRequiredService<ProcesarColaDeCorreo>();
    var resultado = await procesador.EjecutarAsync();

    Console.WriteLine($"Correos tomados: {resultado.Pendientes}");
    Console.WriteLine($"Correos enviados: {resultado.Enviados}");

    if (resultado.Fallidos.Count == 0)
    {
        Console.WriteLine("Correos fallidos: 0");
    }
    else
    {
        Console.WriteLine($"Correos fallidos: {resultado.Fallidos.Count}");

        foreach (var fallo in resultado.Fallidos)
        {
            Console.WriteLine($"  - {fallo.Destinatario}: {fallo.Error}");
        }
    }

    if (resultado.Pendientes == 0)
    {
        Console.WriteLine("No habia correos pendientes de enviar.");
    }
    else if (resultado.Enviados == 0)
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

app.UseHttpsRedirection();

app.MapAccessControl();
app.MapNotifications();

app.Run();

// Lee la cadena de conexion de ConnectionStrings__PirryLedger. Si falta, avisa
// por el nombre de la variable y para: un error de configuracion no puede ser
// una excepcion con traza (RD-08).
static string LeerConexionObligatoria(IConfiguration configuracion)
{
    var valor = configuracion.GetConnectionString(ConfiguracionDeBaseDeDatos.NombreDeConexion);

    if (string.IsNullOrWhiteSpace(valor))
    {
        Console.Error.WriteLine(
            $"Falta la variable de entorno {ConfiguracionDeBaseDeDatos.VariableDeConexion}. " +
            "El README explica como definirla.");
        Environment.Exit(1);
    }

    return valor;
}

// Lee PIRRY_LEDGER_PUBLIC_BASE_URL, con la que se arman los enlaces de activacion
// y de recuperacion. Sin ella los enlaces saldrian con una direccion que no
// existe, asi que es obligatoria igual que la cadena de conexion.
static string LeerUrlBaseObligatoria(IConfiguration configuracion)
{
    var valor = configuracion[ConfiguracionEntorno.VariableBaseUrl];

    if (string.IsNullOrWhiteSpace(valor))
    {
        Console.Error.WriteLine(
            $"Falta la variable de entorno {ConfiguracionEntorno.VariableBaseUrl}. " +
            "El README explica como definirla.");
        Environment.Exit(1);
    }

    return valor.TrimEnd('/');
}

// Si PIRRY_LEDGER_FIRST_ADMIN_EMAIL y ..._PASSWORD estan definidas, crea el
// primer Administrador. Es opcional a proposito: sin ellas no hay
// Administrador, que es justo lo que el seed viene a resolver, asi que exigir
// las variables solo molestaria a quien solo quiere registrarse.
//
// Los tres valores van por una variable cada uno y el nombre no se imprime con
// su valor (RD-08, RD-10). El nombre por defecto es "Administrador" para que
// basten dos variables, aunque el nombre se puede cambiar por una tercera.
static async Task SembrarPrimerAdministrador(WebApplication app)
{
    var correo = app.Configuration[ConfiguracionEntorno.VariableAdministradorCorreo];
    var contrasena = app.Configuration[ConfiguracionEntorno.VariableAdministradorContrasena];

    if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(contrasena))
    {
        return;
    }

    var nombrePorDefecto = "Administrador";
    var nombre = app.Configuration[ConfiguracionEntorno.VariableAdministradorNombre];

    if (string.IsNullOrWhiteSpace(nombre))
    {
        nombre = nombrePorDefecto;
    }

    // Ambito propio porque IUserRepository es scoped y el caso de uso recibe un
    // IUserRepository ya resuelto.
    await using var alcance = app.Services.CreateAsyncScope();

    var sembrador = alcance.ServiceProvider.GetRequiredService<SeedFirstAdministrator>();

    try
    {
        var creado = await sembrador.EjecutarAsync(nombre, correo, contrasena);

        // Un solo mensaje. En el caso normal no dice nada, para no ensuciar el
        // arranque de una base que ya tiene su Administrador.
        if (creado)
        {
            Console.WriteLine("Se creo el primer Administrador desde las variables de entorno.");
        }
    }
    catch (FirstAdministratorNotSeededException error)
    {
        // El mensaje de la excepcion ya es apto para ver: dice el problema sin el
        // correo ni la contrasena (RD-08). No se aborta el arranque, porque la
        // aplicacion sirve igual para registrarse; lo que no habra es
        // Administrador.
        Console.Error.WriteLine($"No se pudo crear el primer Administrador: {error.Message}");
    }
}
