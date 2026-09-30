# Pirry Ledger

ERP para un negocio de comida rápida. Práctica 1 — Control de acceso.

En este punto el repositorio tiene la cola de correo con su persistencia y un
comando para enviar lo pendiente. **La API todavía no expone ningún endpoint**:
el registro, el inicio de sesión y la recuperación llegan en las fases siguientes.

## Requisitos

- .NET SDK 10.0.302. La versión exacta la fija `global.json`, no hace falta
  instalar nada más.
- PostgreSQL 18, con una base `pirry_ledger` y un rol que sea dueño de ella.
- Git.

## Variables de entorno

La aplicación lee todo de variables de entorno. **Ninguna va en el repositorio**
(RD-10). En esta tabla está el nombre y para qué sirve cada una, nunca el valor.

| Variable | Para qué sirve |
|---|---|
| `ConnectionStrings__PirryLedger` | Cadena de conexión a PostgreSQL: host, puerto, base de datos, usuario y contraseña. |
| `PIRRY_LEDGER_SMTP_HOST` | Servidor SMTP saliente, por ejemplo `smtp.gmail.com`. |
| `PIRRY_LEDGER_SMTP_PORT` | Puerto SMTP. Con `StartTls` suele ser `587`. |
| `PIRRY_LEDGER_SMTP_SECURITY` | Cómo se cifra el transporte: `StartTls`, `Ssl` o `Ninguno`. |
| `PIRRY_LEDGER_SMTP_USER` | Cuenta con la que se autentica el envío. |
| `PIRRY_LEDGER_SMTP_PASSWORD` | Contraseña de aplicación de esa cuenta. Nunca la contraseña de la cuenta. |
| `PIRRY_LEDGER_SMTP_FROM` | Dirección de correo que aparece como remitente. |
| `PIRRY_LEDGER_PUBLIC_BASE_URL` | Dirección pública de la aplicación. Es con la que se arman los enlaces de activación y de recuperación. |

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

Termina con código de salida 0. **Aviso**: los proyectos de prueba existen pero
todavía no hay ninguna prueba escrita, así que el runner responde `No hay
ninguna prueba disponible`. Eso es lo esperado, no un fallo.

## Preparar la base de datos

La migración crea la tabla de la cola de correo:

```powershell
$env:ConnectionStrings__PirryLedger = [Environment]::GetEnvironmentVariable('ConnectionStrings__PirryLedger', 'User')
dotnet ef database update --project Src/Core/PirryLedger.Core.Notifications/PirryLedger.Core.Notifications.Infrastructure
```

Resultado esperado:

```
Applying migration '20260930191411_CrearTablaCorreosEnCola'.
Done.
```

Crea `not_correos_en_cola` con las columnas que exige el diseño: `id`,
`destinatario`, `asunto`, `cuerpo`, `estado`, `intentos`, `fecha_creacion_utc`,
`fecha_envio_utc` y `ultimo_error`.

## Cómo arrancar la aplicación

```powershell
dotnet run --project Src/Host/PirryLedger.Host --launch-profile http
```

Arranca en `http://localhost:5243`. Se comprueba en la salida de la consola:

```
Now listening on: http://localhost:5243
Application started. Press Ctrl+C to shut down.
```

Devuelve `404` en cualquier ruta que no sea una de las dos de acceso control
(`/api/auth/register`, `/api/auth/reenviar-activacion` y `/activar`), porque el
resto todavía no está construido.

## Endpoints de acceso control

| Método | Ruta | Qué hace |
|---|---|---|
| `POST` | `/api/auth/register` | Registra un usuario y encola el correo de activación. |
| `POST` | `/api/auth/reenviar-activacion` | Reenvía el enlace. La respuesta es idéntica exista o no el correo. |
| `GET` | `/activar?token=<valor>` | Activa la cuenta con el token del enlace. |

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

### RF-CA-16 — el enlace activa la cuenta y no se reutiliza

Abre el enlace del correo: responde `200 {"activado":true}` y la cuenta queda
`Activo = true`. Ábrelo otra vez y responde `400`.

```sql
SELECT "UsadoUtc" FROM ac_tokens_activacion;   -- con fecha tras el primer uso
```

Un token inexistente, uno ya usado y uno recortado devuelven el mismo `400`, para
no revelar si ese enlace existió.

### RF-CA-17 — el reenvío es idéntico exista o no el correo

Compara estas cinco peticiones: la respuesta debe ser el mismo `202` con el mismo
cuerpo en los cinco casos.

```powershell
$reenviar = 'http://localhost:5243/api/auth/reenviar-activacion'
Invoke-WebRequest $reenviar -Method Post -Body (@{correo='ana@ejemplo.com'}|ConvertTo-Json) -ContentType 'application/json'   # pendiente
Invoke-WebRequest $reenviar -Method Post -Body (@{correo='nadie@ejemplo.com'}|ConvertTo-Json) -ContentType 'application/json' # no existe
Invoke-WebRequest $reenviar -Method Post -Body (@{correo='esto-no-es-correo'}|ConvertTo-Json) -ContentType 'application/json'   # mal formado
```

Que el enlace nuevo sirva y el viejo no:

```sql
SELECT u."Correo", count(t."Id") AS tokens_vivos
FROM ac_usuarios u LEFT JOIN ac_tokens_activacion t ON t."UsuarioId" = u."Id"
GROUP BY u."Correo";
```

El usuario pendiente tiene **un solo** token: el anterior se borró al reenviar.

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

La primera ejecución envía; la segunda responde:

```
Correos tomados: 0
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

## Qué NO incluye

- **Ningún endpoint.** La API no expone rutas todavía. Lo que sigue es la Fase 2:
  registro con activación por correo.
- **Reintentos automáticos, estado fallido y escritura de `ultimo_error`.** Un
  envío fallido devuelve el correo a `Pendiente` y espera a que alguien vuelva a
  lanzar el comando. Llega en la semana 11.
- **Vista de administración de la cola.** Llega en la semana 11.
- **Cuerpo de los correos.** La cola sabe entregar un texto, pero nadie redacta
  todavía el mensaje de activación ni el de recuperación.
- **Control de acceso.** Sin entidades, sin hash de contraseña, sin endpoints.
- **Módulo de negocio.** La máquina de estados de `Factura` se declara al final de
  la práctica.