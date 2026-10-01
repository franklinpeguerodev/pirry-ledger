# Pirry Ledger

ERP para un negocio de comida rápida. Práctica 1 — Control de acceso.

En este punto el repositorio tiene la cola de correo con su persistencia y un
comando para enviar lo pendiente, y la API expone el registro con activación más el
inicio de sesión con su tabla de sesiones. **La recuperación de contraseña y la
administración de usuarios llegan en las fases siguientes.**

## Requisitos

- .NET SDK 10.0.302. La versión exacta la fija `global.json`, no hace falta
  instalar nada más.
- PostgreSQL 18, con una base `pirry_ledger` y un rol que sea dueño de ella.
- Git.
- La herramienta de Entity Framework, solo para aplicar las migraciones. El
  repositorio no trae manifiesto local, así que se instala global:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.12
dotnet ef --version
```

`dotnet ef --version` tiene que responder `10.0.12`.

## Variables de entorno

La aplicación lee todo de variables de entorno. **Ninguna va en el repositorio**
(RD-10). En esta tabla está el nombre y para qué sirve cada una, nunca el valor.

| Variable | Para qué sirve |
|---|---|
| `ConnectionStrings__PirryLedger` | **Obligatoria.** Cadena de conexión a PostgreSQL: host, puerto, base de datos, usuario y contraseña. |
| `PIRRY_LEDGER_SMTP_HOST` | Servidor SMTP saliente, por ejemplo `smtp.gmail.com`. |
| `PIRRY_LEDGER_SMTP_PORT` | Puerto SMTP. Con `StartTls` suele ser `587`. |
| `PIRRY_LEDGER_SMTP_SECURITY` | Cómo se cifra el transporte: `StartTls`, `Ssl` o `Ninguno`. |
| `PIRRY_LEDGER_SMTP_USER` | Cuenta con la que se autentica el envío. |
| `PIRRY_LEDGER_SMTP_PASSWORD` | Contraseña de aplicación de esa cuenta. Nunca la contraseña de la cuenta. |
| `PIRRY_LEDGER_SMTP_FROM` | Dirección de correo que aparece como remitente. |
| `PIRRY_LEDGER_PUBLIC_BASE_URL` | **Obligatoria.** Dirección pública de la aplicación. Es con la que se arman los enlaces de activación y de recuperación. |

Las dos marcadas como obligatorias se leen antes de decidir qué se ejecuta, así
que sin ellas la aplicación no arranca **y tampoco corre el enviador de correo**:
avisa del nombre de la que falta y termina. Las seis del SMTP son
opcionales: sin ellas la aplicación y el registro funcionan igual, y solo el
enviador se queja cuando no hay a quién entregar el correo.

Para definirlas en Windows, a nivel Usuario, y abrir después una terminal nueva:

```powershell
[Environment]::SetEnvironmentVariable('ConnectionStrings__PirryLedger', 'Host=127.0.0.1;Port=5432;Database=pirry_ledger;Username=<usuario>;Password=<contrasena>', 'User')
[Environment]::SetEnvironmentVariable('PIRRY_LEDGER_SMTP_HOST', 'smtp.gmail.com', 'User')
[Environment]::SetEnvironmentVariable('PIRRY_LEDGER_SMTP_PORT', '587', 'User')
[Environment]::SetEnvironmentVariable('PIRRY_LEDGER_SMTP_SECURITY', 'StartTls', 'User')
[Environment]::SetEnvironmentVariable('PIRRY_LEDGER_SMTP_USER', '<cuenta de envio>', 'User')
[Environment]::SetEnvironmentVariable('PIRRY_LEDGER_SMTP_PASSWORD', '<contrasena de aplicacion>', 'User')
[Environment]::SetEnvironmentVariable('PIRRY_LEDGER_SMTP_FROM', '<direccion remitente>', 'User')
[Environment]::SetEnvironmentVariable('PIRRY_LEDGER_PUBLIC_BASE_URL', 'http://localhost:5243', 'User')
```

Gmail exige una contraseña de aplicación, no la contraseña de la cuenta: se
genera en la configuración de seguridad de la cuenta de Google.

## Cómo clonar el repositorio

```powershell
git clone https://github.com/franklinpeguerodev/pirry-ledger.git
cd pirry-ledger
git checkout develop
```

## Cómo compilar

```powershell
dotnet --version
dotnet build pirry-ledger.slnx
```

Resultado esperado: `10.0.302` y una compilación con 0 errores y 0 advertencias.

## Cómo ejecutar las pruebas

```powershell
dotnet test pirry-ledger.slnx
```

Termina con `Correctas!` y el recuento de pruebas superadas. A fecha de hoy hay
**31 pruebas** en `PirryLedger.Core.AccessControl.Tests`, todas sobre la fase de
sesión, y ninguna necesita base de datos ni servidor SMTP: usan dobles en
memoria y un reloj falso, así que corren en unos 10 segundos.

## Preparar la base de datos

Las dos piezas comparten base de datos, cada una con sus propias tablas, así que
hay dos comandos. Da igual en qué orden se ejecuten.

AccessControl crea `ac_usuarios`, `ac_codigos_recuperacion`, `ac_tokens_activacion`
y `ac_sesiones`:

```powershell
$env:ConnectionStrings__PirryLedger = [Environment]::GetEnvironmentVariable('ConnectionStrings__PirryLedger', 'User')
dotnet ef database update --project Src/Core/PirryLedger.Core.AccessControl/PirryLedger.Core.AccessControl.Infrastructure
```

Resultado esperado:

```
Acquiring an exclusive lock for migration application. ...
Applying migration '20260930212544_CrearTablasDeControlDeAcceso'.
Applying migration '20261001002301_CrearTablaSesiones'.
Done.
```

Notifications crea la tabla de la cola de correo:

```powershell
$env:ConnectionStrings__PirryLedger = [Environment]::GetEnvironmentVariable('ConnectionStrings__PirryLedger', 'User')
dotnet ef database update --project Src/Core/PirryLedger.Core.Notifications/PirryLedger.Core.Notifications.Infrastructure
```

Resultado esperado:

```
Acquiring an exclusive lock for migration application. ...
Applying migration '20260930191411_CrearTablaCorreosEnCola'.
Done.
```

Dos mensajes que no son errores. `Acquiring an exclusive lock...` sale en todas las
ejecuciones. Y en una base recién creada, EF Core va primero a preguntar por su
tabla de migraciones, que todavía no existe, así que imprime un
`Failed executing DbCommand` con el `SELECT ... FROM "__EFMigrationsHistory"`
justo antes de aplicar. Si la base ya está al día la respuesta es otra:

```
No migrations were applied. The database is already up to date.
Done.
```

`not_correos_en_cola` queda con las columnas que exige el diseño: `id`,
`destinatario`, `asunto`, `cuerpo`, `estado`, `intentos`, `fecha_creacion_utc`,
`fecha_envio_utc` y `ultimo_error`. `ultimo_error` existe desde el principio pero
todavía no se escribe: llega en la semana 11.

La tabla `ac_sesiones` de esta fase queda así:

| Columna | Tipo | Para qué |
|---|---|---|
| `Id` | `uuid` | Clave primaria. |
| `UsuarioId` | `uuid` | Dueño de la sesión. Clave foránea con borrado en cascada. |
| `HashDelToken` | `varchar(128)` | SHA-256 del token en hexadecimal. **Único**, nunca el token en claro. |
| `EmitidaUtc` | `timestamptz` | Cuándo se creó la sesión. |
| `ExpiraUtc` | `timestamptz` | Vencimiento **absoluto**: 8 horas después de emitirla. |
| `CerradaUtc` | `timestamptz` | Cuándo se cerró. Nulo si sigue abierta. |
| `CredencialVersion` | `integer` | Copia de la del usuario al emitirla. Si no coinciden, la sesión ya no vale. |

Índices: único sobre `HashDelToken`, uno sobre `UsuarioId` y uno sobre
`ExpiraUtc` para poder limpiar las vencidas más adelante.

## Cómo arrancar la aplicación

```powershell
dotnet run --project Src/Host/PirryLedger.Host --launch-profile http
```

Arranca en `http://localhost:5243`. Se comprueba en la salida de la consola:

```
Now listening on: http://localhost:5243
Application started. Press Ctrl+C to shut down.
```

Devuelve `404` en cualquier ruta que no exista, porque el resto del sistema
todavía no está construido. Las seis rutas que sí existen están en la tabla de
abajo. De esas, `/yo` sin cabecera `Authorization` responde `401` y no `404`: la
ruta existe, lo que falta es la credencial.

## Endpoints de acceso control

| Método | Ruta | Qué hace |
|---|---|---|
| `POST` | `/api/auth/register` | Registra un usuario y encola el correo de activación. |
| `POST` | `/api/auth/reenviar-activacion` | Reenvía el enlace. La respuesta es idéntica exista o no el correo. |
| `GET` | `/activar?token=<valor>` | Activa la cuenta con el token del enlace. |
| `POST` | `/api/auth/login` | Devuelve el token de sesión en el cuerpo de la respuesta. |
| `POST` | `/api/auth/logout` | Cierra la sesión del token que llega en la cabecera. Responde `204` siempre. |
| `GET` | `/yo` | Nombre, correo y rol del usuario autenticado. Exige `Authorization: Bearer <token>`. |

Para probarlos a mano con la aplicación en marcha, en otra terminal:

```powershell
$cuenta = @{ nombre = 'Ana'; correo = 'ana@ejemplo.com'; contrasena = 'abc12345' } | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5243/api/auth/register -Method Post -Body $cuenta -ContentType 'application/json'
```

## Cómo enviar el correo pendiente

El enviador es un proceso aparte. En una terminal nueva, con la aplicación
detenida o en marcha, da igual:

```powershell
$env:ConnectionStrings__PirryLedger = [Environment]::GetEnvironmentVariable('ConnectionStrings__PirryLedger', 'User')
dotnet run --project Src/Host/PirryLedger.Host -- --send-mail
```

Imprime siempre un resumen y una línea final que depende del resultado. Sin
credenciales de SMTP válidas, un correo pendiente produce esto:

```
Correos tomados: 1
Correos enviados: 0
Correos fallidos: 1
  - ana@ejemplo.com: No se pudo entregar el correo a ana@ejemplo.com por smtp.gmail.com:587. 534 5.7.9 Please log in with your web browser and then try again. ...
Los correos siguen en la cola como pendientes y se volveran a intentar la proxima vez.
```

Son cuatro ramas distintas y solo una se cumple a la vez. Los correos que fallan
vuelven a `Pendiente` con `intentos` incrementado: no se pierden. Sin correo
pendiente la cuarta línea es `No habia correos pendientes de enviar.`; si hubo
envíos, es `Los correos enviados no se volveran a enviar aunque se ejecute el
comando otra vez.`

## Cómo provocar cada criterio de aceptación

### RF-CA-01 — el registro acepta un correo libre y rechaza el duplicado

Registra un correo cualquiera. Después repite con el mismo correo, también en
mayúsculas, y comprueba que la segunda vez responde `409` con el mismo mensaje que
un correo nuevo.

```sql
SELECT "Correo", "Activo" FROM ac_usuarios ORDER BY "Correo";
```

Hay una sola fila por correo, sin importar si se escribió en mayúsculas o con
espacios alrededor.

### RF-CA-02 — la contraseña no se guarda en claro

```sql
SELECT "Correo", left("HashDeContrasena", 40) AS inicio FROM ac_usuarios;
```

El hash empieza por `$argon2id$v=19$m=...` y **no** contiene la contraseña. Cada
fila tiene un hash distinto aunque dos usuarios usen la misma contraseña, porque
el salt es por usuario.

### RF-CA-14 — la contraseña tiene al menos 8 caracteres, con letras y números

```powershell
# Menos de 8 caracteres -> 400
Invoke-RestMethod -Uri http://localhost:5243/api/auth/register -Method Post -Body (@{ nombre='A'; correo='a@ejemplo.com'; contrasena='abc12' } | ConvertTo-Json) -ContentType 'application/json'
# Solo letras -> 400
Invoke-RestMethod -Uri http://localhost:5243/api/auth/register -Method Post -Body (@{ nombre='A'; correo='b@ejemplo.com'; contrasena='abcdefgh' } | ConvertTo-Json) -ContentType 'application/json'
```

### RF-CA-15 — la cuenta nace inactiva y el enlace es de un solo uso

Tras registrar, antes de abrir el enlace:

```sql
SELECT "Activo" FROM ac_usuarios;   -- false
SELECT "Cuerpo" FROM not_correos_en_cola ORDER BY "fecha_creacion_utc" DESC LIMIT 1;
```

El correo trae `http://localhost:5243/activar?token=<64 caracteres hexadecimales>`.
En la tabla de tokens solo está el SHA-256:

```sql
SELECT left("HashDelToken", 20) FROM ac_tokens_activacion;   -- SHA-256, no el token
```

Y con la cuenta todavía inactiva, el inicio de sesión se rechaza diciendo
exactamente que lo está:

```powershell
$credenciales = @{ correo = 'ana@ejemplo.com'; contrasena = 'abc12345' } | ConvertTo-Json
Invoke-WebRequest -Uri http://localhost:5243/api/auth/login -Method Post -Body $credenciales -ContentType 'application/json'
```

Resultado esperado: `401` con `{"mensaje":"La cuenta no esta activada."}`.

Este mensaje **sí** es distinto del de credenciales incorrectas, y solo se alcanza
con la contraseña correcta. Es lo que exige RF-CA-15. Con la contraseña
equivocada la respuesta es la genérica, aunque la cuenta siga inactiva: así el
mensaje no sirve para averiguar qué correos están registrados, porque hace falta
conocer la contraseña para llegar a él.

### RF-CA-16 — el enlace activa la cuenta y no se reutiliza

Abre el enlace del correo: responde `200 {"activado":true}` y la cuenta queda
`Activo = true`. Ábrelo otra vez y responde `400`.

```sql
SELECT "UsadoUtc" FROM ac_tokens_activacion;   -- con fecha tras el primer uso
```

Un token inexistente, uno ya usado y uno recortado devuelven el mismo `400`, para
no revelar si ese enlace existió.

### RF-CA-17 — el reenvío es idéntico exista o no el correo

Compara estas cuatro peticiones: la respuesta debe ser el mismo `202` con el mismo
cuerpo `{"enviado":true}` en los cuatro casos.

```powershell
$reenviar = 'http://localhost:5243/api/auth/reenviar-activacion'
Invoke-WebRequest $reenviar -Method Post -Body (@{correo='ana@ejemplo.com'}|ConvertTo-Json) -ContentType 'application/json'    # pendiente de activar
Invoke-WebRequest $reenviar -Method Post -Body (@{correo='nadie@ejemplo.com'}|ConvertTo-Json) -ContentType 'application/json' # ese correo no existe
Invoke-WebRequest $reenviar -Method Post -Body (@{correo='esto-no-es-correo'}|ConvertTo-Json) -ContentType 'application/json' # mal formado
Invoke-WebRequest $reenviar -Method Post -Body (@{correo='ana@ejemplo.com'}|ConvertTo-Json) -ContentType 'application/json'    # ya activa
```

Son cuatro y no cinco porque son cuatro las salidas del caso de uso: el correo
mal formado, el correo que no existe, la cuenta ya activa y la cuenta pendiente.
Solo la cuarta genera algo. "Cuenta activa con un token vivo" no es un quinto
caso: activar la cuenta es justamente lo que deja el token sin usar, así que una
cuenta activa nunca tiene un token vivo que reenviar. La cuarta petición se
ejecuta **después** de activar con RF-CA-16.

Que el enlace nuevo sirva y el viejo no:

```sql
SELECT u."Correo", count(t."Id") AS tokens_vivos
FROM ac_usuarios u
LEFT JOIN ac_tokens_activacion t
  ON t."UsuarioId" = u."Id" AND t."UsadoUtc" IS NULL
GROUP BY u."Correo";
```

`tokens_vivos` cuenta solo los tokens sin usar. El usuario pendiente de activar
tiene **uno solo**: al reenviar se invalidó el anterior, y los tokens ya
gastados siguen en la tabla con su `UsadoUtc` puesto.

### RF-NOT-08 — la operación no necesita servidor de correo

Deja el correo sin configurar y comprueba que encolar funciona igual. Una fila
nace en `Pendiente` sin que ninguna operación abra una conexión SMTP.

```sql
SELECT estado, intentos FROM not_correos_en_cola ORDER BY fecha_creacion_utc DESC;
```

`estado = 0` es `Pendiente` y `intentos = 0`.

### RF-NOT-09 — el envío ocurre en un proceso aparte

Inserta un correo pendiente con cualquier cliente SQL y ejecuta el enviador. El
paso a `Enviado` ocurre solo cuando el comando corre, nunca al encolar.

```sql
INSERT INTO not_correos_en_cola (id, destinatario, asunto, cuerpo, estado, intentos, fecha_creacion_utc)
VALUES (gen_random_uuid(), '<destinatario>', 'Prueba', 'Cuerpo de prueba', 0, 0, now());
```

Después del envío, `estado = 2` y `fecha_envio_utc` tiene valor.

### RF-NOT-12 — no se reenvía un correo ya enviado

Ejecuta el enviador dos veces seguidas con un correo pendiente:

```powershell
dotnet run --project Src/Host/PirryLedger.Host -- --send-mail
dotnet run --project Src/Host/PirryLedger.Host -- --send-mail
```

La primera ejecución envía; la segunda responde, con las cuatro líneas que el
comando imprime siempre:

```
Correos tomados: 0
Correos enviados: 0
Correos fallidos: 0
No habia correos pendientes de enviar.
```

Y el correo sigue con `intentos = 1`: no se reclamó dos veces. Con dos emisores
corriendo a la vez sobre tres correos pendientes, uno toma los tres y el otro
toma cero.

### RF-NOT-13 — las credenciales vienen de variables de entorno

Borra o cambia `PIRRY_LEDGER_SMTP_PASSWORD` en esa terminal, deja un correo
pendiente y ejecuta el enviador:

```powershell
$env:PIRRY_LEDGER_SMTP_PASSWORD = ''
dotnet run --project Src/Host/PirryLedger.Host -- --send-mail
```

El correo vuelve a `Pendiente` con `intentos = 1`, y el proceso informa:

```
Correos fallidos: 1
```

Ningún mensaje de error imprime el usuario ni la contraseña: se sustituyen por
`[usuario SMTP]` y `[contrasena SMTP]` antes de mostrarse o guardarse.

### RF-CA-03 — iniciar sesión devuelve un token y no revela qué cuentas existen

Necesitas una cuenta **activada**. Si aún no tienes una, registra
`ana@ejemplo.com` con la contraseña `abc12345` y abre el enlace como explica
RF-CA-16.

Con la aplicación en marcha, en otra terminal:

```powershell
$credenciales = @{ correo = 'ana@ejemplo.com'; contrasena = 'abc12345' } | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5243/api/auth/login -Method Post -Body $credenciales -ContentType 'application/json'
```

Resultado esperado: un token de 43 caracteres, sin `+`, `/` ni `=`.

Ahora bien, un correo que no existe y una contraseña equivocada tienen que ser
indistinguibles:

```powershell
Invoke-RestMethod -Uri http://localhost:5243/api/auth/login -Method Post `
  -Body (@{ correo = 'nadie@ejemplo.com'; contrasena = 'abc12345' } | ConvertTo-Json) `
  -ContentType 'application/json'
```

Resultado esperado: `401` con el mismo mensaje que el intento con contraseña
equivocada. Si los mensajes o el tiempo de respuesta fueran distintos, el
endpoint serviría para enumerar las cuentas que existen. Por eso el caso de uso
verifica la contraseña contra un hash señuelo cuando el correo no existe: los dos
caminos ejecutan la misma operación de Argon2.

### RF-CA-07 — el usuario autenticado conoce su nombre, correo y rol

```powershell
$sesion = Invoke-RestMethod -Uri http://localhost:5243/api/auth/login -Method Post -Body $credenciales -ContentType 'application/json'
Invoke-RestMethod -Uri http://localhost:5243/yo -Headers @{ Authorization = "Bearer $($sesion.token)" }
```

Resultado esperado: `Nombre`, `Correo` y `Rol`. Nunca el hash de la contraseña,
ni el token, ni `CredencialVersion`.

Y sin cabecera:

```powershell
Invoke-RestMethod -Uri http://localhost:5243/yo
```

Resultado esperado: `401 Sesion no valida.`

### RF-CA-18 — cerrar sesión invalida esa credencial

```powershell
Invoke-RestMethod -Uri http://localhost:5243/api/auth/logout -Method Post -Headers @{ Authorization = "Bearer $($sesion.token)" }
Invoke-RestMethod -Uri http://localhost:5243/yo -Headers @{ Authorization = "Bearer $($sesion.token)" }
```

El `logout` responde `204` y la segunda llamada responde `401`. Cerrar una sesión
no cierra las demás del mismo usuario: un empleado puede tener el móvil y el
portátil abiertos a la vez.

### RF-CA-19 — cinco intentos fallidos bloquean la cuenta quince minutos

```powershell
1..5 | ForEach-Object {
  Invoke-RestMethod -Uri http://localhost:5243/api/auth/login -Method Post `
    -Body (@{ correo = 'ana@ejemplo.com'; contrasena = 'malaclave1' } | ConvertTo-Json) `
    -ContentType 'application/json'
}
```

Ahora la contraseña correcta se sigue rechazando:

```powershell
Invoke-RestMethod -Uri http://localhost:5243/api/auth/login -Method Post -Body $credenciales -ContentType 'application/json'
```

Resultado esperado: `401` con **el mismo mensaje** que los intentos fallidos. Un
mensaje propio confirmaría que ese correo existe. Pasados quince minutos el
bloqueo se levanta solo y entra con la contraseña correcta.

La expiración de las ocho horas no se prueba esperando: `IClock` es inyectable y
las pruebas automatizadas avanzan el reloj.

## Qué NO incluye

- **Recuperación de contraseña.** La tabla `ac_codigos_recuperacion` existe, pero
  no hay endpoint ni correo: llega más adelante.
- **Cambio de contraseña.** El dominio ya sube `CredencialVersion` y hay pruebas
  de que invalida las sesiones, pero no hay endpoint para pedirlo.
- **Limpieza de las sesiones vencidas.** Las filas se quedan en `ac_sesiones`.
  El índice sobre `ExpiraUtc` está para hacerlo después; esta fase no borra nada
  por su cuenta.
- **Roles en los endpoints.** `Autenticar.EjecutarConRolAsync` ya distingue el
  `403` del `401` y hay pruebas, pero ningún endpoint exige todavía un rol porque
  no hay operación de administración a la que restringir.
- **Renovación de la sesión.** El vencimiento es absoluto: usar el token no lo
  estira. Se decidió así a propósito y está en `docs/adr/001-credencial-de-sesion.md`.
- **Reintentos automáticos, estado fallido y escritura de `ultimo_error`** en el
  envío de correo. Llega en la semana 11.
- **Vista de administración de la cola.** Llega en la semana 11.
- **Módulo de negocio.** La máquina de estados de `Factura` se declara al final de
  la práctica.