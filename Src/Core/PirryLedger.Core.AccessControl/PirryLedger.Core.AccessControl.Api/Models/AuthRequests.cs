using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace PirryLedger.Core.AccessControl.Api;

// RF-CA-17. Solo el Email: el Name y la contrasena no hacen falta para reenviar
// un enlace, y pedirlos daria al User la sensacion de que hace falta.
public sealed record ResendActivationRequest(
    [property: Required(ErrorMessage = "El Email es obligatorio.")]
    string Email);

// RF-CA-03. El cuerpo del login. Los mensajes de error los produce el caso de uso,
// no estos atributos, porque los tres rechazos posibles deben ser indistinguibles
// y el message depende de mas cosas que de la forma del cuerpo.
public sealed record LoginRequest(
    [property: Required(ErrorMessage = "El Email es obligatorio.")]
    string Email,

    [property: Required(ErrorMessage = "La contrasena es obligatoria.")]
    string Password);

public sealed record PasswordRecoveryRequest(
    [property: Required(ErrorMessage = "El Email es obligatorio.")]
    string Email);

public sealed record ResetPasswordRequest(
    [property: Required(ErrorMessage = "El codigo es obligatorio.")]
    string Code,
    [property: Required(ErrorMessage = "La contrasena es obligatoria.")]
    string Password);

public sealed record ChangePasswordRequest(
    [property: Required(ErrorMessage = "La contrasena actual es obligatoria.")]
    string CurrentPassword,
    [property: Required(ErrorMessage = "La nueva contrasena es obligatoria.")]
    string NewPassword);
