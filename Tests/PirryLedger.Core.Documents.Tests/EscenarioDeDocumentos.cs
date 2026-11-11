using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.Documents.Application;
using PirryLedger.Core.Documents.Application.UseCases;

namespace PirryLedger.Core.Documents.Tests;

// Escenario compartido: un Administrator, un Standard y otro Standard, los tres
// con sesion abierta, mas el repositorio y el almacenamiento en memoria que
// usan los cuatro casos de uso.
internal sealed class EscenarioDeDocumentos
{
    public static readonly Guid StandardBranch = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private const string ValidPassword = "abc12345";
    private const string AdminEmailValue = "admin@ejemplo.com";
    private const string StandardEmailValue = "empleado@ejemplo.com";
    private const string OtherEmailValue = "otro@ejemplo.com";

    public EscenarioDeDocumentos(DateTime? now = null)
    {
        var start = now ?? new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        Clock = new FakeClock(start);

        var hasher = new Argon2idPasswordHasher(new Argon2Parameters(
            MemoryKb: 19456,
            Iterations: 2,
            Parallelism: 1));

        Users = new InMemoryUserRepository();
        Admin = Users.Create("Admin", AdminEmailValue, hasher.Hash(ValidPassword), start, active: true, role: Role.Administrator);
        Standard = Users.Create("Empleado", StandardEmailValue, hasher.Hash(ValidPassword), start, active: true, branchId: StandardBranch);
        OtherStandard = Users.Create("Otro", OtherEmailValue, hasher.Hash(ValidPassword), start, active: true, branchId: StandardBranch);

        Sessions = new InMemorySessionRepository();
        Authenticate = new Authenticate(Sessions, Users, Clock, new FakePermissionAuthority());
        Login = new Login(Users, Sessions, hasher, Clock, TimeSpan.FromHours(8));

        Documents = new FakeDocumentRepository();
        Store = new FakeDocumentStore();
        Options = DocumentUploadOptions.Default;
    }

    public FakeClock Clock { get; }

    public InMemoryUserRepository Users { get; }

    public InMemorySessionRepository Sessions { get; }

    public Authenticate Authenticate { get; }

    public Login Login { get; }

    public FakeDocumentRepository Documents { get; }

    public FakeDocumentStore Store { get; }

    public DocumentUploadOptions Options { get; }

    public User Admin { get; }

    public User Standard { get; }

    public User OtherStandard { get; }

    public Task<string> OpenAdminSessionAsync() => Login.ExecuteAsync(AdminEmailValue, ValidPassword);

    public Task<string> OpenStandardSessionAsync() => Login.ExecuteAsync(StandardEmailValue, ValidPassword);

    public Task<string> OpenOtherSessionAsync() => Login.ExecuteAsync(OtherEmailValue, ValidPassword);

    public UploadDocument NewUpload() =>
        new(Documents, Store, Authenticate, Clock, Options);

    public ListDocuments NewList() =>
        new(Documents, Authenticate);

    public DownloadDocument NewDownload() =>
        new(Documents, Store, Authenticate);

    public DeleteDocument NewDelete() =>
        new(Documents, Authenticate, Clock);
}
