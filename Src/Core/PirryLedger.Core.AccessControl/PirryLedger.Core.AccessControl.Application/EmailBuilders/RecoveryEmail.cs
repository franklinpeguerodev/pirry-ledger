using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Application;

internal static class RecoveryEmail
{
    internal const string Subject = "Restablece tu contraseña en Pirry Ledger";

    // La duracion entra como parametro y no se escribe a mano: ADR 014 la subio
    // de quince minutos a cuatro horas, y el texto tiene que seguirla.
    internal static string BuildBody(string code, TimeSpan validity) =>
        $"""
         Hola,

         Usa este código para establecer una nueva contraseña en Pirry Ledger:
         {code}

         El código es de un solo uso y caduca en
         {RecoveryCodePolicy.Describe(validity)}. Si no solicitaste este cambio,
         ignora este message.

         -- equipo de Pirry Ledger
         """;
}
