# ADR 012 - Request a permission from a business operation

- **Estado:** aceptada
- **Fecha:** 2026-10-31
- **Requisitos:** RF-NEG-06; RF-GP-01; toca ADR 008 y ADR 011; RD-01, RD-03, RD-06
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

Anular una factura `Paid` o `InDebt` exige un permiso aprobado para esa factura
concreta (decisión 10 de `docs/current-iteration.md` y ADR 011). El permiso lo
pide quien cobra, lo aprueba un Administrador y se consume al usarlo.

En la fase 3 quedó resuelto el consumo: `IPermissionAuthority.ConsumeAsync`
borra la concesión y devuelve `false` cuando no hay ninguna. Faltaba la otra
mitad: cuando el cajero intenta anular sin concesión, la operación de negocio
tiene que **pedir el permiso en su nombre**, sin obligarlo a llamar antes al
endpoint `POST /api/permissions/requests`.

Ese endpoint existe y funciona, pero no sirve dentro de la operación de anular:
recibe un token, vive en la capa HTTP y obliga a Business a conocer la URL y el
formato de Permissions. Además, llamarlo desde el caso de uso dejaría la
solicitud creada y la anulación rechazada en dos transacciones distintas sin
ninguna garantía de orden.

La restricción de arquitectura manda: `Business.Application` solo puede
referenciar `Core.Contracts` (RD-03). No puede ver `Permissions.Application` ni
`AccessControl.Application`.

## Decisión

### 1. El contrato vive en Core.Contracts

```csharp
// Core.Contracts/Permissions/IPermissionRequestGateway.cs
public interface IPermissionRequestGateway
{
    Task<PermissionRequestOutcome> RequestAsync(
        Guid requesterId,
        string permissionName,
        CancellationToken cancellationToken = default);
}
```

`PermissionRequestOutcome` es un enum con dos valores: `Created` y
`AlreadyPending`. Business necesita distinguirlos para responder `403` o `409`
(ver punto 5), y un `bool` no lo permitiría.

### 2. La identidad viaja como Guid, no como token

El caso de uso de anular ya recibió el `Guid` del llamante desde la sesión, que
el filtro de acceso validó antes. Pasarle el token al gateway obligaría a
Business a depender de `Authenticate` (AccessControl.Application) para
resolverlo, y a repetir la validación de sesión dentro de una operación que ya
pasó el filtro.

El gateway tampoco valida la sesión: confía en la identidad que le pasa el caso
de uso. La validación sigue donde estaba (RD-06: en el servidor, en el filtro y
en `Authenticate`).

### 3. Solo una solicitud `Pending` bloquea

`ExistsPendingAsync(requesterId, permissionName)` consulta únicamente las filas
en estado `Pending`. Una solicitud `Rejected` no impide pedir el permiso otra
vez, y una ya aprobada y consumida tampoco: si el Administrador rechazó la
anulación o la concesión se usó, el cajero puede volver a intentarlo.

Si no hay `Pending`, el gateway crea la solicitud con `PermissionRequest.Create`
y la guarda con el reloj inyectado (RD-11). Si ya hay una, no crea nada y
devuelve `AlreadyPending`.

### 4. La implementación vive en Permissions

`PermissionRequestGateway` está en
`PirryLedger.Core.Permissions.Application.Services` y usa los puertos que ya
existían (`IPermissionRequestRepository`, `IPermissionAuthority`) más `IClock`.
Se registra en `AddPermissions` como
`IPermissionRequestGateway`; el Host no cambia nada más.

El gateway valida el catálogo con `IPermissionAuthority.IsKnown`: un nombre
inventado es un error de programación del llamante y lanza `ArgumentException`,
no un `400` HTTP. Un `400` sería una respuesta a una petición del cliente, y
aquí el cliente es otro caso de uso. Un `Guid.Empty` como solicitante se rechaza
igual.

### 5. El rechazo de la anulación: 403 o 409

El caso de uso `CancelInvoice` hace, en este orden (ADR 011):

1. resuelve la transición a `Cancelled` en memoria;
2. consume la concesión `CancelInvoice:<id>`;
3. si no la había, pide el permiso con el gateway y **no guarda** la factura.

La solicitud queda creada y la factura no cambia. El endpoint traduce el
resultado:

| Resultado del gateway | Código | Mensaje |
|---|---|---|
| `Created` | `403` | `Se envió una solicitud de permiso para anular esta factura. Un Administrador debe aprobarla.` |
| `AlreadyPending` | `409` | `Ya se envió una solicitud de permiso para anular esta factura; espera la aprobación del Administrador.` |

El `409` evita que dos intentos del mismo cajero llenen la bandeja del
Administrador con solicitudes idénticas, y le dice al cliente que no hace falta
volver a pedirla.

### 6. Sin migración

La tabla `per_permission_requests` y sus índices ya existen (ADR 008). El nuevo
método del puerto es una consulta; no hay columnas nuevas ni cambios de esquema.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Llamar por HTTP a `POST /api/permissions/requests` desde el caso de uso | Business necesitaría el token, la URL base y un cliente HTTP; dos transacciones sin orden; y el filtro volvería a validar la sesión. |
| Que `Business.Application` referencie `Permissions.Application` | Rompe RD-03 y convierte el módulo de negocio en dependiente de una implementación del Core. |
| Mover la creación de solicitudes a Business | Business escribiría en la tabla de Permissions: cada módulo es dueño de sus datos. |
| Devolver `bool` en vez de `PermissionRequestOutcome` | No se puede distinguir "se creó" de "ya había una", y el endpoint no sabría elegir entre `403` y `409`. |
| Que el gateway lance cuando ya hay una `Pending` | El gateway impondría una semántica HTTP (status y mensaje) que pertenece al caso de uso. |
| Poner el gateway en AccessControl | Las solicitudes de permiso son de Permissions; AccessControl solo valida sesión y rol. |
| Que cualquier estado de solicitud bloquee | Una solicitud rechazada dejaría al cajero sin poder volver a pedir el permiso de esa factura. |
| Que el caso de uso reciba el token y valide la sesión otra vez | Duplica la validación y obliga a Business a depender de `Authenticate`. |

## Consistencia y concurrencia

- **Un solo bloqueo por usuario y permiso, en el caso normal.** Si existe una
  `Pending`, no se crea otra. La prueba
  `SolicitarDosVecesElMismoPermisoDevuelveAlreadyPendingYNoDuplica` lo cubre.
- **Dos peticiones simultáneas.** `ExistsPendingAsync` y la inserción no son
  atómicas y `per_permission_requests` no tiene un índice único parcial sobre
  `(requester_id, permission_name) WHERE state = 'Pending'`. Dos anulaciones
  simultáneas del mismo cajero pueden crear dos `Pending`. Es inocuo: el
  Administrador aprueba una y rechaza la otra; la primera concesión habilita la
  anulación. Cerrarlo del todo exigiría una migración con un índice parcial, y
  esta fase no toca el esquema.
- **La factura no cambia cuando se pide el permiso.** El caso de uso rechaza
  antes de guardar, así que un intento sin permiso no deja rastro en `bil_`.
- **El permiso de otra factura no bloquea.** El nombre incluye el id de la
  factura, así que la solicitud de una factura no impide pedir la de otra.

## Verificación

- `dotnet build pirry-ledger.slnx` con 0 advertencias y 0 errores.
- `dotnet test pirry-ledger.slnx`: 297 pruebas superadas, 0 con error
  (Business 134, AccessControl 109, Permissions 48, Notifications 6).
- En Permissions, las pruebas nuevas:
  - `SolicitarUnPermisoCreaUnaSolicitudPendiente`,
    `SolicitarDosVecesElMismoPermisoDevuelveAlreadyPendingYNoDuplica`,
    `LaSolicitudPendienteDeOtroUsuarioNoBloquea`,
    `SolicitarUnPermisoDesconocidoSeRechaza` y
    `SolicitarSinSolicitanteSeRechaza`.
- En Business, la prueba de la operación que pide el permiso:
  `AnularSinPermisoCreaLaSolicitudYRechaza` y
  `AnularSinPermisoConUnaSolicitudPendienteNoLaDuplicaYRechaza`. Al quitar la
  línea de `ConsumeAsync` de `CancelInvoice`, las tres pruebas de anulación con
  permiso se ponen rojas: es la verificación del defecto 4.
- `dotnet ef migrations list --project
  Src\Core\PirryLedger.Core.Permissions\PirryLedger.Core.Permissions.Infrastructure`
  sigue mostrando una sola migración, `20261021024258_InitialSchema`.

## Consecuencias

**A favor**

- Business pide el permiso sin conocer Permissions: solo ve un contrato de
  `Core.Contracts`.
- La identidad sale de la sesión ya validada; no hay una segunda validación ni
  un token circulando por dentro.
- El `403` y el `409` distinguen "acabo de pedirlo" de "ya lo habías pedido", y
  el cajero sabe si debe esperar.
- Sin migración.

**En contra, asumido a conciencia**

- El gateway es un contrato más que mantener; una pieza nueva que pida permisos
  tiene que inyectarlo.
- Dos anulaciones simultáneas pueden crear dos `Pending` (ver concurrencia).
- El caso de uso hace dos escrituras en piezas distintas (solicitud y factura)
  sin transacción común; la factura se rechaza antes de guardar, así que el
  estado del negocio nunca queda a medias.

## Qué queda fuera

- El índice único parcial que cerraría la carrera de dos solicitudes.
- La aprobación automática o el aviso al Administrador por la nueva solicitud
  (RF-NEG-07, semana 10).
- La prueba de la carrera de dos anulaciones contra PostgreSQL.
- Cualquier operación de negocio distinta de la anulación que pida permisos.
