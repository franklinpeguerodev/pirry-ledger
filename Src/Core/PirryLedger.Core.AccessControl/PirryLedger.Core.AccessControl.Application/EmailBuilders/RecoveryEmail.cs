using PirryLedger.Core.AccessControl.Domain.Entities;
using PirryLedger.Core.AccessControl.Domain.Enums;

namespace PirryLedger.Core.AccessControl.Application;

internal static class RecoveryEmail
{
    internal const string Subject = "Restablece tu contraseña en Pirry Ledger";

    internal static string BuildBody(string code) =>
        $"""
         Hola,

         Usa este código para establecer una nueva contraseña en Pirry Ledger:
         {code}

         El código es de un solo uso y caduca en 15 minutos. Si no solicitaste
         este cambio, ignora este message.

         -- equipo de Pirry Ledger
         """;
}
