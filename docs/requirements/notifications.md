# Notifications and outgoing mail queue

## Practice 5 extension (planned)

Source: Práctica 5, sections 1.2 and 1.3. The original minimal queue below
remains a delivered baseline. The following text is the newer assignment;
business deviations are recorded separately in ADR 023 and ADR 024.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-NOT-01 | Crear una solicitud de permiso notifica a los Administradores. | Todos los Administradores reciben la notificación; nadie más la recibe. |
| RF-NOT-02 | Resolver una solicitud de permiso notifica al solicitante. | Indica si fue aprobada o rechazada y, en el segundo caso, el motivo. |
| RF-NOT-03 | Al menos un evento propio de tu módulo de negocio dispara una notificación. | El evento está descrito en tu README y se puede provocar siguiendo sus pasos. |
| RF-NOT-04 | Consultar las notificaciones propias, distinguiendo leídas de no leídas. | Un usuario no ve las notificaciones de otro. |
| RF-NOT-05 | Marcar una notificación como leída. | Intentar marcar la notificación de otro usuario se rechaza. |
| RF-NOT-06 | Toda notificación se entrega por dos canales: dentro del sistema y por correo. | El destinatario la ve en su bandeja interna y recibe además el correo correspondiente. |
| RF-NOT-07 | El destinatario puede desactivar el canal de correo sin perder el interno. | Con el correo desactivado la notificación aparece dentro del sistema y no se encola ningún correo. |
| RF-NOT-08 | Los correos se encolan; no se envían dentro de la operación que los origina. | La operación termina correctamente aunque el servidor de correo no responda. |
| RF-NOT-09 | Un proceso independiente toma los correos pendientes y los envía. | Se ejecuta sin que el flujo que creó el correo vuelva a correr. |
| RF-NOT-10 | Un correo que falla se reintenta un número limitado de veces y luego queda fallido. | No se reintenta indefinidamente ni desaparece sin dejar rastro. El máximo de intentos está declarado en tu README. |
| RF-NOT-11 | Cada correo registra estado (pendiente, enviado o fallido), intentos, fechas y último error. | Un Administrador consulta la cola y sabe qué pasó con cualquier correo; un Estándar recibe rechazo. |
| RF-NOT-12 | Un correo enviado no se vuelve a enviar. | Ejecutar el procesador dos veces seguidas no duplica ningún envío. |
| RF-NOT-13 | Las credenciales del servidor de correo se leen de variables de entorno. | No aparecen en el repositorio ni en el historial (RD-10). |

## Historical Practice 1 scope

Source of this historical subsection: Requerimientos del Core, pieza 4,
and Práctica 1, sección 1.5. These four requirements describe the delivered
minimal queue; the remaining Practice 5 requirements are listed above.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-NOT-08 | Los correos no se envían dentro de la operación que los origina: se encolan. | La operación de negocio termina correctamente aunque el servidor de correo no responda. |
| RF-NOT-09 | Un proceso independiente toma los correos pendientes y los envía. | El envío ocurre fuera del flujo que creó el correo, y se puede ejecutar sin que ese flujo vuelva a correr. |
| RF-NOT-12 | Un correo enviado no se vuelve a enviar. | Ejecutar el procesador de la cola dos veces seguidas no duplica ningún envío. |
| RF-NOT-13 | Las credenciales del servidor de correo se leen de variables de entorno. | Cumple RD-10: no aparecen en el repositorio ni en el historial de commits. |

## What Practice 1 requires of the queue

Activation, password recovery and forced reset send emails. Since this practice, email is not sent inside the operation that originates it: it is registered in the `CorreoEnCola` entity and a separate process sends it (RF-NOT-08 and RF-NOT-09, minimal version).

- The business operation finishes successfully even if the mail server does not respond; the email stays in the queue as pending.
- A separate process or command takes the pending emails, sends them over SMTP and marks them as sent. Running it twice does not duplicate sends (RF-NOT-12).
- SMTP credentials are read from environment variables (RF-NOT-13, RD-10). Any server works: a personal email account with an app password, or a test service.
- The email is really received: at grading time the professor registers with their own email and opens the link.

Retries, failed state, last error and admin consultation are now planned in
Practice 5. This historical baseline does not describe them as implemented.
