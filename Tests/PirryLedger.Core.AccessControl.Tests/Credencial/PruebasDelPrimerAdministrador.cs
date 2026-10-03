using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// El seed del primer Administrador (docs/adr/002-primer-administrator.md).
//
// Estas pruebas cubren los tres motivos por los que un seed puede ser
// peligroso: que se ejecute dos veces, que convierta en Administrador una cuenta
// ajena por error, y que no respete la politica de contrasena.
public sealed class PruebasDelPrimerAdministrador
{
    private const string ContrasenaValida = "abc12345";
    private const string CorreoDelAdministrador = "admin@ejemplo.com";

    private static readonly DateTime Ahora = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static (SeedFirstAdministrator Sembrar, UsuarioEnMemoria Usuarios, Argon2idPasswordHasher Hasher) Escenario()
    {
        var usuarios = new UsuarioEnMemoria();
        var reloj = new RelojFalso(Ahora);

        // El Argon2id real, como en las demas pruebas: si el seed usara otro
        // hasher, estas pruebas no lo detectarian.
        var hasher = new Argon2idPasswordHasher(new ParametrosDeArgon2(
            MemoriaKb: 19456,
            Iteraciones: 2,
            Paralelismo: 1));

        var sembrar = new SeedFirstAdministrator(usuarios, hasher, reloj);

        return (sembrar, usuarios, hasher);
    }

    [Fact]
    public async Task CreaUnAdministradorActivo()
    {
        var (sembrar, usuarios, _) = Escenario();

        var creado = await sembrar.EjecutarAsync("Administrador", CorreoDelAdministrador, ContrasenaValida);

        Assert.True(creado);

        var guardado = await usuarios.BuscarPorCorreoAsync(CorreoDelAdministrador);

        Assert.NotNull(guardado);
        Assert.Equal(Rol.Administrador, guardado!.Rol);

        // Activo, al contrario que el registro. Sin enlace de activacion no hay
        // quien lo abra, y activar es operacion de Administrador.
        Assert.True(guardado.Activo);
    }

    [Fact]
    public async Task LaContrasenaQuedaHasheadaYConSalPropia()
    {
        var (sembrar, usuarios, hasher) = Escenario();

        await sembrar.EjecutarAsync("Administrador", CorreoDelAdministrador, ContrasenaValida);

        var guardado = await usuarios.BuscarPorCorreoAsync(CorreoDelAdministrador);

        // RF-CA-02: nunca la contrasena.
        Assert.NotEqual(ContrasenaValida, guardado!.HashDeContrasena);
        Assert.StartsWith("$argon2id$", guardado.HashDeContrasena);

        // Sal por usuario (RF-CA-02): dos cuentas con la MISMA contrasena no
        // pueden compartir el valor almacenado.
        //
        // La segunda cuenta se crea con el registro publico, no con el seed: el
        // seed ya corrio una vez y por idempotencia no vuelve a crear nada. La
        // comparacion es entre el hash del Administrador y el de un Estándar con
        // la misma contrasena, que es donde se ve la sal.
        usuarios.Crear("Estandar", "estandar@ejemplo.com", hasher.Hash(ContrasenaValida), Ahora, activo: true);
        var otro = await usuarios.BuscarPorCorreoAsync("estandar@ejemplo.com");

        Assert.NotEqual(guardado.HashDeContrasena, otro!.HashDeContrasena);

        // Y el hash verifica de verdad, con el mismo hasher.
        Assert.True(hasher.Verificar(ContrasenaValida, guardado.HashDeContrasena));
    }

    [Fact]
    public async Task EjecutarloDosvecesNoCreaDosAdministradores()
    {
        var (sembrar, usuarios, _) = Escenario();

        var primero = await sembrar.EjecutarAsync("Administrador", CorreoDelAdministrador, ContrasenaValida);
        var segundo = await sembrar.EjecutarAsync("Administrador", CorreoDelAdministrador, ContrasenaValida);

        // Es el requisito: arrancar la aplicacion dos veces no puede crear dos
        // Administradores.
        Assert.True(primero);
        Assert.False(segundo);

        Assert.True(await usuarios.ExisteAlgunAdministradorAsync());
        Assert.Single(await usuarios.BuscarTodosLosAdministradoresAsync());
    }

    [Fact]
    public async Task NoCreaUnSegundoAdministradorAunqueElCorreoSeaDistinto()
    {
        var (sembrar, usuarios, _) = Escenario();

        await sembrar.EjecutarAsync("Administrador", CorreoDelAdministrador, ContrasenaValida);

        // Mismo caso de uso, otro correo. La idempotencia no compara correos:
        // pregunta si ya hay un Administrador.
        var segundo = await sembrar.EjecutarAsync("Otro", "otro@ejemplo.com", ContrasenaValida);

        Assert.False(segundo);
        Assert.Null(await usuarios.BuscarPorCorreoAsync("otro@ejemplo.com"));
    }

    [Fact]
    public async Task NoPromueveUnaCuentaQueYaExiste()
    {
        var (sembrar, usuarios, _) = Escenario();

        // Alguien se registro con ese correo antes de que existiera el seed.
        usuarios.Crear("Antes", CorreoDelAdministrador, "hash-cualquiera", Ahora, activo: true);

        // Promover a un Estándar por el camino del seed seria el escalamiento que
        // RF-CA-08 quiere impedir, asi que se rechaza.
        var error = await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.EjecutarAsync("Administrador", CorreoDelAdministrador, ContrasenaValida));

        Assert.NotNull(error);

        // La cuenta sigue siendo Estándar: el seed no la toco.
        var guardado = await usuarios.BuscarPorCorreoAsync(CorreoDelAdministrador);
        Assert.Equal(Rol.Estandar, guardado!.Rol);
        Assert.Equal("hash-cualquiera", guardado.HashDeContrasena);
    }

    [Fact]
    public async Task AplicaLaPoliticaDeContrasenaDelRegistro()
    {
        var (sembrar, usuarios, _) = Escenario();

        // RF-CA-14 alcanza al seed aunque la contrasena venga de una variable de
        // entorno y no de un formulario: una contrasena debil en un
        // Administrador es peor que en un Estándar.
        await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.EjecutarAsync("Administrador", CorreoDelAdministrador, "corta"));

        // Solo letras y con longitud de sobra: la politica exige las dos cosas,
        // letras y numeros, no una de las dos.
        await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.EjecutarAsync("Administrador", CorreoDelAdministrador, "sololetrasnodigitos"));

        Assert.False(await usuarios.ExisteAlgunAdministradorAsync());
    }

    [Fact]
    public async Task RechazaCorreoMalFormadoYNombreVacio()
    {
        var (sembrar, usuarios, _) = Escenario();

        await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.EjecutarAsync("Administrador", "no-es-un-correo", ContrasenaValida));

        await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.EjecutarAsync("   ", CorreoDelAdministrador, ContrasenaValida));

        Assert.False(await usuarios.ExisteAlgunAdministradorAsync());
    }

    [Fact]
    public async Task LosMensajesNoLlevanElCorreoNiLaContrasena()
    {
        var (sembrar, usuarios, _) = Escenario();

        usuarios.Crear("Antes", CorreoDelAdministrador, "hash-cualquiera", Ahora, activo: true);

        var error = await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.EjecutarAsync("Administrador", CorreoDelAdministrador, ContrasenaValida));

        // RD-08: el mensaje dice que paso, no con que datos. Este texto sale por
        // consola al arrancar, asi que no puede llevar el correo.
        Assert.DoesNotContain(CorreoDelAdministrador, error.Message);
        Assert.DoesNotContain(ContrasenaValida, error.Message);
    }

    [Fact]
    public async Task ElCorreoSeNormalizaComoEnElRegistro()
    {
        var (sembrar, usuarios, _) = Escenario();

        await sembrar.EjecutarAsync("Administrador", "  Admin@Ejemplo.COM  ", ContrasenaValida);

        // La misma normalizacion que en Usuario.Crear, para que el indice unico
        // de RF-CA-01 no tenga dos formas de decidir que dos correos son la misma
        // persona.
        var guardado = await usuarios.BuscarPorCorreoAsync("admin@ejemplo.com");
        Assert.NotNull(guardado);
        Assert.Equal("admin@ejemplo.com", guardado!.Correo);
    }

    [Fact]
    public void CrearComoAdministradorEsLaSegundaPuertaYElRegistroSigueSiendoEstandar()
    {
        var ahora = Ahora;

        var porRegistro = Usuario.Crear("Registro", "registro@ejemplo.com", "hash", ahora);
        var porSeed = Usuario.CrearComoAdministrador("Seed", "seed@ejemplo.com", "hash", ahora);

        // La puerta publica no cambio: sigue dando Estándar e inactivo, que es lo
        // que exigen RF-CA-15 y RF-CA-04.
        Assert.Equal(Rol.Estandar, porRegistro.Rol);
        Assert.False(porRegistro.Activo);

        // Y la nueva puerta no toca CredencialVersion: el Administrador arranca
        // con las sesiones limpias, sin el numero alterado que habria dejado
        // Crear seguido de CambiarRol y Activar.
        Assert.Equal(Rol.Administrador, porSeed.Rol);
        Assert.True(porSeed.Activo);
        Assert.Equal(0, porSeed.CredencialVersion);
    }
}