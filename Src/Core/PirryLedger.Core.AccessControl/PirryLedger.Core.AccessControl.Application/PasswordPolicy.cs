namespace PirryLedger.Core.AccessControl.Application;

// RF-CA-14: politica minima de contrasena, al menos 8 caracteres, con letras y
// numeros. El criterio dice "en el registro y en todo cambio de contrasena", asi
// que el unico sitio que decide es este y lo llaman todos los casos de uso.
//
// RD-07: un dato que no cumple produce un rechazo controlado con un mensaje que
// dice que falta, nunca una excepcion sin manejar ni un error de infraestructura.
public static class PasswordPolicy
{
    public const int LongitudMinima = 8;

    // Un solo resultado con todos los motivos, para que el mensaje sea el mismo
    // siempre y no dependa del orden en que se comprueben las reglas.
    public sealed record Resultado(bool EsValida, string? Motivo)
    {
        public static Resultado Valida { get; } = new(true, null);
    }

    public static Resultado Evaluar(string? contrasena)
    {
        if (string.IsNullOrEmpty(contrasena))
        {
            return new Resultado(false, "La contrasena es obligatoria.");
        }

        if (contrasena.Length < LongitudMinima)
        {
            return new Resultado(false, $"La contrasena debe tener al menos {LongitudMinima} caracteres.");
        }

        var tieneLetra = false;
        var tieneNumero = false;

        foreach (var caracter in contrasena)
        {
            if (char.IsLetter(caracter))
            {
                tieneLetra = true;
            }
            else if (char.IsDigit(caracter))
            {
                tieneNumero = true;
            }

            if (tieneLetra && tieneNumero)
            {
                return Resultado.Valida;
            }
        }

        if (!tieneLetra && !tieneNumero)
        {
            return new Resultado(false, "La contrasena debe tener letras y numeros.");
        }

        return new Resultado(false, "La contrasena debe tener letras y numeros.");
    }
}