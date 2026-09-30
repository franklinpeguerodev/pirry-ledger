namespace PirryLedger.Core.AccessControl.Application;

// RD-07: toda entrada externa se valida antes de usarse, y un dato invalido
// produce un rechazo controlado con un mensaje, nunca una excepcion sin manejar
// ni un error de infraestructura.
//
// El registro de usuario es la primera puerta de entrada de la aplicacion, asi
// que es donde mas importa. El corrector de "@gmail.com,otro" es a proposito: sin
// Address.TryParse, un correo con dos arrobas pasaria el filtro de sintaxis de
// .NET y llegaria al indice unico como un registro mas.
public static class EmailValidator
{
    public const int LongitudMaxima = 320;

    public sealed record Resultado(bool EsValido, string? Motivo)
    {
        public static Resultado Valido { get; } = new(true, null);
    }

    public static Resultado Evaluar(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo))
        {
            return new Resultado(false, "El correo es obligatorio.");
        }

        var recortado = correo.Trim();

        if (recortado.Length > LongitudMaxima)
        {
            return new Resultado(false, $"El correo no puede pasar de {LongitudMaxima} caracteres.");
        }

        if (recortado.Any(char.IsWhiteSpace))
        {
            return new Resultado(false, "El correo no puede contener espacios.");
        }

        if (!System.Net.Mail.MailAddress.TryCreate(recortado, out var direccion))
        {
            return new Resultado(false, "El correo no tiene un formato valido.");
        }

        if (!string.Equals(direccion.Address, recortado, StringComparison.Ordinal))
        {
            // MailAddress acepta "nombre@correo.com" y lo normaliza a otra cosa.
            // Si no coincide con lo que el usuario escribio, no es la direccion
            // que el usuario cree estarregistrando.
            return new Resultado(false, "El correo no tiene un formato valido.");
        }

        var arroba = recortado.LastIndexOf('@');

        // Con "User@dominio.com", el arroba va antes del ultimo punto del dominio
        // y el dominio tiene que tener al menos un punto.
        if (arroba <= 0 ||
            arroba == recortado.Length - 1 ||
            recortado.IndexOf('@') != arroba ||
            recortado.IndexOf('.', arroba) < arroba + 2)
        {
            return new Resultado(false, "El correo no tiene un formato valido.");
        }

        return Resultado.Valido;
    }
}