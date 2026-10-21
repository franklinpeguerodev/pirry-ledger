# Entities in use

Source: Requerimientos del Core, sección 2. Only the entities that delivered or current iterations use are listed. Minimum attributes: you may add the attributes your design needs, but you may not omit any of these.

| Entidad | Atributos mínimos |
|---|---|
| Usuario | Nombre · correo (único) · contraseña con hash · rol · activo |
| Rol | Administrador o Estándar |
| CodigoRecuperacion | Usuario · código · fecha de emisión · fecha de vencimiento · usado o no usado |
| CorreoEnCola | Destinatario · asunto · cuerpo · estado · intentos · fecha de creación · fecha de envío · último error |
| SolicitudPermiso | Solicitante · permiso solicitado · estado · aprobador · fecha de creación · fecha de resolución · motivo del rechazo |

## Not defined by the course (design decisions)

The course does not define these, so they are Franklin's design decisions. When one is important, it is recorded in an ADR (with Franklin's authorization) before implementing:

- The activation token for RF-CA-15 and RF-CA-16 (single use, with expiry). Delivered.
- The failed-attempt counter and lock expiry for RF-CA-19. Delivered.
- How a session credential is represented and invalidated (RF-CA-12, 18, 20). Delivered, `docs/adr/001-session-credential.md`.
- The business entity that carries the state machine: `Invoice`, see `business-module.md`.
- What a "permiso" is and which action it unlocks (RF-GP-05), for the permissions piece. Current iteration: see `docs/current-iteration.md`.

## Naming

Requirement text uses the course's Spanish names (`SolicitudPermiso`, `Pendiente`, `Aprobada`, `Aplicada`, `Rechazada`). The code is in English, so each iteration proposes English names for new entities and states and records the mapping in the README, as was done for `Invoice` and its states.