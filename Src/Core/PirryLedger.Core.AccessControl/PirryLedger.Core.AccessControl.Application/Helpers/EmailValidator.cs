using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Application;

// RD-07: toda entrada externa se valida antes de usarse, y un dato invalido
// produce un rechazo controlado con un message, nunca una excepcion sin manejar
// ni un error de infraestructura.
//
// El registro de User es la primera puerta de entrada de la aplicacion, asi
// que es donde mas importa. El corrector de "@gmail.com,otro" es a proposito: sin
// Address.TryParse, un Email con dos arrobas pasaria el filtro de sintaxis de
// .NET y llegaria al indice unico como un registro mas.
public static class EmailValidator
{
    public const int MaximumLength = 320;

    public sealed record Result(bool IsValid, string? Reason)
    {
        public static Result Valid { get; } = new(true, null);
    }

    public static Result Evaluate(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return new Result(false, "El Email es obligatorio.");
        }

        var trimmed = email.Trim();

        if (trimmed.Length > MaximumLength)
        {
            return new Result(false, $"El Email no puede pasar de {MaximumLength} caracteres.");
        }

        if (trimmed.Any(char.IsWhiteSpace))
        {
            return new Result(false, "El Email no puede contener espacios.");
        }

        if (!System.Net.Mail.MailAddress.TryCreate(trimmed, out var address))
        {
            return new Result(false, "El Email no tiene un formato valido.");
        }

        if (!string.Equals(address.Address, trimmed, StringComparison.Ordinal))
        {
            // MailAddress acepta "Name@Email.com" y lo normaliza a otra cosa.
            // Si no coincide con lo que el User escribio, no es la direccion
            // que el User cree estarregistrando.
            return new Result(false, "El Email no tiene un formato valido.");
        }

        var atIndex = trimmed.LastIndexOf('@');

        // Con "User@dominio.com", el arroba va antes del ultimo punto del dominio
        // y el dominio tiene que tener al menos un punto.
        if (atIndex <= 0 ||
            atIndex == trimmed.Length - 1 ||
            trimmed.IndexOf('@') != atIndex ||
            trimmed.IndexOf('.', atIndex) < atIndex + 2)
        {
            return new Result(false, "El Email no tiene un formato valido.");
        }

        return Result.Valid;
    }
}
