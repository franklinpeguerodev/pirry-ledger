# Práctica 1 — Control de acceso completo

Programación III · TDS-007 · ITLA · 2026-C-3
Bloque 2 | Semana 4 | 8 puntos | Individual

Primer hito del repositorio. Se entrega la primera pieza del Core funcionando completa: Control de acceso, desde el registro con activación por correo hasta la recuperación de contraseña y la administración de usuarios, construida sobre el diseño de componentes de la semana 2 y con la mecánica de ramas y pull requests de la semana 3. Además se deja declarada la estructura de la máquina de estados del módulo de negocio, todavía sin pruebas.

Es la misma especificación para los 25 proyectos. Los identificadores son los de los Requerimientos del Core, pieza 1. Úsalos en los commits y en la descripción de los pull requests: un criterio de aceptación que no se cumple es un requisito que no está hecho, aunque el código corra.

## 1. Alcance

The requirement tables live in other files:

- Registro, sesión, roles y contraseñas (RF-CA-01 a RF-CA-22): `access-control.md`.
- Correo saliente, cola mínima (RF-NOT-08, 09, 12, 13): `notifications.md`.
- Estructura de la máquina de estados de negocio (RF-NEG-03, 04, 05): `business-module.md`.

Además aplican los requisitos de diseño que tocan esta pieza (`design-rules.md`): RD-05, RD-06, RD-07 (un correo vacío o mal formado produce un rechazo controlado, no una excepción sin manejar), RD-08 (ningún mensaje al usuario expone trazas ni consultas), RD-09 (los usuarios registrados sobreviven a reiniciar la aplicación), RD-10, RD-11 (fechas y horas en UTC a través de un único reloj inyectable, sin llamar a `DateTime.UtcNow` directamente) y RD-12 (cada pieza del Core es verificable sin levantar la aplicación completa: su proyecto de pruebas la monta sola).

Los registros de auditoría de RF-CA-08, RF-CA-13 y RF-CA-20 se exigen en la semana 14, con la pieza 6; aquí no se califican.

### 1.7 Cómo se construyó: historial y pull requests

- Cada funcionalidad en su rama, fusionada a `main` por un pull request en el propio repositorio con las cuatro secciones: Qué cambia, Por qué, Cómo probarlo, Qué NO incluye. Como mínimo cuatro pull requests: registro y activación, sesión, recuperación de contraseña, administración de usuarios.
- Commits atómicos con asunto en imperativo y el identificador del requisito que cubren.
- README con las instrucciones exactas para ejecutar el proyecto, las variables de entorno que necesita (nombre y para qué, nunca el valor) y cómo provocar cada criterio de aceptación. Se sigue al pie de la letra al calificar; lo que el README no dice, no se busca.

## 2. Cómo se entrega

```powershell
git tag practica-1
git push origin --tags
```

En Moodle, en la tarea «Práctica 1»: la URL del repositorio y el nombre de la etiqueta. Se evalúa el punto etiquetado; se puede seguir trabajando en `main` después.

## 3. Rúbrica (8.0 puntos)

Cada criterio se califica como No logrado (0 %), Parcialmente logrado (50 %) o Logrado (100 %).

### Registro y activación — RF-CA-01, 02, 14, 15, 16, 17 (1.5 pts)

- **No logrado:** no se puede registrar un usuario, o la contraseña se guarda en texto plano, o la cuenta funciona sin activar.
- **Parcialmente logrado:** registro con hash y activación por enlace funcionan, pero falla uno de estos: correo duplicado aceptado, hash sin sal, enlace reutilizable o sin vencimiento, política de contraseña ausente, reenvío que revela si el correo existe.
- **Logrado:** registro con correo único, hash con sal, política de contraseña, cuenta inactiva hasta abrir el enlace, enlace de un solo uso con vencimiento, y reenvío con respuesta idéntica exista o no el correo.

### Sesión — RF-CA-03, 07, 18, 19 (1.5 pts)

- **No logrado:** no hay inicio de sesión, o acepta credenciales incorrectas, o no entrega credencial de sesión.
- **Parcialmente logrado:** inicio de sesión funciona, pero falla uno de estos: el rechazo revela qué dato falló, la consulta del autenticado responde sin sesión, no hay cierre de sesión efectivo, no hay bloqueo por intentos.
- **Logrado:** credenciales incorrectas rechazadas con el mismo mensaje, consulta del autenticado protegida, cierre de sesión que invalida la credencial, y bloqueo temporal tras cinco fallos consecutivos.

### Roles y administración — RF-CA-04, 05, 06, 08, 20, 21, RD-06 (1.5 pts)

- **No logrado:** no hay roles, o cualquier usuario puede ejecutar cualquier operación.
- **Parcialmente logrado:** roles y rechazo funcionan desde la interfaz, pero una petición construida a mano por un Estándar se ejecuta, o la exigencia de rol está repetida en varios lugares, o falta el cambio de rol, la desactivación o el listado.
- **Logrado:** exigencia de rol legible en un punto, rechazo del lado del servidor aunque la petición se construya a mano, cambio de rol y desactivación reservados al Administrador con sus protecciones, y listado sin datos sensibles.

### Contraseñas — RF-CA-09 a 13, 22 (1.5 pts)

- **No logrado:** no hay recuperación de contraseña, o el código no vence ni es de un solo uso, o la contraseña anterior sigue sirviendo.
- **Parcialmente logrado:** la recuperación funciona, pero falla uno de estos: el flujo revela qué correos existen, las sesiones anteriores siguen válidas, falta el restablecimiento forzado por Administrador, o el cambio con sesión no exige la contraseña actual.
- **Logrado:** recuperación con código de un solo uso y vencimiento, respuesta idéntica exista o no el correo, sesiones anteriores invalidadas, restablecimiento forzado por Administrador, y cambio de contraseña con sesión que exige la actual.

### Correo por cola — RF-NOT-08, 09, 12, 13 (0.5 pt)

- **No logrado:** el correo se envía dentro de la operación, o hay credenciales SMTP en el repositorio, o el correo no llega.
- **Parcialmente logrado:** se encola y se envía, pero ejecutar el enviador dos veces duplica envíos, o la operación falla cuando el servidor SMTP no responde.
- **Logrado:** la operación termina bien sin servidor SMTP, el enviador independiente entrega el correo real, no duplica envíos, y las credenciales vienen de variables de entorno.

### Máquina de estados de negocio — RF-NEG-03, 04, 05, RD-04 (0.5 pt)

- **No logrado:** no existe la entidad central con estado, o los estados no están declarados en ningún lugar del código.
- **Parcialmente logrado:** entidad y estados declarados, pero las transiciones no están en un solo lugar, o falta la transición prohibida o el estado terminal, o falta la tabla en `docs/`.
- **Logrado:** entidad con estado, entre 3 y 5 estados en un solo lugar, transiciones en un solo lugar con al menos una prohibida y un estado terminal, y la tabla documentada.

### Historial, pull requests y README (1.0 pt)

- **No logrado:** trabajo directo en `main` sin ramas ni pull requests, o credenciales o archivos generados en el historial, o el README no permite ejecutar el proyecto.
- **Parcialmente logrado:** hay ramas y pull requests, pero alguna descripción no trae las cuatro secciones, o hay commits que mezclan cambios sin relación, o el README omite variables de entorno o cómo provocar algún criterio.
- **Logrado:** al menos cuatro pull requests con las cuatro secciones, commits atómicos que citan el requisito, nada indebido en el historial, y un README que permite ejecutar y verificar cada criterio sin preguntar.

### Revisión acumulada

A partir de la Práctica 2, dos de los ocho puntos de cada práctica verifican que lo entregado aquí siga funcionando: registro con activación, inicio de sesión, rechazo por rol y recuperación de contraseña. Romper Control de acceso en la semana 5 cuesta puntos en las semanas 7, 8 y 11.

## 4. Qué se revisa exactamente

- `git clone`, `git checkout practica-1`, variables de entorno según el README, y ejecución.
- Registrarse con un correo propio; intentar iniciar sesión antes de activar; abrir el enlace recibido; abrirlo por segunda vez; intentar registrar el mismo correo otra vez.
- Registrar con una contraseña de 5 caracteres y con un correo mal formado: rechazo controlado.
- Leer el almacenamiento: la contraseña no aparece; dos usuarios con la misma contraseña no comparten el valor almacenado.
- Iniciar sesión con contraseña incorrecta y con correo inexistente: los dos rechazos son idénticos. Fallar cinco veces seguidas y luego usar la contraseña correcta.
- Cerrar sesión y volver a usar la credencial cerrada.
- Con sesión de Estándar, invocar una operación de Administrador construyendo la petición a mano; intentar cambiar el propio rol.
- Como Administrador: listar usuarios, cambiar un rol, desactivar un usuario con sesión abierta y probar esa sesión, intentar desactivarse a sí mismo.
- Pedir recuperación con un correo inexistente y con uno existente: misma respuesta. Usar el código, volver a usarlo, iniciar sesión con la contraseña vieja y con la nueva, probar una credencial emitida antes del cambio.
- Forzar el restablecimiento de un usuario como Administrador. Cambiar la contraseña con sesión indicando una contraseña actual incorrecta.
- Apagar el acceso al servidor SMTP y registrar un usuario: la operación termina bien y el correo queda pendiente. Ejecutar el enviador dos veces.
- Reiniciar la aplicación: los usuarios siguen ahí.
- Localizar el punto único de estados y transiciones del negocio, y leer `docs/maquina-de-estados.md`.
- `git log --oneline --graph --all`, `git ls-files` y `git log -p` en busca de credenciales; los pull requests en GitHub.