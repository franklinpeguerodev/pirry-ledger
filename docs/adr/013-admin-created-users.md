# ADR 013 - Administrator-created users

- **Estado:** aceptada
- **Fecha:** 2026-11-04
- **Requisitos:** RF-CA-01, RF-CA-15, RF-CA-19, RF-CA-21; RD-06, RD-07, RD-08; toca ADR 002
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

Hasta hoy el alta es pública: `POST /api/auth/register` no exige sesión. Cualquiera
que conozca la URL crea una cuenta, la activa con el enlace que llega al correo que
indicó y entra al sistema como `Standard`.

Ese `Standard` recién creado no es inofensivo. La tabla de `RoleRequirements`
declara `ListInvoices` y `GetInvoice` como `AccessLevel.AnySession`, así que una
cuenta recién registrada puede leer todo el historial de facturación: montos,
nombres de deudores y pagos. Para un negocio de dos sucursales y dos o tres
empleados por sucursal, el alta abierta es una puerta de entrada a los datos del
negocio, no una comodidad.

El único camino documentado para tener un segundo `Administrator` es RF-CA-08:
un Administrador cambia el rol de un usuario existente (`ChangeUserRole`, con su
prueba `AdministradorCambiaElRolDeOtro`). El seed de ADR 002 crea únicamente al
primer Administrador y se niega a crear otro.

## Decisión

### 1. El alta pasa a la administración

`POST /api/admin/users`, con `AccessLevel.Administrator` en `RoleRequirements`, y
`POST /api/auth/register` deja de existir. El caso de uso `RegisterUser` se
convierte en `CreateUserByAdministrator`: además de validar y guardar, reautentica
con `Authenticate.ExecuteWithRoleAsync` y exige el rol `Administrator`.

La comprobación se hace dos veces a propósito, igual que en `ChangeUserRole`: la
hace el filtro antes de llegar al endpoint y la repite el caso de uso. La segunda
no es redundancia decorativa: un caso de uso se puede llamar desde otro sitio que
no pase por el filtro, y RD-06 exige que la autorización viva en el servidor.

### 2. El cuerpo no lleva rol

```json
{ "name": "María Pérez", "email": "maria@pirry.local", "password": "Temporal123" }
```

El modelo `CreateUserRequest` **no tiene propiedad `Role`**. `System.Text.Json`
ignora los miembros desconocidos, así que un cliente que envíe
`"role": "Administrator"` obtiene un `Standard` igual. La prueba
`UnRolEnviadoEnElCuerpoSeIgnoraYElUsuarioNaceEstandar` lo fija.

**Por qué:** RF-CA-08 ya es la puerta documentada al rol `Administrator`. Añadir
`role` al alta crearía una segunda puerta que ningún requisito pide, y con ella un
segundo sitio que auditar. El alta tiene un solo trabajo: crear un `Standard`
inactivo con correo único (RF-CA-01).

Consecuencia operativa: un segundo Administrador se consigue en dos pasos —crear
el `Standard`, activarlo y promoverlo con `POST /api/admin/users/{id}/role`—.
Ambos pasos están documentados y ambos son de Administrador.

### 3. La cuenta nace inactiva y se activa por correo

RF-CA-15 no distingue quién crea la cuenta: "el usuario nace inactivo y recibe por
correo un enlace de activación con token de un solo uso y fecha de vencimiento".
El alta por Administrador respeta esa regla sin excepción. El token dura 24 horas
y el correo sale por la cola (RF-NOT-08).

Esto tiene una propiedad útil: crear una cuenta **no** crea un usuario usable.
Hasta que alguien abra el enlace del correo, la cuenta no puede iniciar sesión.

### 4. El Administrador fija la contraseña y no se obliga a cambiarla

El Administrador escribe la contraseña inicial y la conoce. No hay bandera
`MustChangePassword`, ni sesión restringida, ni un `403` de "cambie su contraseña
antes de continuar". Franklin lo decidió así: la contraseña se queda como está si
el usuario no la cambia.

El riesgo se declara en voz alta: el Administrador conoce la contraseña de cada
`Standard` y puede suplantarlo. Se acepta porque el Administrador ya tiene control
total sobre el negocio y sobre las cuentas, y porque la pieza de auditoría
(semana 14) registrará las operaciones. La aprobación de cambios de contraseña
(ADR 014) es un control de flujo, no una frontera de seguridad: la frontera real
es el correo del usuario.

### 5. Quién creó a quién no se guarda aquí

No hay columna `created_by_user_id`. Saber qué Administrador creó qué cuenta es un
registro de auditoría, y la auditoría es una pieza posterior. Añadir la columna
ahora obligaría a una migración para un dato que nadie lee todavía.

### 6. El desbloqueo es una operación de Administrador e idempotente

`POST /api/admin/users/{id}/unlock` pone `FailedAttempts` en cero y
`LockedUntilUtc` en nulo. No toca el hash ni `CredentialVersion`: desbloquear no
cambia la contraseña ni invalida sesiones, porque una cuenta bloqueada no tiene
sesiones válidas que invalidar.

Desbloquear dos veces devuelve `204` las dos veces. No es un error: el
Administrador pidió un estado, no una transición, y el estado ya era el pedido.
Es distinto de desactivar o reactivar, donde el segundo intento sí se rechaza
porque ahí sí hay una transición que no se puede repetir.

### 7. Respuesta

`201 Created` con `{ "id": "<guid>" }`. Es el recurso recién creado y lo único que
el cliente necesita para el siguiente paso (listarlo, promoverlo o desbloquearlo).

### 8. Sin migración

No hay columnas ni tablas nuevas: el alta usa `ac_users` tal como está y el
desbloqueo usa dos columnas que ya existen (`failed_attempts`, `locked_until_utc`).

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Mantener el alta pública y aprobar cuentas después | La cuenta existiría y podría activarse antes de la aprobación; el problema es que exista, no que se use. Además exige un estado nuevo en `User`. |
| Reutilizar `POST /api/auth/register` con un token de Administrador | Una ruta pública que a veces exige sesión es una ruta que se puede invocar sin ella por error; el nivel de acceso dejaría de leerse de un vistazo en `RoleRequirements`. |
| Añadir `role` al cuerpo del alta | Segunda puerta a `Administrator` sin requisito que la respalde; RF-CA-08 ya cubre el cambio de rol y está probado. |
| Que la activación fije la contraseña | Cambia RF-CA-16 y obliga a un formulario público nuevo; además el Administrador perdería la contraseña inicial que quiere conocer. |
| Generar la contraseña y enviarla por correo | La contraseña viajaría en un correo, que es justo lo que la cola ya hace con tokens de un solo uso y no con credenciales permanentes. |
| Guardar `created_by_user_id` ahora | Migración para un dato que solo lee la auditoría, que es una pieza posterior. |
| Crear la cuenta activa | Rompe RF-CA-15 y elimina la única prueba de que el correo existe. |
| Desbloquear rechazando la segunda llamada | El Administrador tendría que consultar el estado antes de actuar; `204` idempotente es más simple y no oculta nada. |

## Consecuencias

**A favor**

- Desaparece la puerta por la que cualquiera podía registrarse y leer la
  facturación del negocio.
- El alta queda en un solo sitio, con una sola validación y un solo mensaje de
  error para el correo duplicado.
- El rol sigue teniendo una sola puerta (RF-CA-08), lo que mantiene un único
  lugar que auditar cuando llegue la pieza de auditoría.
- El desbloqueo es idempotente y no invalida sesiones que no existen.
- Sin migración.

**En contra, asumido a conciencia**

- Es un cambio de contrato: cualquier cliente que llame a `/api/auth/register`
  recibe `404`. El README lo documenta.
- El Administrador conoce la contraseña inicial de cada `Standard` y puede
  suplantarlo. Mitigación futura: auditoría. Mitigación actual: la contraseña se
  puede cambiar, y ese cambio exige aprobación (ADR 014).
- Crear un segundo Administrador son dos pasos en vez de uno.
- El sistema sigue dependiendo del seed (ADR 002) para el primer Administrador.
  Si el seed no se configura, no hay ninguno y el alta no se puede usar.

## Qué queda fuera

- La auditoría de quién creó, desbloqueó o promovió a quién (semana 14).
- Un aviso al Administrador cuando se crea una cuenta.
- La baja o el borrado de usuarios: hoy solo existe desactivar (RF-CA-20).
- La edición del nombre o del correo de una cuenta existente.
