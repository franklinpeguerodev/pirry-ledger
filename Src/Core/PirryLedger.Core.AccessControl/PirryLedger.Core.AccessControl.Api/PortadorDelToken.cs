using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using PirryLedger.Core.AccessControl.Application;

namespace PirryLedger.Core.AccessControl.Api;

// Lectura de la cabecera Authorization: Bearer <token>.
//
// Vive aqui y no en cada endpoint porque el formato tiene que interpretarse igual
// en todos lados. Un endpoint que lo leyera por su cuenta tarde o temprano
// aceptaria "token" sin "Bearer" y se saltaria la validacion.
//
// El prefijo se compara sin distinguir mayusculas porque el esquema HTTP lo
// especifica asi, y "bearer" tiene que funcionar igual que "Bearer".
internal static class PortadorDelToken
{
    private const string Esquema = "Bearer";

    public static string? Leer(HttpRequest peticion)
    {
        if (!peticion.Headers.TryGetValue("Authorization", out StringValues valores))
        {
            return null;
        }

        var cabecera = valores.ToString();

        if (string.IsNullOrWhiteSpace(cabecera))
        {
            return null;
        }

        var partes = cabecera.Split(' ', 2, StringSplitOptions.TrimEntries);

        // Sin esquema o sin token: se trata como "no hay credencial", y el punto
        // de validacion responde 401. No es un error de formato distinto.
        if (partes.Length != 2 || !EsBearer(partes[0]) || string.IsNullOrWhiteSpace(partes[1]))
        {
            return null;
        }

        return partes[1];
    }

    private static bool EsBearer(string esquema) =>
        string.Equals(esquema, Esquema, StringComparison.OrdinalIgnoreCase);
}