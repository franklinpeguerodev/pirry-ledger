# Access control (RF-CA)

Source: Práctica 1, sección 1 (the newer text, which adds RF-CA-14 to RF-CA-22 and refines the Core document). Grouped as in the assignment. A criterion that does not pass means the requirement is not done, even if the code runs.

## 1.1 Registro y activación de la cuenta

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-CA-01 | Registro de usuario con correo único. | Un segundo registro con un correo ya existente se rechaza. |
| RF-CA-02 | La contraseña se guarda con hash y sal, nunca en texto plano. | El valor almacenado no coincide con la contraseña y no puede revertirse a ella; dos usuarios con la misma contraseña no comparten el valor almacenado (RD-05). |
| RF-CA-14 | Política mínima de contraseña: al menos 8 caracteres, con letras y números. | Una contraseña que no la cumple se rechaza con mensaje controlado, en el registro y en todo cambio de contraseña (RD-07). |
| RF-CA-15 | El usuario nace inactivo y recibe por correo un enlace de activación con token de un solo uso y fecha de vencimiento. | Antes de activar, el inicio de sesión se rechaza con un mensaje que indica que la cuenta no está activa. El correo sale por la cola (RF-NOT-08). |
| RF-CA-16 | Abrir el enlace activa la cuenta. | Tras activar, el inicio de sesión funciona. Usar el enlace dos veces, o después de vencido, se rechaza y el estado no cambia. |
| RF-CA-17 | El usuario puede pedir que se le reenvíe el enlace de activación indicando su correo. | La respuesta es idéntica exista o no ese correo. El reenvío invalida el enlace anterior. |

## 1.2 Sesión

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-CA-03 | Inicio de sesión que entrega una credencial de sesión. | Credenciales correctas abren sesión; incorrectas se rechazan sin revelar cuál de los dos datos falló. |
| RF-CA-07 | Consulta del usuario autenticado y su rol. | Sin sesión válida la consulta se rechaza. |
| RF-CA-18 | Cierre de sesión. | La credencial cerrada deja de servir: usarla después se rechaza. |
| RF-CA-19 | Tras 5 intentos fallidos consecutivos, la cuenta queda bloqueada 15 minutos. | El sexto intento, aun con la contraseña correcta, se rechaza durante el bloqueo. Un inicio de sesión correcto pone el contador en cero. |

## 1.3 Roles y administración de usuarios

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-CA-04 | Dos roles: Administrador y Estándar. | Todo usuario tiene exactamente un rol asignado. |
| RF-CA-05 | Cada operación del sistema declara qué rol puede ejecutarla. | Existe un punto del código donde se puede leer la exigencia de rol de cada operación. |
| RF-CA-06 | Un usuario Estándar que invoca una operación de Administrador recibe un rechazo explícito. | El rechazo ocurre también cuando la petición se construye a mano, sin pasar por la interfaz (RD-06). |
| RF-CA-08 | Cambio de rol de un usuario, reservado al Administrador. | Un Estándar no puede cambiar ningún rol, ni el propio. |
| RF-CA-20 | Un Administrador desactiva y reactiva usuarios. | Un usuario desactivado no inicia sesión y sus sesiones abiertas dejan de ser válidas. Un Administrador no puede desactivarse a sí mismo. |
| RF-CA-21 | Un Administrador lista los usuarios con su rol y su estado. | Un Estándar recibe rechazo. El listado nunca incluye hashes ni tokens. |

## 1.4 Contraseñas: recuperación, cambio y restablecimiento

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-CA-09 | Un usuario inicia la recuperación de su contraseña indicando su correo registrado. | La respuesta es idéntica exista o no ese correo: el flujo no revela qué correos están registrados. |
| RF-CA-10 | La recuperación genera un código de un solo uso con fecha de vencimiento y lo envía al correo del usuario a través de la cola de correos. | Usarlo dos veces, o después de vencido, se rechaza y la contraseña no cambia. El correo sale por la cola (RF-NOT-08). |
| RF-CA-11 | Con un código válido el usuario define una contraseña nueva, que se guarda con hash. | La contraseña anterior deja de servir para iniciar sesión. |
| RF-CA-12 | Las sesiones abiertas antes del cambio de contraseña dejan de ser válidas. | Una credencial de sesión emitida antes del restablecimiento es rechazada. |
| RF-CA-13 | Un Administrador puede forzar el restablecimiento de la contraseña de un usuario. | El usuario no puede seguir usando su contraseña anterior y recibe por la cola el correo con el código para definir una nueva. |
| RF-CA-22 | Un usuario con sesión cambia su propia contraseña indicando la actual. | Con la contraseña actual incorrecta el cambio se rechaza. Al cambiarla aplican RF-CA-14 y RF-CA-12. |

## Notes

- Audit records for RF-CA-08, RF-CA-13 and RF-CA-20 are required in week 14 (piece 6) and are not graded in Practice 1. Do not implement them now, but do not design in a way that makes adding them hard later.
- The Core document says password recovery depends on the mail queue, a later piece, and that RF-CA-13 does not depend on email. Practice 1 overrides both: a minimal queue is built now (see `notifications.md`), and its criterion for RF-CA-13 requires that the user receives the email with the code through the queue.