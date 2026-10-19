# Permissions management (RF-GP)

Source: Requerimientos del Core, pieza 2, and Asignación 2, sección 1.1. The text is the professor's original Spanish, kept verbatim so IDs and citations match. A criterion that does not pass means the requirement is not done, even if the code runs.

## Transiciones

| Desde | Hacia | Quién la ejecuta | Condición |
|---|---|---|---|
| Pendiente | Aprobada | Administrador | La solicitud está en Pendiente. |
| Aprobada | Aplicada | El sistema | Automática, inmediatamente después de aprobar. |
| Pendiente | Rechazada | Administrador | Exige un motivo escrito. |
| Cualquier otra combinación | — | — | Prohibida. El sistema la rechaza y el estado no cambia. |

## Requisitos

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-GP-01 | Un usuario Estándar crea una solicitud de permiso. | La solicitud nace en estado Pendiente. |
| RF-GP-02 | Un Administrador consulta las solicitudes pendientes. | Un usuario Estándar no puede ver las solicitudes de otros usuarios. |
| RF-GP-03 | Un Administrador aprueba una solicitud pendiente. | Queda registrado quién aprobó y cuándo. |
| RF-GP-04 | Un Administrador rechaza una solicitud pendiente indicando el motivo. | Sin motivo escrito, el rechazo no se completa. |
| RF-GP-05 | Al aprobar, el sistema aplica el cambio de permiso automáticamente. | Tras la aprobación el solicitante puede ejecutar la acción que pidió, sin ninguna intervención manual adicional. |
| RF-GP-06 | Solo se permiten las transiciones de la tabla anterior. | Aprobar dos veces la misma solicitud, o pasar de Rechazada a Aplicada, se rechaza y el estado no cambia. |
| RF-GP-07 | Nadie resuelve su propia solicitud. | Un Administrador que intenta aprobar o rechazar una solicitud creada por él mismo recibe un rechazo. |

## Notes

- Design rules that apply (see `design-rules.md`): RD-04 (transitions resolved in one place), RD-06 (authorization on the server), RD-07 (invalid input gives a controlled rejection) and RD-09 (requests survive an application restart). RD-12 also applies: the piece is tested without starting the application.
- Audit records for the resolution of a request (RF-AUD-02) are required in week 14 and are not graded here. Notifications to the Administrators and to the requester (RF-NOT-01, RF-NOT-02) belong to weeks 11 and 12. Do not implement either now, but do not design in a way that makes adding them hard later.
- This state machine is independent from the business module's one (RF-NEG-09): changing the states of one must never force changes in the other, and they must not share code or enums.
- The professor tests the piece by creating, approving, rejecting with a reason and approving twice a request, following the README to the letter.