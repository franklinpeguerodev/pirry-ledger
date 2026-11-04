using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;
using System.Text.Json;

namespace PirryLedger.Core.AccessControl.Tests.Credencial;

// RF-CA-01, RF-CA-15 y RF-CA-21, y docs/adr/013-admin-created-users.md: el alta
// de un User la hace un Administrator. Estas pruebas cubren lo que el ADR decide:
// que el alta sea solo de Administrator, que la cuenta nazca Standard e inactiva,
// que el correo de activacion se encole y que el cuerpo no pueda pedir otro role.
public sealed class PruebasDeAltaPorAdministrador
{
    private static readonly DateTime utcNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string BaseUrl = "http://localhost:5243";
    private const string AdminEmail = "admin@ejemplo.com";
    private const string AdminPassword = "admin12345";

    private static readonly TimeSpan ValidezDelEnlace = TimeSpan.FromHours(24);

    // scenario con un Administrator active y su Session abierta, mas los dobles
    // que el caso de uso necesita para encolar el correo y guardar el token.
    private static async Task<EscenarioDeAlta> EscenarioAsync()
    {
        var scenario = new SessionScenario(utcNow);

        scenario.Users.Create(
            "Admin",
            AdminEmail,
            scenario.Hasher.Hash(AdminPassword),
            utcNow,
            active: true,
            role: Role.Administrator);

        var tokenAdmin = await scenario.Login.ExecuteAsync(AdminEmail, AdminPassword);

        var tokens = new FakeActivationTokenRepository();
        var queue = new InMemoryEmailQueue();
        var branch = Branch.Create("Sucursal Centro", "Calle Principal 1");
        var branches = new FakeBranchRepository(branch);

        var caso = new CreateUserByAdministrator(
            scenario.Users,
            tokens,
            scenario.Hasher,
            queue,
            scenario.Authenticate,
            scenario.Clock,
            BaseUrl,
            branches);

        return new EscenarioDeAlta(scenario, tokenAdmin, tokens, queue, caso, branch.Id);
    }

    [Fact]
    public async Task AdministradorCreaUnUsuarioEstandarEInactivo()
    {
        var escenario = await EscenarioAsync();

        var id = await escenario.Caso.ExecuteAsync(
            escenario.TokenAdmin,
            "Maria Perez",
            "maria@ejemplo.com",
            "temporal123",
            escenario.BranchId,
            ValidezDelEnlace);

        var creado = await escenario.Scenario.Users.FindByIdAsync(id);

        Assert.NotNull(creado);
        Assert.Equal("Maria Perez", creado!.Name);
        Assert.Equal("maria@ejemplo.com", creado.Email);
        Assert.Equal(Role.Standard, creado.Role);

        // RF-CA-15: el alta NO crea un User usable. Hasta que se abra el enlace
        // del correo, la cuenta no inicia Session.
        Assert.False(creado.Active);

        // RF-CA-02: lo guardado es el hash, nunca la contrasena.
        Assert.NotEqual("temporal123", creado.PasswordHash);
        Assert.True(escenario.Scenario.Hasher.Verify("temporal123", creado.PasswordHash));
    }

    [Fact]
    public async Task ElAltaEncolaElCorreoDeActivacionYGuardaSoloElHashDelToken()
    {
        var escenario = await EscenarioAsync();

        await escenario.Caso.ExecuteAsync(
            escenario.TokenAdmin,
            "Maria Perez",
            "maria@ejemplo.com",
            "temporal123",
            escenario.BranchId,
            ValidezDelEnlace);

        // RF-NOT-08: el correo sale por la cola, no por SMTP dentro de la
        // operation.
        var correo = Assert.Single(escenario.Queue.Emails);
        Assert.Equal("maria@ejemplo.com", correo.Recipient);

        // El token en claro solo existe dentro del cuerpo del correo.
        var token = Assert.Single(escenario.Tokens.Saved);
        var enlace = correo.Body.Split('\n', StringSplitOptions.TrimEntries)
            .Single(linea => linea.Contains("/activate?token=", StringComparison.Ordinal));
        var tokenEnClaro = enlace.Split("token=")[1].Trim();

        Assert.Equal(OneTimeCodeGenerator.CalculateHash(tokenEnClaro), token.TokenHash);
        Assert.NotEqual(tokenEnClaro, token.TokenHash);
        Assert.Equal(utcNow.Add(ValidezDelEnlace), token.ExpiresAtUtc);
    }

    [Fact]
    public async Task ElUsuarioCreadoNoPuedeIniciarSesionHastaQueSeActive()
    {
        var escenario = await EscenarioAsync();

        await escenario.Caso.ExecuteAsync(
            escenario.TokenAdmin,
            "Maria Perez",
            "maria@ejemplo.com",
            "temporal123",
            escenario.BranchId,
            ValidezDelEnlace);

        // RF-CA-15: nace inactivo, y un inactivo no inicia Session.
        await Assert.ThrowsAsync<AccountNotActivatedException>(
            () => escenario.Scenario.Login.ExecuteAsync("maria@ejemplo.com", "temporal123"));
    }

    // ADR 013, decision 2: el cuerpo no lleva role. System.Text.Json ignora los
    // miembros desconocidos, asi que "role": "Administrator" no llega a ningun
    // sitio. La unica puerta a Administrator sigue siendo RF-CA-08.
    [Fact]
    public async Task UnRolEnviadoEnElCuerpoSeIgnoraYElUsuarioNaceEstandar()
    {
        var escenario = await EscenarioAsync();

        const string cuerpo = """
            {
              "name": "Maria Perez",
              "email": "maria@ejemplo.com",
              "password": "temporal123",
              "role": "Administrator"
            }
            """;

        var request = JsonSerializer.Deserialize<CreateUserRequest>(
            cuerpo,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(request);

        // El modelo ni siquiera tiene donde guardar el role: no es que se
        // descarte despues, es que no existe.
        Assert.Null(typeof(CreateUserRequest).GetProperty("Role"));

        var id = await escenario.Caso.ExecuteAsync(
            escenario.TokenAdmin,
            request!.Name,
            request.Email,
            request.Password,
            escenario.BranchId,
            ValidezDelEnlace);

        var creado = await escenario.Scenario.Users.FindByIdAsync(id);

        Assert.Equal(Role.Standard, creado!.Role);
    }

    [Fact]
    public async Task UnaSucursalVaciaSeRechazaSinCrearNada()
    {
        var escenario = await EscenarioAsync();

        await Assert.ThrowsAsync<RegistrationRejectedException>(
            () => escenario.Caso.ExecuteAsync(
                escenario.TokenAdmin,
                "Maria Perez",
                "maria@ejemplo.com",
                "temporal123",
                Guid.Empty,
                ValidezDelEnlace));

        Assert.Null(await escenario.Scenario.Users.FindByEmailAsync("maria@ejemplo.com"));
        Assert.Empty(escenario.Tokens.Saved);
        Assert.Empty(escenario.Queue.Emails);
    }

    // RD-06: el rechazo ocurre en el servidor, con una peticion armada a mano. No
    // basta con que la interfaz no muestre el boton.
    [Fact]
    public async Task UnEstandarNoPuedeCrearUsuarios()
    {
        var escenario = await EscenarioAsync();
        var tokenEstandar = await escenario.Scenario.OpenSessionAsync();

        await Assert.ThrowsAsync<InsufficientRoleException>(
            () => escenario.Caso.ExecuteAsync(
                tokenEstandar,
                "Maria Perez",
                "maria@ejemplo.com",
                "temporal123",
                escenario.BranchId,
                ValidezDelEnlace));

        // Y nada cambio: ni el User, ni el token, ni el correo.
        Assert.Null(await escenario.Scenario.Users.FindByEmailAsync("maria@ejemplo.com"));
        Assert.Empty(escenario.Tokens.Saved);
        Assert.Empty(escenario.Queue.Emails);
    }

    [Fact]
    public async Task SinSesionEs401YNo403()
    {
        var escenario = await EscenarioAsync();

        // El orden importa: sin Session no se sabe el role, asi que no se puede
        // decir "no tienes permisos" porque eso confirmaria que el token fue real
        // alguna vez. Gana el 401.
        await Assert.ThrowsAsync<InvalidSessionException>(
            () => escenario.Caso.ExecuteAsync(
                string.Empty,
                "Maria Perez",
                "maria@ejemplo.com",
                "temporal123",
                escenario.BranchId,
                ValidezDelEnlace));

        await Assert.ThrowsAsync<InvalidSessionException>(
            () => escenario.Caso.ExecuteAsync(
                "token-inventado",
                "Maria Perez",
                "maria@ejemplo.com",
                "temporal123",
                escenario.BranchId,
                ValidezDelEnlace));
    }

    [Fact]
    public async Task UnCorreoDuplicadoSeRechazaSinCrearNada()
    {
        var escenario = await EscenarioAsync();

        await escenario.Caso.ExecuteAsync(
            escenario.TokenAdmin,
            "Maria Perez",
            "maria@ejemplo.com",
            "temporal123",
            escenario.BranchId,
            ValidezDelEnlace);

        // RF-CA-01: el correo es unico. El segundo alta no crea otra cuenta, ni
        // otro token, ni otro correo.
        await Assert.ThrowsAsync<DuplicateEmailException>(
            () => escenario.Caso.ExecuteAsync(
                escenario.TokenAdmin,
                "Otra Maria",
                "maria@ejemplo.com",
                "temporal456",
                escenario.BranchId,
                ValidezDelEnlace));

        var users = await escenario.Scenario.Users.ListAsync();

        Assert.Single(users, user => user.Email == "maria@ejemplo.com");
        Assert.Single(escenario.Tokens.Saved);
        Assert.Single(escenario.Queue.Emails);
    }

    // RD-07: el Name vive en una columna de 150 caracteres. Un Name mas largo es
    // un 400 controlado, no un error interno de PostgreSQL con traza.
    [Fact]
    public async Task UnNombreDemasiadoLargoSeRechazaSinCrearNada()
    {
        var escenario = await EscenarioAsync();
        var nombre = new string('a', CreateUserByAdministrator.MaximumNameLength + 1);

        var error = await Assert.ThrowsAsync<RegistrationRejectedException>(
            () => escenario.Caso.ExecuteAsync(
                escenario.TokenAdmin,
                nombre,
                "maria@ejemplo.com",
                "temporal123",
                escenario.BranchId,
                ValidezDelEnlace));

        Assert.Contains("150", error.Message);
        Assert.Null(await escenario.Scenario.Users.FindByEmailAsync("maria@ejemplo.com"));
        Assert.Empty(escenario.Tokens.Saved);
        Assert.Empty(escenario.Queue.Emails);
    }

    private sealed record EscenarioDeAlta(
        SessionScenario Scenario,
        string TokenAdmin,
        FakeActivationTokenRepository Tokens,
        InMemoryEmailQueue Queue,
        CreateUserByAdministrator Caso,
        Guid BranchId);

    private sealed class FakeBranchRepository : IBranchRepository
    {
        private readonly Branch _branch;

        public FakeBranchRepository(Branch branch)
        {
            _branch = branch;
        }

        public Task<Branch?> FindByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Branch?>(id == _branch.Id ? _branch : null);

        public Task<IReadOnlyList<Branch>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Branch>>(new[] { _branch });

        public Task SaveAsync(
            Branch branch,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
