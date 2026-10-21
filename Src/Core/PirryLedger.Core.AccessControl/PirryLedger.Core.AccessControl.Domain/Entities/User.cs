using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Domain.Entities;

// RF-CA-01, RF-CA-02, RF-CA-04 y RF-CA-15.
//
// Atributos minimos exigidos por docs/requirements/entities.md: Name, Email
// unico, contrasena con hash, Role y Active. Los tres ultimos atributos que el
// curso no define pero esta fase necesita estan marcados como decision propia.
//
// Esta entidad no sabe como se guarda: la contrasena entra ya hasheada y Domain
// no ve ningun paquete de criptografia. El hash es un valor opaco para ella.
public sealed class User
{
    private User()
    {
    }

    private User(
        Guid id,
        string name,
        string email,
        string passwordHash,
        Role role,
        bool active,
        DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        Active = active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    // RF-CA-02: nunca la contrasena. El formato lo define quien hashea.
    public string PasswordHash { get; private set; } = string.Empty;

    // RF-CA-04: todo User tiene exactamente un Role, nunca ninguno.
    public Role Role { get; private set; }

    // RF-CA-15: el User nace inactivo. Sin pasar por aqui no hay Session.
    public bool Active { get; private set; }

    // Decision propia: RF-CA-15 lo declara "decisiones de diseno". Lo necesita
    // el listado de RF-CA-21 y el seed idempotente del primer Administrator.
    public DateTime CreatedAtUtc { get; private set; }

    // Trazabilidad minima: null significa que la contrasena solo ha sido la
    // inicial; se establece cada vez que ChangePassword reemplaza el hash.
    public DateTime? PasswordChangedAtUtc { get; private set; }

    // Decision propia: RF-CA-12, RF-CA-18 y RF-CA-20 exige que cambiar la
    // contrasena o Deactivate invalide las sesiones abiertas. Subir este numero
    // es lo que invalida todas las credenciales emitidas antes del cambio.
    public int CredentialVersion { get; private set; }

    // Decision propia: RF-CA-19, bloqueos tras cinco intentos fallidos.
    public int FailedAttempts { get; private set; }

    public DateTime? LockedUntilUtc { get; private set; }

    // Una sola puerta de entrada para el registro Public: nada crea un User
    // escribiendolo a mano. El role y el Active se fijan aqui y no como
    // parametros, porque RF-CA-15 exige que nazca inactivo y RF-CA-04 que nazca
    // con un role.
    //
    // Esta es la UNICA excepcion a esa regla, y hay dos: CreateAsAdministrator.
    // Se llego a esta forma tras comparar tres, en docs/adr/002-first-administrator.md.
    public static User Create(string name, string email, string passwordHash, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El Name es obligatorio.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("El Email es obligatorio.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("El hash de la contrasena es obligatorio.", nameof(passwordHash));
        }

        // Decision propia: el Email se guarda siempre en minusculas. Sin esto,
        // "Juan@Email.com" y "juan@Email.com" serian dos personas distintas
        // para el indice unico de RF-CA-01. La normalizacion vive en la entidad,
        // en un solo sitio, para que buscar y guardar no puedan discrepar.
        return new User(
            Guid.NewGuid(),
            name.Trim(),
            email.Trim().ToLowerInvariant(),
            passwordHash,
            Role.Standard,
            active: false,
            utcNow);
    }

    // La segunda puerta, y la unica que no es el registro Public. La necesita el
    // seed del primer Administrator, porque Create fija role.Standard de forma
    // fija y no hay forma de llegar a Administrator por el camino normal
    // (docs/adr/002-first-administrator.md).
    //
    // Nace Active, al contrario que Create. No es un descuido: este User no
    // llega por Email, asi que no tiene enlace de activacion que nadie pueda
    // abrir. Si naciera inactivo no habria forma de activarlo, porque activarlo
    // es una operation de Administrator y no habria ningun Administrator.
    //
    // Los chequeos de Name, Email y hash son los mismos que en Create, y estan
    // repetidos a proposito: duplicar cuatro lineas es mas barato que un
    // constructor privado que una de las dos puertas puedan saltarse.
    public static User CreateAsAdministrator(
        string name,
        string email,
        string passwordHash,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El Name es obligatorio.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("El Email es obligatorio.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("El hash de la contrasena es obligatorio.", nameof(passwordHash));
        }

        // El Email se normaliza igual que en Create, para que las dos puertas
        // escriban el mismo formato y el indice unico de RF-CA-01 no tenga dos
        // formas de decidir que dos correos son la misma persona.
        return new User(
            Guid.NewGuid(),
            name.Trim(),
            email.Trim().ToLowerInvariant(),
            passwordHash,
            Role.Administrator,
            active: true,
            utcNow);
    }

    // RF-CA-16: abrir el enlace de activacion activa la cuenta. Solo desde
    // inactivo, para que Activate dos veces el mismo User no tenga efecto.
    public void Activate()
    {
        if (Active)
        {
            throw new InvalidOperationException("La cuenta ya esta activa.");
        }

        Active = true;
    }

    // RF-CA-20: un Administrator desactiva y reactiva. Deactivate sube la
    // version de credencial, asi que las sesiones abiertas dejan de servir.
    public void Deactivate()
    {
        if (!Active)
        {
            throw new InvalidOperationException("La cuenta ya esta inactiva.");
        }

        Active = false;
        CredentialVersion++;
    }

    public void Reactivate()
    {
        if (Active)
        {
            throw new InvalidOperationException("La cuenta ya esta activa.");
        }

        Active = true;
    }

    // RF-CA-08 + ADR 009: cambiar el role invalida las sesiones abiertas.
    // Mismo mecanismo que ChangePassword y Deactivate: incrementar
    // CredentialVersion hace que cualquier session emitida antes de la
    // mutacion deje de pasar IsValid, sin tocar la tabla de sesiones. El
    // usuario promovido (o degradado) tiene que re-login para que su
    // siguiente peticion refleje el nuevo role.
    //
    // El caso de uso (ChangeUserRole) decide quien puede cambiar el role; la
    // entidad solo se encarga de que el valor sea uno valido y de la
    // invalidacion.
    public void ChangeRole(Role newRole)
    {
        Role = newRole;
        CredentialVersion++;
    }

    // RF-CA-11 y RF-CA-13: cambiar la contrasena invalida las sesiones abiertas
    // (RF-CA-12) y pone a cero el contador de fallos (RF-CA-19).
    public void ChangePassword(string newHash, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newHash);

        PasswordHash = newHash;
        PasswordChangedAtUtc = utcNow;
        CredentialVersion++;
        FailedAttempts = 0;
        LockedUntilUtc = null;
    }

    // RF-CA-19: tras cinco intentos fallidos seguidos la cuenta queda bloqueada
    // quince minutos. El sexto intento se rechaza aunque la contrasena sea
    // correcta, porque el bloqueo se comprueba antes de Verify el hash.
    public void RegisterFailedAttempt(int maximumAttempts, DateTime utcNow, TimeSpan lockoutDuration)
    {
        if (LockedUntilUtc is not null && utcNow < LockedUntilUtc.Value)
        {
            return;
        }

        FailedAttempts++;

        if (FailedAttempts >= maximumAttempts)
        {
            LockedUntilUtc = utcNow.Add(lockoutDuration);
        }
    }

    // RF-CA-19: un inicio de Session correcto pone el contador en cero.
    public void RegisterSuccessfulAttempt()
    {
        FailedAttempts = 0;
        LockedUntilUtc = null;
    }

    public bool IsBlocked(DateTime utcNow) =>
        LockedUntilUtc is not null && utcNow < LockedUntilUtc.Value;
}
