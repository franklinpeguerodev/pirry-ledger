using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// El seed del primer Administrator (docs/adr/002-primer-administrator.md).
//
// Estas pruebas cubren los tres motivos por los que un seed puede ser
// peligroso: que se ejecute dos veces, que convierta en Administrator una cuenta
// ajena por error, y que no respete la politica de Password.
public sealed class PruebasDelPrimerAdministrador
{
    private const string ContrasenaValida = "abc12345";
    private const string CorreoDelAdministrador = "admin@ejemplo.com";

    private static readonly DateTime utcNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static (SeedFirstAdministrator Sembrar, FakeUserRepository Users, Argon2idPasswordHasher Hasher) scenario()
    {
        var Users = new FakeUserRepository();
        var Clock = new FakeClock(utcNow);

        // El Argon2id real, como en las demas pruebas: si el seed usara otro
        // hasher, estas pruebas no lo detectarian.
        var hasher = new Argon2idPasswordHasher(new Argon2Parameters(
            MemoryKb: 19456,
            Iterations: 2,
            Parallelism: 1));

        var sembrar = new SeedFirstAdministrator(Users, hasher, Clock);

        return (sembrar, Users, hasher);
    }

    [Fact]
    public async Task CreaUnAdministradorActivo()
    {
        var (sembrar, Users, _) = scenario();

        var creado = await sembrar.ExecuteAsync("Administrator", CorreoDelAdministrador, ContrasenaValida);

        Assert.True(creado);

        var guardado = await Users.FindByEmailAsync(CorreoDelAdministrador);

        Assert.NotNull(guardado);
        Assert.Equal(Role.Administrator, guardado!.Role);

        // Active, al contrario que el registro. Sin enlace de activacion no hay
        // quien lo abra, y Activate es Operation de Administrator.
        Assert.True(guardado.Active);
    }

    [Fact]
    public async Task LaContrasenaQuedaHasheadaYConSalPropia()
    {
        var (sembrar, Users, hasher) = scenario();

        await sembrar.ExecuteAsync("Administrator", CorreoDelAdministrador, ContrasenaValida);

        var guardado = await Users.FindByEmailAsync(CorreoDelAdministrador);

        // RF-CA-02: nunca la Password.
        Assert.NotEqual(ContrasenaValida, guardado!.PasswordHash);
        Assert.StartsWith("$argon2id$", guardado.PasswordHash);

        // Sal por User (RF-CA-02): dos cuentas con la MISMA Password no
        // pueden compartir el valor almacenado.
        //
        // La segunda cuenta se crea con el registro Public, no con el seed: el
        // seed ya corrio una vez y por idempotencia no vuelve a Create nada. La
        // comparacion es entre el hash del Administrator y el de un Estándar con
        // la misma Password, que es donde se ve la sal.
        Users.Create("Standard", "Standard@ejemplo.com", hasher.Hash(ContrasenaValida), utcNow, active: true);
        var otro = await Users.FindByEmailAsync("Standard@ejemplo.com");

        Assert.NotEqual(guardado.PasswordHash, otro!.PasswordHash);

        // Y el hash verifica de verdad, con el mismo hasher.
        Assert.True(hasher.Verify(ContrasenaValida, guardado.PasswordHash));
    }

    [Fact]
    public async Task EjecutarloDosvecesNoCreaDosAdministradores()
    {
        var (sembrar, Users, _) = scenario();

        var primero = await sembrar.ExecuteAsync("Administrator", CorreoDelAdministrador, ContrasenaValida);
        var segundo = await sembrar.ExecuteAsync("Administrator", CorreoDelAdministrador, ContrasenaValida);

        // Es el requisito: arrancar la aplicacion dos veces no puede Create dos
        // Administradores.
        Assert.True(primero);
        Assert.False(segundo);

        Assert.True(await Users.AnyAdministratorExistsAsync());
        Assert.Single(await Users.FindAllAdministratorsAsync());
    }

    [Fact]
    public async Task NoCreaUnSegundoAdministradorAunqueElCorreoSeaDistinto()
    {
        var (sembrar, Users, _) = scenario();

        await sembrar.ExecuteAsync("Administrator", CorreoDelAdministrador, ContrasenaValida);

        // Mismo caso de uso, otro Email. La idempotencia no compara correos:
        // pregunta si ya hay un Administrator.
        var segundo = await sembrar.ExecuteAsync("Otro", "otro@ejemplo.com", ContrasenaValida);

        Assert.False(segundo);
        Assert.Null(await Users.FindByEmailAsync("otro@ejemplo.com"));
    }

    [Fact]
    public async Task NoPromueveUnaCuentaQueYaExiste()
    {
        var (sembrar, Users, _) = scenario();

        // Alguien se registro con ese Email antes de que existiera el seed.
        Users.Create("Antes", CorreoDelAdministrador, "hash-cualquiera", utcNow, active: true);

        // Promover a un Estándar por el camino del seed seria el escalamiento que
        // RF-CA-08 quiere impedir, asi que se rechaza.
        var error = await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.ExecuteAsync("Administrator", CorreoDelAdministrador, ContrasenaValida));

        Assert.NotNull(error);

        // La cuenta sigue siendo Estándar: el seed no la toco.
        var guardado = await Users.FindByEmailAsync(CorreoDelAdministrador);
        Assert.Equal(Role.Standard, guardado!.Role);
        Assert.Equal("hash-cualquiera", guardado.PasswordHash);
    }

    [Fact]
    public async Task AplicaLaPoliticaDeContrasenaDelRegistro()
    {
        var (sembrar, Users, _) = scenario();

        // RF-CA-14 alcanza al seed aunque la Password venga de una variable de
        // entorno y no de un formulario: una Password debil en un
        // Administrator es peor que en un Estándar.
        await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.ExecuteAsync("Administrator", CorreoDelAdministrador, "corta"));

        // Solo letras y con longitud de sobra: la politica exige las dos cosas,
        // letras y numeros, no una de las dos.
        await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.ExecuteAsync("Administrator", CorreoDelAdministrador, "sololetrasnodigitos"));

        Assert.False(await Users.AnyAdministratorExistsAsync());
    }

    [Fact]
    public async Task RechazaCorreoMalFormadoYNombreVacio()
    {
        var (sembrar, Users, _) = scenario();

        await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.ExecuteAsync("Administrator", "no-es-un-Email", ContrasenaValida));

        await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.ExecuteAsync("   ", CorreoDelAdministrador, ContrasenaValida));

        Assert.False(await Users.AnyAdministratorExistsAsync());
    }

    [Fact]
    public async Task LosMensajesNoLlevanElCorreoNiLaContrasena()
    {
        var (sembrar, Users, _) = scenario();

        Users.Create("Antes", CorreoDelAdministrador, "hash-cualquiera", utcNow, active: true);

        var error = await Assert.ThrowsAsync<FirstAdministratorNotSeededException>(
            () => sembrar.ExecuteAsync("Administrator", CorreoDelAdministrador, ContrasenaValida));

        // RD-08: el Message dice que paso, no con que datos. Este texto sale por
        // consola al arrancar, asi que no puede llevar el Email.
        Assert.DoesNotContain(CorreoDelAdministrador, error.Message);
        Assert.DoesNotContain(ContrasenaValida, error.Message);
    }

    [Fact]
    public async Task ElCorreoSeNormalizaComoEnElRegistro()
    {
        var (sembrar, Users, _) = scenario();

        await sembrar.ExecuteAsync("Administrator", "  Admin@Ejemplo.COM  ", ContrasenaValida);

        // La misma normalizacion que en User.Create, para que el indice unico
        // de RF-CA-01 no tenga dos formas de decidir que dos correos son la misma
        // persona.
        var guardado = await Users.FindByEmailAsync("admin@ejemplo.com");
        Assert.NotNull(guardado);
        Assert.Equal("admin@ejemplo.com", guardado!.Email);
    }

    [Fact]
    public void CrearComoAdministradorEsLaSegundaPuertaYElRegistroSigueSiendoEstandar()
    {
        var utcNow = DateTime.UtcNow;

        var porRegistro = User.Create("Registro", "registro@ejemplo.com", "hash", utcNow);
        var porSeed = User.CreateAsAdministrator("Seed", "seed@ejemplo.com", "hash", utcNow);

        // La puerta publica no cambio: sigue dando Estándar e inactivo, que es lo
        // que exigen RF-CA-15 y RF-CA-04.
        Assert.Equal(Role.Standard, porRegistro.Role);
        Assert.False(porRegistro.Active);

        // Y la nueva puerta no toca CredentialVersion: el Administrator arranca
        // con las Sessions limpias, sin el numero alterado que habria dejado
        // Create seguido de ChangeRole y Activate.
        Assert.Equal(Role.Administrator, porSeed.Role);
        Assert.True(porSeed.Active);
        Assert.Equal(0, porSeed.CredentialVersion);
    }
}