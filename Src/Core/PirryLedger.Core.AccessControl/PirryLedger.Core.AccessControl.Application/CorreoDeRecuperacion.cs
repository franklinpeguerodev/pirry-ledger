namespace PirryLedger.Core.AccessControl.Application;

internal static class CorreoDeRecuperacion
{
    internal const string Asunto = "Restablece tu contraseña en Pirry Ledger";

    internal static string ArmarCuerpo(string codigo) =>
        $"""
         Hola,

         Usa este código para establecer una nueva contraseña en Pirry Ledger:
         {codigo}

         El código es de un solo uso y caduca en 15 minutos. Si no solicitaste
         este cambio, ignora este mensaje.

         -- equipo de Pirry Ledger
         """;
}
