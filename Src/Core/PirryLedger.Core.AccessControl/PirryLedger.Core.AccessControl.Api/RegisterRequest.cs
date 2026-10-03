using System.ComponentModel.DataAnnotations;

namespace PirryLedger.Core.AccessControl.Api;

// Lo que llega por el cuerpo. Es un record aparte del de la entidad: la entidad
// nunca se enlaza directamente a una peticion (RD-02).
//
// Los atributos Required de DataAnnotations son la primera barrera, pero no la
// unica: el caso de uso vuelve a validar correo, contrasena y nombre. Un endpoint
// mal construido a mano pasa por los dos (RD-07), y por eso los mensajes de
// error los produce el caso de uso, no los atributos.
public sealed record RegisterRequest(
    [property: Required(ErrorMessage = "El nombre es obligatorio.")]
    string Nombre,

    [property: Required(ErrorMessage = "El correo es obligatorio.")]
    string Correo,

    [property: Required(ErrorMessage = "La contrasena es obligatoria.")]
    string Contrasena);

// RF-CA-17. Solo el correo: el nombre y la contrasena no hacen falta para reenviar
// un enlace, y pedirlos daria al usuario la sensacion de que hace falta.
public sealed record ResendActivationRequest(
    [property: Required(ErrorMessage = "El correo es obligatorio.")]
    string Correo);

// RF-CA-03. El cuerpo del login. Los mensajes de error los produce el caso de uso,
// no estos atributos, porque los tres rechazos posibles deben ser indistinguibles
// y el mensaje depende de mas cosas que de la forma del cuerpo.
public sealed record LoginRequest(
    [property: Required(ErrorMessage = "El correo es obligatorio.")]
    string Correo,

    [property: Required(ErrorMessage = "La contrasena es obligatoria.")]
    string Contrasena);

public sealed record PasswordRecoveryRequest(
    [property: Required(ErrorMessage = "El correo es obligatorio.")]
    string Correo);

public sealed record ResetPasswordRequest(
    [property: Required(ErrorMessage = "El codigo es obligatorio.")]
    string Codigo,
    [property: Required(ErrorMessage = "La contrasena es obligatoria.")]
    string Contrasena);

public sealed record ChangePasswordRequest(
    [property: Required(ErrorMessage = "La contrasena actual es obligatoria.")]
    string ContrasenaActual,
    [property: Required(ErrorMessage = "La nueva contrasena es obligatoria.")]
    string NuevaContrasena);