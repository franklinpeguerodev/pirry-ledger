using PirryLedger.Core.AccessControl.Application;
using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace PirryLedger.Core.AccessControl.Api;

// Lectura de la header Authorization: Bearer <token>.
//
// Vive aqui y no en cada endpoint porque el formato tiene que interpretarse igual
// en todos lados. Un endpoint que lo leyera por su cuenta tarde o temprano
// aceptaria "token" sin "Bearer" y se saltaria la validacion.
//
// El prefix se compara sin distinguir mayusculas porque el esquema HTTP lo
// especifica asi, y "bearer" tiene que funcionar igual que "Bearer".
internal static class TokenBearer
{
    private const string Scheme = "Bearer";

    public static string? Read(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out StringValues values))
        {
            return null;
        }

        var header = values.ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var parts = header.Split(' ', 2, StringSplitOptions.TrimEntries);

        // Sin esquema o sin token: se trata como "no hay credencial", y el punto
        // de validacion responde 401. No es un error de formato distinto.
        if (parts.Length != 2 || !IsBearer(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return null;
        }

        return parts[1];
    }

    private static bool IsBearer(string scheme) =>
        string.Equals(scheme, Scheme, StringComparison.OrdinalIgnoreCase);
}
