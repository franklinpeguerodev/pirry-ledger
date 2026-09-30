using PirryLedger.Core.AccessControl.Domain;
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
    private readonly IUserRepository _usuarios;
    private readonly IActivationTokenRepository _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailQueue _colaDeCorreo;
    private readonly IClock _reloj;
    private readonly string _urlBase;

    public RegisterUser(
        IUserRepository usuarios,
        IActivationTokenRepository tokens,
        IPasswordHasher hasher,
        IEmailQueue colaDeCorreo,
        IClock reloj,
        string urlBase)
    {
        _usuarios = usuarios;
        _tokens = tokens;
        _hasher = hasher;
        _colaDeCorreo = colaDeCorreo;
        _reloj = reloj;
        _urlBase = urlBase;
    }

    public async Task EjecutarAsync(
        string nombre,
        string correo,
        string contrasena,
        TimeSpan duracionDeValidez,
        CancellationToken cancellationToken = default)
    {
        var correoValido = EmailValidator.Evaluar(correo);

        if (!correoValido.EsValido)
        {
            throw new RegistrationRejectedException(correoValido.Motivo!);
        }

        var politica = PasswordPolicy.Evaluar(contrasena);

        if (!politica.EsValida)
        {
            throw new RegistrationRejectedException(politica.Motivo!);
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new RegistrationRejectedException("El nombre es obligatorio.");
        }

        var correoNormalizado = correo.Trim();

        // RF-CA-01: el indice unico de la base es la garantia real. Esta
        // comprobacion da un mensaje controlado antes de llegar alla, y el
        // indice cubre el caso de dos registros concurrentes.
        var existente = await _usuarios.BuscarPorCorreoAsync(correoNormalizado, cancellationToken);

        if (existente is not null)
        {
            throw new DuplicateEmailException();
        }

        // RF-CA-02: se hashea aqui y el resto del sistema solo ve el hash.
        var hash = _hasher.Hash(contrasena);

        // RF-CA-15: nace inactivo y con rol Estandar, ambos fijados por la
        // entidad, no por parametros de este metodo.
        var usuario = Usuario.Crear(nombre, correoNormalizado, hash, _reloj.UtcNow);

        await _usuarios.GuardarAsync(usuario, cancellationToken);

        var tokenEnClaro = OneTimeCodeGenerator.Generar();

        var token = TokenActivacion.Crear(
            usuario.Id,
            OneTimeCodeGenerator.CalcularHash(tokenEnClaro),
            _reloj.UtcNow,
            duracionDeValidez);

        await _tokens.GuardarAsync(token, cancellationToken);

        // RF-NOT-08: se encola y la operacion termina. El token en claro sale
        // ahora, dentro del correo, y no se guarda en ningun sitio mas.
        await _colaDeCorreo.EnqueueAsync(
            new QueuedEmail(
                correoNormalizado,
                CorreoDeActivacion.Asunto,
                CorreoDeActivacion.ArmarCuerpo(_urlBase, tokenEnClaro)),
            cancellationToken);
    }
}