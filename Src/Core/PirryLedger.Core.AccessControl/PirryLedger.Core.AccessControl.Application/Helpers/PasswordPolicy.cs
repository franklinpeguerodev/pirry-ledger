using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-14: politica minima de contrasena, al menos 8 caracteres, con letras y
// numeros. El criterio dice "en el registro y en todo cambio de contrasena", asi
// que el unico sitio que decide es este y lo llaman todos los casos de uso.
//
// RD-07: un dato que no cumple produce un rechazo controlado con un message que
// dice que falta, nunca una excepcion sin manejar ni un error de infraestructura.
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    // Un solo resultado con todos los motivos, para que el message sea el mismo
    // siempre y no dependa del orden en que se comprueben las reglas.
    public sealed record Result(bool IsValid, string? Reason)
    {
        public static Result Valid { get; } = new(true, null);
    }

    public static Result Evaluate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new Result(false, "La contrasena es obligatoria.");
        }

        if (password.Length < MinimumLength)
        {
            return new Result(false, $"La contrasena debe tener al menos {MinimumLength} caracteres.");
        }

        var hasLetter = false;
        var hasDigit = false;

        foreach (var character in password)
        {
            if (char.IsLetter(character))
            {
                hasLetter = true;
            }
            else if (char.IsDigit(character))
            {
                hasDigit = true;
            }

            if (hasLetter && hasDigit)
            {
                return Result.Valid;
            }
        }

        if (!hasLetter && !hasDigit)
        {
            return new Result(false, "La contrasena debe tener letras y numeros.");
        }

        return new Result(false, "La contrasena debe tener letras y numeros.");
    }
}
