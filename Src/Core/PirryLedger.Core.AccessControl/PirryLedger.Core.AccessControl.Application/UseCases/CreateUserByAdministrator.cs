using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Auth;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-01, RF-CA-15 y RF-CA-21, y docs/adr/013-admin-created-users.md: el alta
// la hace un Administrator y nadie mas. La ruta publica /api/auth/register deja
// de existir; esta es la unica puerta que crea un User.
//
// La comprobacion del role se hace dos veces a proposito, igual que en
// ChangeUserRole: el filtro la hace antes de llegar al endpoint y el caso de uso
// la repite. La segunda no es adorno: un caso de uso se puede llamar desde otro
// sitio que no pase por el filtro, y RD-06 exige que la autorizacion viva en el
// servidor.
//
// El cuerpo no lleva role. El User nace Standard e inactivo, y la unica puerta a
// Administrator sigue siendo ChangeUserRole (RF-CA-08).
//
// El caso de uso no sabe nada de SMTP. Encola el correo de activacion y termina
// bien aunque no haya servidor de correo configurado (RF-NOT-08); el envio lo
// hace el proceso aparte.
//
// El mensaje de excepcion no lleva el correo ni la contrasena del usuario
// (RD-08): dice que paso, no con que datos.
public sealed class CreateUserByAdministrator
{
    // El Name vive en una columna de 150 caracteres (ac_users.name, ver
    // UserConfiguration). Sin esta validacion, un Name mas largo llegaria a
    // PostgreSQL y saldria como error interno con traza (RD-07). El limite es el
    // mismo de la columna, no uno inventado aqui.
    public const int MaximumNameLength = 150;

    private readonly IUserRepository _users;
    private readonly IActivationTokenRepository _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailQueue _emailQueue;
    private readonly Authenticate _authenticate;
    private readonly IClock _clock;
    private readonly string _baseUrl;
    private readonly IBranchRepository? _branches;

    public CreateUserByAdministrator(
        IUserRepository users,
        IActivationTokenRepository tokens,
        IPasswordHasher hasher,
        IEmailQueue emailQueue,
        Authenticate authenticate,
        IClock clock,
        string baseUrl)
    {
        _users = users;
        _tokens = tokens;
        _hasher = hasher;
        _emailQueue = emailQueue;
        _authenticate = authenticate;
        _clock = clock;
        _baseUrl = baseUrl;
    }

    public CreateUserByAdministrator(
        IUserRepository users,
        IActivationTokenRepository tokens,
        IPasswordHasher hasher,
        IEmailQueue emailQueue,
        Authenticate authenticate,
        IClock clock,
        string baseUrl,
        IBranchRepository branches)
        : this(users, tokens, hasher, emailQueue, authenticate, clock, baseUrl)
    {
        _branches = branches;
    }

    // Devuelve el Id del User creado: es lo unico que el cliente necesita para
    // el siguiente paso (listarlo, promoverlo o desbloquearlo).
    public async Task<Guid> ExecuteAsync(
        string plainToken,
        string name,
        string email,
        string password,
        Guid branchId,
        TimeSpan validityDuration,
        CancellationToken cancellationToken = default)
    {
        // RF-CA-06 y RD-06: primero quien llama. Un Standard recibe 403 antes de
        // que se valide nada del cuerpo, asi que no puede usar esta operation
        // para sondear que correos existen.
        await _authenticate.ExecuteWithRoleAsync(
            plainToken,
            RoleRequirements.RequiredRole(Operation.CreateUserByAdministrator),
            cancellationToken);

        var emailResult = EmailValidator.Evaluate(email);

        if (!emailResult.IsValid)
        {
            throw new RegistrationRejectedException(emailResult.Reason!);
        }

        var policy = PasswordPolicy.Evaluate(password);

        if (!policy.IsValid)
        {
            throw new RegistrationRejectedException(policy.Reason!);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new RegistrationRejectedException("El nombre es obligatorio.");
        }

        // RD-07: el Name se valida antes de tocar la base, para que un dato
        // demasiado largo sea un 400 controlado y no un error de PostgreSQL.
        if (name.Trim().Length > MaximumNameLength)
        {
            throw new RegistrationRejectedException(
                $"El nombre no puede tener mas de {MaximumNameLength} caracteres.");
        }

        if (branchId == Guid.Empty || branchId == Branch.AllId && _branches is not null)
        {
            throw new RegistrationRejectedException("Debe indicar una sucursal física.");
        }

        if (_branches is not null &&
            (await _branches.FindByIdAsync(branchId, cancellationToken) is not { Active: true, IsAll: false }))
        {
            throw new RegistrationRejectedException("La sucursal indicada no está disponible.");
        }

        var normalizedEmail = email.Trim();

        // RF-CA-01: el indice unico de la base es la garantia real. Esta
        // comprobacion da un mensaje controlado antes de llegar alla, y el
        // indice cubre el caso de dos altas concurrentes.
        var existing = await _users.FindByEmailAsync(normalizedEmail, cancellationToken);

        if (existing is not null)
        {
            throw new DuplicateEmailException();
        }

        // RF-CA-02: se hashea aqui y el resto del sistema solo ve el hash.
        var hash = _hasher.Hash(password);

        // RF-CA-15: nace inactivo y con role Standard, ambos fijados por la
        // entidad, no por parametros de este metodo. Que el alta sea de un
        // Administrator no cambia la regla: la cuenta no sirve hasta que se abra
        // el enlace del correo.
        var user = User.Create(name, normalizedEmail, hash, _clock.UtcNow, branchId);

        await _users.SaveAsync(user, cancellationToken);

        var plainTokenValue = OneTimeCodeGenerator.Generate();

        var token = ActivationToken.Create(
            user.Id,
            OneTimeCodeGenerator.CalculateHash(plainTokenValue),
            _clock.UtcNow,
            validityDuration);

        await _tokens.SaveAsync(token, cancellationToken);

        // RF-NOT-08: se encola y la operation termina. El token en claro sale
        // ahora, dentro del correo, y no se guarda en ningun sitio mas.
        await _emailQueue.EnqueueAsync(
            new QueuedEmail(
                user.Email,
                ActivationEmail.Subject,
                ActivationEmail.BuildBody(_baseUrl, plainTokenValue)),
            cancellationToken);

        return user.Id;
    }
}
