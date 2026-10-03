using PirryLedger.Core.AccessControl.Domain;
using PirryLedger.Core.Contracts.Time;

namespace PirryLedger.Core.AccessControl.Application;

// Crea el primer Administrador de la base a partir de las variables de entorno
// (docs/adr/002-primer-administrador.md).
//
// Por que hace falta: el registro publico siempre fija Rol.Estandar, porque
// RF-CA-15 exige que nazca inactivo y un usuario recien registrado no puede
// gobernarse a si mismo. El resultado es que no hay forma de llegar a
// Administrador, y RF-CA-08, RF-CA-20 y RF-CA-21 no se pueden ejercitar sin uno.
//
// Esto NO es autorregistro. Lee una contrasena que el operador ya tiene, no una
// que viene de un formulario, y no encola ningun correo.
//
// El caso de uso es idempotente por el motivo que RF-NOT-12 exige al enviador y
// que aqui importa por lo mismo: arrancar la aplicacion dos veces no puede
// crear dos Administradores. Se resuelve preguntando si ya hay alguno, no
// contando.
//
// Los mensajes de excepcion no llevan el correo ni la contrasena (RD-08).
public sealed class SeedFirstAdministrator
{
    private readonly IUserRepository _usuarios;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _reloj;

    public SeedFirstAdministrator(
        IUserRepository usuarios,
        IPasswordHasher hasher,
        IClock reloj)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _reloj = reloj;
    }

    // Devuelve true si creo la cuenta, false si no habia nada que hacer porque ya
    // existia un Administrador. Devolver el booleano y no lanzar en el caso
    // normal es lo que permite al Host imprimir una sola linea y seguir
    // arrancando.
    public async Task<bool> EjecutarAsync(
        string nombre,
        string correo,
        string contrasena,
        CancellationToken cancellationToken = default)
    {
        var correoValido = EmailValidator.Evaluar(correo);

        if (!correoValido.EsValido)
        {
            throw new FirstAdministratorNotSeededException(correoValido.Motivo!);
        }

        // Se reutiliza la politica del registro (RF-CA-14) en vez de duplicarla.
        // La contrasena del Administrador sale de una variable de entorno, no de
        // un formulario, asi que no hay reglas mas debiles que aplicar.
        var politica = PasswordPolicy.Evaluar(contrasena);

        if (!politica.EsValida)
        {
            throw new FirstAdministratorNotSeededException(politica.Motivo!);
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new FirstAdministratorNotSeededException("El nombre es obligatorio.");
        }

        // Idempotencia: si ya hay un Administrador, esta operacion ya se hizo. No
        // se compara el correo porque un seed distinto apuntando a otra cuenta
        // tampoco debe crear un segundo Administrador por accidente.
        if (await _usuarios.ExisteAlgunAdministradorAsync(cancellationToken))
        {
            return false;
        }

        var correoNormalizado = correo.Trim();

        // El correo del seed podria existir como Estándar, por ejemplo si alguien
        // se registro con ese correo antes de que existiera el seed. Se rechaza en
        // vez de promover la cuenta: cambiarle el rol a alguien que se registro
        // solo es justo el privilege escalation que RF-CA-08 quiere impedir.
        var existente = await _usuarios.BuscarPorCorreoAsync(correoNormalizado, cancellationToken);

        if (existente is not null)
        {
            throw new FirstAdministratorNotSeededException(
                "Ya existe una cuenta con ese correo y no es Administrador. " +
                "Usa otro correo para el primer Administrador.");
        }

        // RF-CA-02: el mismo hasher del registro, con su sal por usuario.
        var hash = _hasher.Hash(contrasena);

        var administrador = Usuario.CrearComoAdministrador(
            nombre,
            correoNormalizado,
            hash,
            _reloj.UtcNow);

        await _usuarios.GuardarAsync(administrador, cancellationToken);

        return true;
    }
}