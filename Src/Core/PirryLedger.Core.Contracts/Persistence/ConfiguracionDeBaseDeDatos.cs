namespace PirryLedger.Core.Contracts.Persistence;

// Las dos piezas del Core comparten una base de datos, asi que comparten una
// sola cadena de conexion. Compartir la base no significa compartir las tablas:
// cada pieza es la unica que lee y escribe las suyas (prefijo ac_ para control
// de acceso, not_ para notificaciones).
//
// El nombre vive aqui para que el Host, las factorias en tiempo de diseno y las
// dos piezas usen exactamente el mismo y no se desincronicen.
public static class ConfiguracionDeBaseDeDatos
{
    // Nombre de la variable de entorno: ConnectionStrings__PirryLedger (RD-10).
    public const string VariableDeConexion = "ConnectionStrings__PirryLedger";

    // Clave dentro de IConfiguration, equivalente a la variable de arriba.
    public const string NombreDeConexion = "PirryLedger";
}
