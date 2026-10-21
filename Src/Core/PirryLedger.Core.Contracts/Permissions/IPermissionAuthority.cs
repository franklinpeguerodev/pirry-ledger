namespace PirryLedger.Core.Contracts.Permissions;

// Punto de contacto de la pieza de Permissions con el resto del sistema
// (RF-GP-05, current-iteration.md punto 3). Otras piezas consultan si un
// usuario tiene un permiso sin tocar las tablas de la pieza.
//
// Vive en Core.Contracts para que cualquier modulo pueda depender solo del
// contrato, no de la implementacion (RD-03). La implementacion esta en
// PirryLedger.Core.Permissions.Infrastructure.
public interface IPermissionAuthority
{
    // RF-GP-01: el nombre del permiso es uno de los del catalogo. Si no, la
    // peticion que lo uso se rechaza en el caso de uso, no en el filtro.
    bool IsKnown(string permissionName);

    // RF-GP-05: responde si el usuario tiene el permiso concedido ahora mismo.
    // La consulta operativa se hace sobre la tabla de concesiones del modulo
    // de Permissions; la maquina de estados de la solicitud no entra aqui.
    Task<bool> UserHasPermissionAsync(Guid userId, string permissionName, CancellationToken cancellationToken = default);
}