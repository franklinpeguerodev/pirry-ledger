# ADR 011 - Consume a permission grant

- **Estado:** aceptada
- **Fecha:** 2026-10-30
- **Requisitos:** RF-GP-05; RF-NEG-06; toca ADR 008; RD-04, RD-06
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

Anular una factura `Paid` o `InDebt` exige un permiso aprobado para esa
factura concreta (decisión 10 de `docs/current-iteration.md`). El permiso lo
pide quien cobra, lo aprueba un Administrador y se consume al usarlo: cada
anulación pide el suyo. ADR 008 dejó la tabla `per_permission_grants` y el
índice único `(user_id, permission_name)`, y anotó que una revocación sería un
`DELETE` simple, pero no existía ninguna operación que borrara una concesión.

El catálogo de permisos es una lista cerrada con un solo nombre fijo,
`ViewSalesSummary`. Un permiso por factura no puede ser una entrada más de esa
lista: el número de facturas no se conoce al compilar. Hace falta una regla de
nombre, no un nombre más.

Además, el consumo y la anulación viven en piezas distintas: Business resuelve
la transición de la factura y Permissions borra la concesión. Hay que decidir
en qué orden ocurren y qué pasa si una de las dos partes falla.

## Decisión

### 1. El nombre del permiso

El permiso se llama `CancelInvoice:<id de la factura>`, con el GUID en formato
`D` y en minúsculas. Son 50 caracteres (14 del prefijo y 36 del GUID) y caben
en el `varchar(64)` de `permission_name` (ADR 008).

La forma es canónica: mayúsculas, llaves, espacios o cualquier otra variante
del GUID se rechazan. Así una factura tiene **un solo** nombre de permiso y no
se puede pedir dos veces con dos textos distintos para la misma factura.

### 2. El catálogo

`PermissionCatalog` acepta dos clases de nombre:

- los fijos de la lista cerrada (`ViewSalesSummary`);
- los que cumplen la regla `CancelInvoice:<guid canónico>`.

Cualquier otro nombre sigue rechazándose con `400` al crear la solicitud. La
convención se implementa dos veces a propósito: `PermissionNames` en
`Core.Contracts` (la usa Business para construir el nombre) y
`PermissionCatalog` en `Permissions.Domain` (la valida). Se mantienen separadas
para no crear la referencia `Permissions.Domain -> Core.Contracts`, que hoy no
existe en ningún dominio del repositorio. Una prueba del módulo,
`PruebasDelCatalogoDePermisos`, ata las dos implementaciones: el nombre que
construye una lo acepta la otra, y la misma matriz de nombres mal formados la
rechazan las dos.

### 3. La operación de consumo

`IPermissionAuthority` gana una operación:

```csharp
Task<bool> ConsumeAsync(
    Guid userId,
    string permissionName,
    CancellationToken cancellationToken = default);
```

Borra el par `(user_id, permission_name)` y devuelve `true` si había una fila,
`false` si no. **No lanza** cuando no hay concesión: "no tenía el permiso" es un
resultado normal, y el mensaje al usuario lo decide el caso de uso que anula la
factura. Con usuario vacío o nombre vacío devuelve `false` sin tocar la base.

Solo borra el par del usuario que llama. La concesión de otro usuario para la
misma factura es su propio permiso y no se toca; la de otra factura tampoco.

### 4. El orden: transición y después consumo

En la anulación (fase 4) la operación hace, en este orden:

1. resuelve la transición de la factura a `Cancelled` en memoria;
2. consume la concesión;
3. guarda la factura.

Si el consumo devuelve `false`, se rechaza **antes** de guardar: la factura no
cambia y el usuario recibe el rechazo por falta de permiso. Si el guardado de
la factura falla después del consumo, la concesión se pierde y hay que pedir
otra. Es el coste aceptado de este orden: se prefiere perder un permiso a dejar
viva la concesión de una factura ya anulada, que podría reutilizarse.

### 5. Sin migración

La tabla y el índice único ya existen (ADR 008). Consumir es un `DELETE` del
par; no hay columnas nuevas ni cambios de esquema. La migración de Permissions
sigue siendo una sola.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Un permiso fijo `CancelInvoice` con el id de la factura en el cuerpo de la solicitud | Una sola concesión abriría todas las facturas: el permiso dejaría de ser "para esa factura". |
| Una tabla `per_invoice_cancellations(invoice_id, user_id)` en Permissions | Permissions conocería facturas, que son datos de Business (RD-01), y exigiría una migración nueva. |
| Consumir la concesión antes de resolver la transición | Si la transición falla (por ejemplo, la factura ya está anulada), el permiso se pierde sin haber anulado nada. |
| Consumir la concesión después de guardar la factura | Una factura anulada puede quedar con la concesión viva; el orden B prefiere lo contrario. |
| No consumir: la concesión se queda y se reutiliza | Rompe "cada anulación pide su propio permiso" y convierte el permiso en permanente para esa factura. |
| Devolver una excepción cuando no hay concesión | El contrato impondría un mensaje y un código que pertenecen al caso de uso de la anulación, no a Permissions. |
| Una interfaz aparte `IPermissionGrantConsumer` | Duplica el mismo adaptador y obliga a inyectar dos contratos para la misma tabla. `IPermissionAuthority` es un contrato interno, no una API pública. |
| Referencia `Permissions.Domain -> Core.Contracts` para compartir el prefijo (opción A) | Funciona y deja una sola implementación, pero rompe el patrón del repositorio: hoy ningún dominio referencia otro proyecto. Se prefirió duplicar el literal y atar las dos copias con una prueba (opción B). |
| Mover `PermissionCatalog` a `Core.Contracts` | Una sola fuente y ninguna referencia nueva, pero mueve un archivo, su espacio de nombres y sus usos, y cambia la propiedad del catálogo. Más diff del que la fase necesita. |

## Consistencia y concurrencia

- **Una sola vez.** La primera llamada borra la fila y devuelve `true`; la
  segunda no encuentra nada y devuelve `false`. La prueba lo cubre con el
  repositorio real sobre EF InMemory.
- **Dos peticiones simultáneas.** Las dos pueden leer la fila antes de borrar.
  El `DELETE` de la segunda afecta a cero filas y EF lanza
  `DbUpdateConcurrencyException`; el repositorio la traduce a `false`. El
  perdedor recibe "ya no hay concesión", que es la verdad. Esa carrera no se
  puede reproducir con EF InMemory; se probará con PostgreSQL en la fase 4.
- **Fallo después del consumo.** Si la factura no se guarda, la concesión ya no
  está. Se acepta: volver a pedir el permiso es barato y la alternativa deja
  una concesión viva sobre una factura terminal.
- **Sin auditoría del consumo.** No se registra quién consumió ni cuándo; la
  auditoría llega en la semana 14. La fila de la solicitud original conserva la
  historia de la aprobación.

## Verificación

- `dotnet build pirry-ledger.slnx` con 0 advertencias y 0 errores.
- `dotnet test pirry-ledger.slnx`: 249 pruebas superadas, 0 con error
  (Business 104, AccessControl 96, Permissions 43, Notifications 6).
- En Permissions, las pruebas nuevas:
  - catálogo: `ElCatalogoAceptaElPermisoDeAnularUnaFacturaConcreta`,
    `ElCatalogoRechazaElGuidEnMayusculasParaQueHayaUnSoloNombrePorFactura`,
    `ElCatalogoRechazaUnPermisoDeAnulacionMalFormado` (seis casos) y
    `ElCatalogoSigueRechazandoUnNombreInventado`;
  - consumo: `ConsumirUnaConcesionLaBorraYDevuelveTrue`,
    `ConsumirDosVecesLaMismaConcesionLaSegundaDevuelveFalse`,
    `ConsumirLaConcesionDeUnaFacturaNoBorraLaDeOtra`,
    `ConsumirLaConcesionDeOtroUsuarioNoLaBorra`,
    `ConsumirSinConcesionDevuelveFalseYNoCambiaNada`,
    `LaAutoridadDejaDeVerElPermisoDespuesDeConsumirlo` y
    `LaAutoridadNoConsumeConUsuarioVacio`.
- `dotnet ef migrations list --project
  Src\Core\PirryLedger.Core.Permissions\PirryLedger.Core.Permissions.Infrastructure`
  sigue mostrando una sola migración, `20261021024258_InitialSchema`.

## Consecuencias

**A favor**

- El permiso es de una sola factura y de un solo uso; no hay forma de reutilizarlo
  en otra factura ni de pedir dos nombres para la misma.
- El contrato no obliga a Business a conocer las tablas de Permissions: solo
  pide consumir un par.
- Sin migración: la fase es pequeña y no toca el esquema.
- El orden B no deja concesiones vivas sobre facturas anuladas.

**En contra, asumido a conciencia**

- La convención está implementada dos veces (dominio y contratos). La prueba
  las ata, pero un cambio futuro toca dos archivos.
- Si el guardado de la factura falla después del consumo, el permiso se pierde.
- La concesión de otro usuario para la misma factura queda viva aunque la
  factura ya esté anulada; es inútil porque el estado es terminal.
- No hay auditoría del consumo hasta la semana 14.

## Qué queda fuera

- La operación de negocio que anula la factura y consume la concesión: fase 4.
- Los endpoints y las reglas de rol: fase 4.
- La revocación de permisos como operación de negocio y la limpieza de
  concesiones de facturas terminales.
- La auditoría de quién consumió una concesión (semana 14).
- La prueba de la carrera de dos consumos contra PostgreSQL (fase 4).
