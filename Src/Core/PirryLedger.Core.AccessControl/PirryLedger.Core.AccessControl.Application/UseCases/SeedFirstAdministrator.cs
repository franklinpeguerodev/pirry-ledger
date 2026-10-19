using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// Crea el primer Administrator de la base a partir de las variables de entorno
// (docs/adr/002-first-administrator.md).
//
// Por que hace falta: el registro Public siempre fija role.Standard, porque
// RF-CA-15 exige que nazca inactivo y un User recien registrado no puede
// gobernarse a si mismo. El resultado es que no hay forma de llegar a
// Administrator, y RF-CA-08, RF-CA-20 y RF-CA-21 no se pueden ejercitar sin uno.
//
// Esto NO es autorregistro. Lee una contrasena que el operador ya tiene, no una
// que viene de un formulario, y no encola ningun Email.
//
// El caso de uso es idempotente por el motivo que RF-NOT-12 exige al enviador y
// que aqui importa por lo mismo: arrancar la aplicacion dos veces no puede
// Create dos Administradores. Se resuelve preguntando si ya hay alguno, no
// contando.
//
// Los mensajes de excepcion no llevan el Email ni la contrasena (RD-08).
public sealed class SeedFirstAdministrator
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public SeedFirstAdministrator(
        IUserRepository users,
        IPasswordHasher hasher,
        IClock clock)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
    }

    // Devuelve true si creo la cuenta, false si no habia nada que hacer porque ya
    // existia un Administrator. Devolver el booleano y no lanzar en el caso
    // normal es lo que permite al Host imprimir una sola linea y seguir
    // arrancando.
    public async Task<bool> ExecuteAsync(
        string name,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var emailResult = EmailValidator.Evaluate(email);

        if (!emailResult.IsValid)
        {
            throw new FirstAdministratorNotSeededException(emailResult.Reason!);
        }

        // Se reutiliza la politica del registro (RF-CA-14) en vez de duplicarla.
        // La contrasena del Administrator sale de una variable de entorno, no de
        // un formulario, asi que no hay reglas mas debiles que aplicar.
        var policy = PasswordPolicy.Evaluate(password);

        if (!policy.IsValid)
        {
            throw new FirstAdministratorNotSeededException(policy.Reason!);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new FirstAdministratorNotSeededException("El Name es obligatorio.");
        }

        // Idempotencia: si ya hay un Administrator, esta operation ya se hizo. No
        // se compara el Email porque un seed distinto apuntando a otra cuenta
        // tampoco debe Create un segundo Administrator por accidente.
        if (await _users.AnyAdministratorExistsAsync(cancellationToken))
        {
            return false;
        }

        var normalizedEmail = email.Trim();

        // El Email del seed podria existir como Estándar, por ejemplo si alguien
        // se registro con ese Email antes de que existiera el seed. Se rechaza en
        // vez de promover la cuenta: cambiarle el role a alguien que se registro
        // solo es justo el privilege escalation que RF-CA-08 quiere impedir.
        var existing = await _users.FindByEmailAsync(normalizedEmail, cancellationToken);

        if (existing is not null)
        {
            throw new FirstAdministratorNotSeededException(
                "Ya existe una cuenta con ese Email y no es Administrator. " +
                "Usa otro Email para el primer Administrator.");
        }

        // RF-CA-02: el mismo hasher del registro, con su sal por User.
        var hash = _hasher.Hash(password);

        var administrator = User.CreateAsAdministrator(
            name,
            normalizedEmail,
            hash,
            _clock.UtcNow);

        await _users.SaveAsync(administrator, cancellationToken);

        return true;
    }
}
