# Notifications: minimal mail queue

Source: Requerimientos del Core, pieza 4 (cola de correos), and Práctica 1, sección 1.5. Only the four requirements that Practice 1 uses are included. The rest of the piece (notifications themselves, retries, failed state, admin view of the queue) arrives in weeks 11 and 12 and is not stored here yet.

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

Retries, failed state, last error and admin consultation of the queue arrive in week 11.