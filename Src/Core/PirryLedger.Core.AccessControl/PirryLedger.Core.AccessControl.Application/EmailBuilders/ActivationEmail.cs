using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Application;

// El texto del Email de activacion y la URL del enlace viven aqui y no en cada
// caso de uso.
//
// El registro y el reenvio mandan el mismo message. Si cada uno tuviera su propia
// copia, un cambio en el texto se aplicaria a un Email y se olvidaria del otro,
// y el profesor veria dos mensajes distintos segun por donde le llegara. Aqui la
// route /activate esta escrita una sola vez, que es lo que habria que cambiar si
// manana el endpoint se mueve.
internal static class ActivationEmail
{
    internal const string Subject = "Activa tu cuenta en Pirry Ledger";

    // El endpoint que atiende esta route esta en la capa Api. Las dos piezas se
    // prueban juntas en la verificacion manual que describe el README.
    private const string Route = "/activate";

    // _baseUrl viene de PIRRY_LEDGER_PUBLIC_BASE_URL. Se recorta al final para no
    // terminar con "//activate".
    internal static string BuildBody(string baseUrl, string plainToken) =>
        $"""
         Hola,

         Gracias por registrarte en Pirry Ledger.

         Activa tu cuenta con este enlace:
         {baseUrl.TrimEnd('/')}{Route}?token={plainToken}

         El enlace es de un solo uso y caduca pronto. Si no te registraste, ignora
         este message.

         -- equipo de Pirry Ledger
         """;
}
