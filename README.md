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

Devuelve `404` en cualquier ruta porque todavía no hay endpoints.

## Cómo enviar el correo pendiente

El enviador es un proceso aparte. En una terminal nueva, con la aplicación
detenida o en marcha, da igual:

```powershell
$env:ConnectionStrings__PirryLedger = [Environment]::GetEnvironmentVariable('ConnectionStrings__PirryLedger', 'User')
dotnet run --project Src/Host/PirryLedger.Host -- --send-mail
```

## Cómo provocar cada criterio de aceptación

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