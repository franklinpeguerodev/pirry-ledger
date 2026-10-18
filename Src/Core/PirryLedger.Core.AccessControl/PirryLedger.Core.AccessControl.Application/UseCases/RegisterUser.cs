using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using PirryLedger.Core.Contracts.Notifications;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-01 y RF-CA-15: registrar un usuario con correo unico.
//
// El caso de uso no sabe nada de SMTP. Encola el correo de activacion y termina
// bien aunque no haya servidor de correo configurado (RF-NOT-08); el envio lo
// hace el proceso aparte.
//
// El mensaje de excepcion no lleva el correo ni la contrasena del usuario
// (RD-08): dice que paso, no con que datos.
public sealed class RegisterUser
{
    private readonly IUserRepository _users;
    private readonly IActivationTokenRepository _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailQueue _emailQueue;
    private readonly IClock _clock;
    private readonly string _baseUrl;

    public RegisterUser(
        IUserRepository users,
        IActivationTokenRepository tokens,
        IPasswordHasher hasher,
        IEmailQueue emailQueue,
        IClock clock,
        string baseUrl)
    {
        _users = users;
        _tokens = tokens;
        _hasher = hasher;
        _emailQueue = emailQueue;
        _clock = clock;
        _baseUrl = baseUrl;
    }

    public async Task ExecuteAsync(
        string name,
        string email,
        string password,
        TimeSpan validityDuration,
        CancellationToken cancellationToken = default)
    {
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

        var normalizedEmail = email.Trim();

        // RF-CA-01: el indice unico de la base es la garantia real. Esta
        // comprobacion da un mensaje controlado antes de llegar alla, y el
        // indice cubre el caso de dos registros concurrentes.
        var existing = await _users.FindByEmailAsync(normalizedEmail, cancellationToken);

        if (existing is not null)
        {
            throw new DuplicateEmailException();
        }

        // RF-CA-02: se hashea aqui y el resto del sistema solo ve el hash.
        var hash = _hasher.Hash(password);

        // RF-CA-15: nace inactivo y con role Estandar, ambos fijados por la
        // entidad, no por parametros de este metodo.
        var user = User.Create(name, normalizedEmail, hash, _clock.UtcNow);

        await _users.SaveAsync(user, cancellationToken);

        var plainToken = OneTimeCodeGenerator.Generate();

        var token = ActivationToken.Create(
            user.Id,
            OneTimeCodeGenerator.CalculateHash(plainToken),
            _clock.UtcNow,
            validityDuration);

        await _tokens.SaveAsync(token, cancellationToken);

        // RF-NOT-08: se encola y la operacion termina. El token en claro sale
        // ahora, dentro del correo, y no se guarda en ningun sitio mas.
        await _emailQueue.EnqueueAsync(
            new QueuedEmail(
                normalizedEmail,
                ActivationEmail.Subject,
                ActivationEmail.BuildBody(_baseUrl, plainToken)),
            cancellationToken);
    }
}
