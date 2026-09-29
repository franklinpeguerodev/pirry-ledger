# Entities in use

Source: Requerimientos del Core, sección 2. Only the entities that the current iteration uses are listed. Minimum attributes: you may add the attributes your design needs, but you may not omit any of these.

| Entidad | Atributos mínimos |
|---|---|
| Usuario | Nombre · correo (único) · contraseña con hash · rol · activo |
| Rol | Administrador o Estándar |
| CodigoRecuperacion | Usuario · código · fecha de emisión · fecha de vencimiento · usado o no usado |
| CorreoEnCola | Destinatario · asunto · cuerpo · estado · intentos · fecha de creación · fecha de envío · último error |

## Not defined by the course (design decisions)

The course does not define these, so they are Franklin's design decisions and should be recorded as decisions before implementing:

- The activation token for RF-CA-15 and RF-CA-16 (single use, with expiry).
- The failed-attempt counter and lock expiry for RF-CA-19.
- How a session credential is represented and invalidated (RF-CA-12, 18, 20).
- The business entity that carries the state machine (see `business-module.md`).