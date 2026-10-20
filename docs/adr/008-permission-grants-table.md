# ADR 008 - Permission grants live in their own table

- **Estado:** aceptada
- **Fecha:** 2026-10-20
- **Requisitos:** RF-GP-05; RD-09; indirecamente, RF-NEG-09 (la máquina de estados no crece para responder "qué permisos tiene un usuario ahora")
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

La pieza de Gestión de permisos necesita responder dos preguntas distintas,
y la respuesta a cada una tiene un consumidor diferente:

1. **"¿Esta solicitud pasó por todas las transiciones correctas y quién la
   resolvió?"** — lo consume la auditoría de la resolución (futura, semana 14)
   y la trazabilidad mínima que ya exige RF-GP-03 / RF-GP-04. La respuesta
   natural vive en `per_permission_requests` (cada fila es una transición con
   su aprobador y su fecha).
2. **"¿El usuario X tiene el permiso P **ahora mismo**?"** — lo consume el
   filtro de la ruta de demostración (`ViewSalesSummary`) y, mañana, cualquier
   pieza que decida si concede o no una acción. La respuesta natural es un
   EXISTS sobre una vista operativa.

Con una sola tabla (`per_permission_requests`) las dos preguntas compiten
por el mismo esquema: responder la segunda exige filtrar por `state =
'Applied'`, lo que ata la consulta operacional a las transiciones del modelo
de solicitudes. Si en el futuro se añade revocación, esa consulta tiene que
volverse más compleja (¿un nuevo estado "Revocado"? ¿un "Revocado" sin
transición formal?), y la tabla de solicitudes empieza a absorber
preocupaciones que no son suyas.

La Asignación 2 (RF-GP-05) implementa una sola ruta con un solo permiso, y
la revocación no se entrega esta semana. Decidido el 2026-10-20 con
Franklin: a pesar de la simplicidad actual, las concesiones se persisten
desde el día uno en su propia tabla, por dos razones concretas:

- **Revocación rápida.** Una `DELETE FROM per_permission_grants WHERE ...` es
  una operación trivial que no toca la máquina de estados de la solicitud.
  La historia de la solicitud (quién la creó, quién la aprobó, cuándo) se
  queda intacta en `per_permission_requests`. Si la revocación llega en
  semanas futuras, no hay que decidir cómo afecta al modelo de estados.
- **Trazabilidad operativa.** "Quién le dio el permiso a este usuario y
  cuándo" sale directamente de `per_permission_grants` con
  `(granted_by_user_id, granted_at_utc)`, sin join con la solicitud. Si la
  auditoría (semana 14) necesita reconstruirlo, tiene la fila a la mano.

## Decisión

Cuando una `PermissionRequest` transiciona de `Approved` a `Applied` dentro
de `ApprovePermissionRequest`, **la misma transacción** inserta una fila en
`per_permission_grants`:

```sql
INSERT INTO per_permission_grants (id, user_id, permission_name, granted_at_utc, granted_by_user_id)
VALUES (@id, @requester_id, @requested_permission, @applied_at_utc, @approver_id);
```

`IPermissionAuthority.UserHasPermissionAsync(userId, permissionName)` deja
de consultar `per_permission_requests` y pasa a ser:

```sql
SELECT EXISTS (
    SELECT 1 FROM per_permission_grants
    WHERE user_id = @u AND permission_name = @p
);
```

`per_permission_requests` conserva toda la historia de transiciones (solicitante,
permiso solicitado, estado, aprobador, fechas de creación / aprobación /
aplicación / rechazo, motivo del rechazo). `per_permission_grants` es solo la
vista operativa de "qué permisos tiene cada usuario en este momento".

### Por qué dos tablas y no una sola con un campo `granted bool`

La separación no es por estética: cada tabla responde a una pregunta y se
actualiza por motivos distintos.

- `per_permission_requests` se modifica por transiciones del estado
  (`Pending → Approved`, `Approved → Applied`, `Pending → Rejected`); las
  reglas de transición están en `PermissionRequestStateMachine`.
- `per_permission_grants` se modifica por concesiones y revocaciones; las
  reglas de quién concede y cuándo están en los casos de uso que aún no
  existen (semanas futuras).

Mezclar las dos en una sola tabla obliga a una de las dos preocupaciones a
ganarle a la otra en cada cambio. Por ejemplo, "revoqué este permiso"
podría modelarse como un nuevo estado `Revoked` en
`PermissionRequestStateMachine`, lo que añadiría una transición que no es
una transición de la solicitud — es una transición de la concesión. RF-NEG-09
quedaría amenazado sin que el cambio lo pidiera explícitamente.

### Por qué el INSERT ocurre dentro de `ApprovePermissionRequest`

RF-GP-05 exige que la aprobación aplique el permiso automáticamente, sin
ninguna intervención manual adicional. Si el INSERT en
`per_permission_grants` ocurriera en otro proceso (un worker, un comando
separado), habría una ventana en la que la solicitud está `Applied` pero
`UserHasPermissionAsync` aún devuelve `false`, y la ruta de demostración
negaría el acceso al solicitante recién aprobado. La operación que aplica el
permiso es la misma que inserta la fila: una transacción, un commit.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Derivar lo concedido de `per_permission_requests` (opción Y de la propuesta) | Con una sola tabla, responder "quién tiene qué ahora" exige filtrar por `state = 'Applied'` en cada EXISTS, y revocar exige inventar un nuevo estado o un cambio adicional que no es una transición de la solicitud. La tabla de solicitudes empieza a absorber responsabilidades que no son suyas. Sin revocación implementada hoy, las dos opciones funcionan; con la decisión de Franklin de preparar la revocación desde el día uno, esta alternativa queda descartada. |
| Tabla `per_permission_grants` con un campo `active` para revocar | Más estados y más código. Para una pieza sin revocación activa, es prematuro. Cuando la revocación llegue, se decide si `active` es la forma (probablemente no: una `DELETE` es más limpia que un `UPDATE ... SET active = false`). |
| Reutilizar `ac_users` con un campo `granted_permissions jsonb` | El módulo de Permissions escribiría en una tabla de AccessControl. Rompe la frontera de módulos (RD-01) y ata dos piezas que la iteración actual quiere mantener independientes. |
| Cache en memoria del proceso (`IMemoryCache`) en lugar de tabla | Sin `dotnet ef` que cree la tabla, pero cada réplica del Host tendría su propia caché, y un reinicio la pierde. La revocación inmediata (necesaria por la decisión de Franklin) exige que un cambio sea visible para todas las réplicas y para el siguiente arranque sin demora. Una tabla es la única fuente que cumple eso. |

## Persistencia y compatibilidad

Una sola migración nueva para el módulo Permissions, siguiendo el patrón
existente (`InitialSchema`, snake_case, ADR 004):

```sql
CREATE TABLE per_permission_requests (
    id                   uuid          PRIMARY KEY,
    requester_id         uuid          NOT NULL REFERENCES ac_users(id) ON DELETE CASCADE,
    requested_permission varchar(64)   NOT NULL,
    state                varchar(16)   NOT NULL,
    approver_id          uuid              NULL REFERENCES ac_users(id) ON DELETE SET NULL,
    rejection_reason     varchar(500)      NULL,
    created_at_utc       timestamptz   NOT NULL,
    approved_at_utc      timestamptz       NULL,
    applied_at_utc       timestamptz       NULL,
    rejected_at_utc      timestamptz       NULL
);

CREATE INDEX ix_per_permission_requests_requester ON per_permission_requests(requester_id);
CREATE INDEX ix_per_permission_requests_state    ON per_permission_requests(state);

CREATE TABLE per_permission_grants (
    id                    uuid          PRIMARY KEY,
    user_id               uuid          NOT NULL REFERENCES ac_users(id) ON DELETE CASCADE,
    permission_name       varchar(64)   NOT NULL,
    granted_at_utc        timestamptz   NOT NULL,
    granted_by_user_id    uuid              NULL REFERENCES ac_users(id) ON DELETE SET NULL
);

-- Un usuario no puede tener dos concesiones activas del mismo permiso.
-- Esto refleja la regla de RF-GP-05: aprobar dos veces la misma solicitud
-- deja una sola fila porque la segunda aprobación falla con InvalidPermissionRequestTransition
-- ANTES de llegar al INSERT. El índice único es una segunda barrera para
-- protegerse de carrera y de bypass futuro del caso de uso.
CREATE UNIQUE INDEX ux_per_permission_grants_user_permission ON per_permission_grants(user_id, permission_name);
CREATE INDEX        ix_per_permission_grants_permission      ON per_permission_grants(permission_name);
```

- Sin renombre de columnas o tablas existentes. Las tablas `ac_*` y `not_*`
  no se tocan.
- Sin migración de datos: el sistema parte de cero en ambas tablas. La
  verificación de conteos antes/después es trivial (cero en ambos lados).
- La FK a `ac_users` se declara sin propiedad de navegación: `Permissions.Infrastructure`
  no necesita ver `AccessControl.Domain.Entities.User`. Se accede a la fila
  referenciada solo por el `Guid`.

## Verificación

- `dotnet build pirry-ledger.slnx` con 0 errores.
- `dotnet test pirry-ledger.slnx` con la suite verde.
- Inspección del esquema: `\d per_permission_grants` y
  `\d per_permission_requests` muestran las columnas y los índices declarados.
- Una prueba del módulo Permissions verifica que, tras
  `ApprovePermissionRequest`, existe exactamente una fila en
  `per_permission_grants` con los campos correctos (`user_id`,
  `permission_name`, `granted_at_utc`, `granted_by_user_id`).
- Una prueba verifica que `IPermissionAuthority.UserHasPermissionAsync(requester,
  "ViewSalesSummary")` devuelve `true` tras la aprobación y `false` antes.
- Una prueba verifica que un INSERT duplicado `(user_id, permission_name)` falla
  con la violación del índice único (segunda barrera contra bypass).
- Una prueba verifica que `RejectPermissionRequest` **no** inserta fila en
  `per_permission_grants`.

## Consecuencias

**A favor**

- Responder "quién tiene qué ahora" es un EXISTS directo sobre la tabla
  operativa, sin filtrar por estado.
- La revocación futura (cuando llegue) es una `DELETE` simple, sin tocar la
  máquina de estados ni la tabla de solicitudes.
- La historia completa queda en `per_permission_requests`; la vista operativa
  en `per_permission_grants`. Separación clara.
- Trazabilidad: `granted_by_user_id` y `granted_at_utc` responden a "quién
  le dio el permiso a este usuario y cuándo" sin join con la solicitud.

**En contra, asumido a conciencia**

- Dos tablas con información parcialmente solapada (la solicitud dice "se
  aplicó", la grants dice "está concedido"). Sin revocación implementada
  hoy, las dos siempre coinciden; cuando llegue la revocación, la grants
  será la verdad operativa y la solicitud seguirá contando la historia.
- Si una solicitud se "revierte" en el futuro, hay que coordinar las dos
  tablas. Se decide en su momento, no se adelanta aquí.
- Más una migración por módulo nuevo. Aceptado: las migraciones son
  explícitas en este repositorio y se documentan en el README.

## Qué queda fuera

- La revocación como operación de negocio. No hay endpoint ni caso de uso
  para borrar una fila de `per_permission_grants`. Se decide cuando se
  construya la pieza correspondiente.
- La auditoría de quién consultó o modificó `per_permission_grants`
  (semana 14).
- La notificación al solicitante de que su permiso fue concedido (semana 11).
- Reutilizar `ac_users.granted_permissions` como columna jsonb: descartado
  por RD-01.
- Índices adicionales sobre `granted_at_utc` para limpieza por antigüedad
  (se decide cuando haya política de retención).