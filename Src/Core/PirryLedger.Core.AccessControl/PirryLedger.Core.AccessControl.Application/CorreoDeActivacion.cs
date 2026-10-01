namespace PirryLedger.Core.AccessControl.Application;

// El texto del correo de activacion y la URL del enlace viven aqui y no en cada
// caso de uso.
//
// El registro y el reenvio mandan el mismo mensaje. Si cada uno tuviera su propia
// copia, un cambio en el texto se aplicaria a un correo y se olvidaria del otro,
// y el profesor veria dos mensajes distintos segun por donde le llegara. Aqui la
// ruta /activar esta escrita una sola vez, que es lo que habria que cambiar si
// manana el endpoint se mueve.
internal static class CorreoDeActivacion
{
    internal const string Asunto = "Activa tu cuenta en Pirry Ledger";

    // El endpoint que atiende esta ruta esta en la capa Api. Las dos piezas se
    // prueban juntas en la verificacion manual que describe el README.
    private const string Ruta = "/activar";

    // _urlBase viene de PIRRY_LEDGER_PUBLIC_BASE_URL. Se recorta al final para no
    // terminar con "//activar".
    internal static string ArmarCuerpo(string urlBase, string tokenEnClaro) =>
        $"""
         Hola,

         Gracias por registrarte en Pirry Ledger.

         Activa tu cuenta con este enlace:
         {urlBase.TrimEnd('/')}{Ruta}?token={tokenEnClaro}

         El enlace es de un solo uso y caduca pronto. Si no te registraste, ignora
         este mensaje.

         -- equipo de Pirry Ledger
         """;
}