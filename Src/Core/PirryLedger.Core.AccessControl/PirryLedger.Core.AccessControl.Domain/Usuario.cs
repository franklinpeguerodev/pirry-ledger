namespace PirryLedger.Core.AccessControl.Domain;

// RF-CA-01, RF-CA-02, RF-CA-04 y RF-CA-15.
//
// Atributos minimos exigidos por docs/requirements/entities.md: Nombre, correo
// unico, contrasena con hash, rol y activo. Los tres ultimos atributos que el
// curso no define pero esta fase necesita estan marcados como decision propia.
//
// Esta entidad no sabe como se guarda: la contrasena entra ya hasheada y Domain
// no ve ningun paquete de criptografia. El hash es un valor opaco para ella.
public sealed class Usuario
{
    private Usuario()
    {
    }

    private Usuario(
        Guid id,
        string nombre,
        string correo,
        string hashDeContrasena,
        Rol rol,
        bool activo,
        DateTime fechaDeCreacionUtc)
    {
        Id = id;
        Nombre = nombre;
        Correo = correo;
        HashDeContrasena = hashDeContrasena;
        Rol = rol;
        Activo = activo;
        FechaDeCreacionUtc = fechaDeCreacionUtc;
    }

    public Guid Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string Correo { get; private set; } = string.Empty;

    // RF-CA-02: nunca la contrasena. El formato lo define quien hashea.
    public string HashDeContrasena { get; private set; } = string.Empty;

    // RF-CA-04: todo usuario tiene exactamente un rol, nunca ninguno.
    public Rol Rol { get; private set; }

    // RF-CA-15: el usuario nace inactivo. Sin pasar por aqui no hay sesion.
    public bool Activo { get; private set; }

    // Decision propia: RF-CA-15 lo declara "decisiones de diseno". Lo necesita
    // el listado de RF-CA-21 y el seed idempotente del primer Administrador.
    public DateTime FechaDeCreacionUtc { get; private set; }

    // Trazabilidad minima: null significa que la contrasena solo ha sido la
    // inicial; se establece cada vez que CambiarContrasena reemplaza el hash.
    public DateTime? ContrasenaCambiadaUtc { get; private set; }

    // Decision propia: RF-CA-12, RF-CA-18 y RF-CA-20 exige que cambiar la
    // contrasena o desactivar invalide las sesiones abiertas. Subir este numero
    // es lo que invalida todas las credenciales emitidas antes del cambio.
    public int CredencialVersion { get; private set; }

    // Decision propia: RF-CA-19, bloqueos tras cinco intentos fallidos.
    public int IntentosFallidos { get; private set; }

    public DateTime? BloqueoHastaUtc { get; private set; }

    // Una sola puerta de entrada para el registro publico: nada crea un usuario
    // escribiendolo a mano. El rol y el activo se fijan aqui y no como
    // parametros, porque RF-CA-15 exige que nazca inactivo y RF-CA-04 que nazca
    // con un rol.
    //
    // Esta es la UNICA excepcion a esa regla, y hay dos: CrearComoAdministrador.
    // Se llego a esta forma tras comparar tres, en docs/adr/002-primer-administrador.md.
    public static Usuario Crear(string nombre, string correo, string hashDeContrasena, DateTime ahoraUtc)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        }

        if (string.IsNullOrWhiteSpace(correo))
        {
            throw new ArgumentException("El correo es obligatorio.", nameof(correo));
        }

        if (string.IsNullOrWhiteSpace(hashDeContrasena))
        {
            throw new ArgumentException("El hash de la contrasena es obligatorio.", nameof(hashDeContrasena));
        }

        // Decision propia: el correo se guarda siempre en minusculas. Sin esto,
        // "Juan@Correo.com" y "juan@correo.com" serian dos personas distintas
        // para el indice unico de RF-CA-01. La normalizacion vive en la entidad,
        // en un solo sitio, para que buscar y guardar no puedan discrepar.
        return new Usuario(
            Guid.NewGuid(),
            nombre.Trim(),
            correo.Trim().ToLowerInvariant(),
            hashDeContrasena,
            Rol.Estandar,
            activo: false,
            ahoraUtc);
    }

    // La segunda puerta, y la unica que no es el registro publico. La necesita el
    // seed del primer Administrador, porque Crear fija Rol.Estandar de forma
    // fija y no hay forma de llegar a Administrador por el camino normal
    // (docs/adr/002-primer-administrador.md).
    //
    // Nace ACTIVO, al contrario que Crear. No es un descuido: este usuario no
    // llega por correo, asi que no tiene enlace de activacion que nadie pueda
    // abrir. Si naciera inactivo no habria forma de activarlo, porque activarlo
    // es una operacion de Administrador y no habria ningun Administrador.
    //
    // Los chequeos de nombre, correo y hash son los mismos que en Crear, y estan
    // repetidos a proposito: duplicar cuatro lineas es mas barato que un
    // constructor privado que una de las dos puertas puedan saltarse.
    public static Usuario CrearComoAdministrador(
        string nombre,
        string correo,
        string hashDeContrasena,
        DateTime ahoraUtc)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        }

        if (string.IsNullOrWhiteSpace(correo))
        {
            throw new ArgumentException("El correo es obligatorio.", nameof(correo));
        }

        if (string.IsNullOrWhiteSpace(hashDeContrasena))
        {
            throw new ArgumentException("El hash de la contrasena es obligatorio.", nameof(hashDeContrasena));
        }

        // El correo se normaliza igual que en Crear, para que las dos puertas
        // escriban el mismo formato y el indice unico de RF-CA-01 no tenga dos
        // formas de decidir que dos correos son la misma persona.
        return new Usuario(
            Guid.NewGuid(),
            nombre.Trim(),
            correo.Trim().ToLowerInvariant(),
            hashDeContrasena,
            Rol.Administrador,
            activo: true,
            ahoraUtc);
    }

    // RF-CA-16: abrir el enlace de activacion activa la cuenta. Solo desde
    // inactivo, para que activar dos veces el mismo usuario no tenga efecto.
    public void Activar()
    {
        if (Activo)
        {
            throw new InvalidOperationException("La cuenta ya esta activa.");
        }

        Activo = true;
    }

    // RF-CA-20: un Administrador desactiva y reactiva. Desactivar sube la
    // version de credencial, asi que las sesiones abiertas dejan de servir.
    public void Desactivar()
    {
        if (!Activo)
        {
            throw new InvalidOperationException("La cuenta ya esta inactiva.");
        }

        Activo = false;
        CredencialVersion++;
    }

    public void Reactivar()
    {
        if (Activo)
        {
            throw new InvalidOperationException("La cuenta ya esta activa.");
        }

        Activo = true;
    }

    // RF-CA-08: cambiar el rol lo hace un Administrador. El caso de uso decide
    // quien puede; la entidad solo se encarga de que el valor sea uno valido.
    public void CambiarRol(Rol nuevoRol)
    {
        Rol = nuevoRol;
    }

    // RF-CA-11 y RF-CA-13: cambiar la contrasena invalida las sesiones abiertas
    // (RF-CA-12) y pone a cero el contador de fallos (RF-CA-19).
    public void CambiarContrasena(string nuevoHash, DateTime ahoraUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nuevoHash);

        HashDeContrasena = nuevoHash;
        ContrasenaCambiadaUtc = ahoraUtc;
        CredencialVersion++;
        IntentosFallidos = 0;
        BloqueoHastaUtc = null;
    }

    // RF-CA-19: tras cinco intentos fallidos seguidos la cuenta queda bloqueada
    // quince minutos. El sexto intento se rechaza aunque la contrasena sea
    // correcta, porque el bloqueo se comprueba antes de verificar el hash.
    public void RegistrarIntentoFallido(int maximoIntentos, DateTime ahoraUtc, TimeSpan duracionDelBloqueo)
    {
        if (BloqueoHastaUtc is not null && ahoraUtc < BloqueoHastaUtc.Value)
        {
            return;
        }

        IntentosFallidos++;

        if (IntentosFallidos >= maximoIntentos)
        {
            BloqueoHastaUtc = ahoraUtc.Add(duracionDelBloqueo);
        }
    }

    // RF-CA-19: un inicio de sesion correcto pone el contador en cero.
    public void RegistrarIntentoCorrecto()
    {
        IntentosFallidos = 0;
        BloqueoHastaUtc = null;
    }

    public bool EstaBloqueado(DateTime ahoraUtc) =>
        BloqueoHastaUtc is not null && ahoraUtc < BloqueoHastaUtc.Value;
}