# ADR 006 - Local configuration from a `.env` file loaded with `DotNetEnv`

- **Estado:** aceptada
- **Fecha:** 2026-10-04
- **Requisitos:** RD-10 (secretos en variables de entorno), RF-NOT-13 (credenciales SMTP); ningún requisito funcional cambia
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

RD-10 exige que las credenciales y claves se lean de variables de entorno. El
proyecto vive con **once** variables: `ConnectionStrings__PirryLedger`,
`PIRRY_LEDGER_PUBLIC_BASE_URL`, las seis `PIRRY_LEDGER_SMTP_*` y las tres
`PIRRY_LEDGER_FIRST_ADMIN_*`.

Hasta esta ADR el README obligaba a definirlas con once comandos
`[Environment]::SetEnvironmentVariable(..., 'User')`, o a repetir el bloque
`$env:` en cada terminal. Eso tenía tres problemas comprobados:

1. **Los secretos se guardan en el registro de Windows** del usuario (contraseña
   de la base, contraseña del primer Administrador y contraseña de aplicación
   SMTP), fuera del repositorio sí, pero en un sitio que no es el proyecto y que
   queda ahí entre sesiones.
2. **Migrar de máquina es repetir once comandos**, con la probabilidad de olvidar
   uno y encontrarse un `Falta la variable de entorno ...` en el arranque.
3. No había una lista única de "qué variables usa este proyecto": estaban
   repartidas entre el paso 4 y el paso SMTP del README.

El `.gitignore` ya tenía previsto el formato (`.env`, `.env.*` y la excepción
`!.env.example`, líneas 152-155), pero no existía ningún archivo `.env`.

## Decisión

El Host carga un archivo `.env` de desarrollo local **antes** de construir la
configuración, con el paquete `DotNetEnv` 3.2.0.

1. **Punto de carga.** `Program.cs` llama a `CargarArchivoEnv()` antes de
   `WebApplication.CreateBuilder(args)`. Así las variables entran por el mismo
   camino de siempre: `Environment` → `IConfiguration` →
   `ConfiguracionEntorno`. RD-10 sigue cumplido porque al final siguen siendo
   variables de entorno, no un mecanismo de configuración distinto.
2. **El entorno real manda.** Se usa `Env.NoClobber()`: una variable ya definida
   en la terminal o en el sistema **no** la pisa el archivo. Un despliegue con
   variables de verdad sigue funcionando exactamente igual que hoy.
3. **Falta de archivo no es error.** `CargarArchivoEnv()` busca el `.env` desde
   el directorio de trabajo hacia arriba y, si no lo encuentra, vuelve sin más.
   El comportamiento anterior (mensaje con el nombre de la variable y código de
   salida 1, sin traza, RD-08) queda intacto y verificado.
4. **El archivo real nunca se versiona.** `.env` está en el `.gitignore`. Lo que
   sí se versiona es `.env.example`, con las once variables, sus descripciones y
   valores de ejemplo, nunca valores reales.
5. **No cambia ninguna variable.** Nombres, obligatoriedad y errores de
   `ConfiguracionEntorno` y `Program.cs` son los mismos. Solo cambia de dónde
   salen en desarrollo local.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Loader de `.env` escrito a mano (aproximadamente 40 líneas) | Evita el paquete, pero es código propio que hay que mantener y probar: parser de comillas, de comentarios y de valores con `#`, que son exactamente los fallos que un paquete de uso general ya tiene resueltos. |
| Script de PowerShell que define las variables y lanza la aplicación | Cero dependencias, pero no es `.env`: no sirve desde otro sistema ni desde otro lenguaje, y obliga a rodear cada comando (`dotnet ef`, `--send-mail`) con el script. |
| Mantener los once `SetEnvironmentVariable` del README | Funciona, pero deja los secretos en el registro de Windows, exige repetir los comandos en cada máquina y no da una lista única del proyecto. |
| `appsettings.*.local.json` con la configuración del desarrollador | Es el mecanismo natural de ASP.NET Core, pero guarda secretos en archivos, no en variables de entorno (RD-10), y de todos modos esos archivos ya están en el `.gitignore` del repo para no volver a esos archivos. |
| `dotnet user-secrets` para los secretos | Mecanismo oficial del SDK y sin paquete nuevo, pero es una segunda fuente de verdad junto al entorno, y en la tablet o en un despliegue seguirían haciendo falta las variables: dos caminos en lugar de uno. |

## Verificación

Ejecutada el 2026-10-04 en la rama `fix/standardize-listen-url-on-base-env-var`:

- `git check-ignore -v .env` → `.gitignore:153:.env`; `git status --short` muestra
  `?? .env.example`, es decir, el ejemplo se puede versionar y el `.env` real no.
- `dotnet add Src/Host/PirryLedger.Host/PirryLedger.Host.csproj package DotNetEnv`
  → `DotNetEnv 3.2.0` (arrastra `Superpower 3.0.0`, dependencia suya). Es el
  único paquete nuevo del repositorio.
- `dotnet build pirry-ledger.slnx` → 0 errores y 0 advertencias.
  `dotnet test pirry-ledger.slnx` → 90 pruebas en verde (6 Notifications + 84
  AccessControl).
- **Solo `.env`, sin ninguna variable de entorno en el proceso** (las once
  borradas con `Remove-Item Env:` antes de lanzar) →
  `Now listening on: http://localhost:5243`, `GET /yo` → `401`, `stderr` vacío.
- **Precedencia:** `$env:PIRRY_LEDGER_PUBLIC_BASE_URL = 'http://localhost:5999'`
  con el `.env` en 5243 → `Now listening on: http://localhost:5999`,
  `GET :5999/yo` → `401` y `GET :5243/yo` sin respuesta.
- **Sin `.env` y sin variables:** `ExitCode: 1` y
  `Falta la variable de entorno ConnectionStrings__PirryLedger. El README
  explica como definirla.`, sin traza, idéntico al comportamiento anterior.
- **Parseo del bloque SMTP del README** con `dotnet fsi` y el paquete instalado,
  sobre un archivo temporal con
  `PIRRY_LEDGER_SMTP_PASSWORD="clave de prueba 12345"` →
  `SMTP_PASSWORD parseado = [clave de prueba 12345]`: el valor con espacios
  llega completo y sin comillas.
- `git diff` sin cambios en `Src/Core`, `Src/Business` ni `Tests/`: solo el Host,
  la plantilla y la documentación.

## Consecuencias

**A favor**

- Una sola lista del proyecto (`.env.example`): se ve de un vistazo qué variables
  usa el sistema y para qué sirve cada una.
- Clonar el repositorio y preparar el entorno pasa de once comandos a
  `Copy-Item .env.example .env` y editar.
- El mismo archivo sirve para la API, las migraciones y `--send-mail`, sin
  repetir nada por terminal.
- Los secretos de desarrollo dejan el registro de Windows.

**En contra, asumido a conciencia**

- Se añade una dependencia de terceros al Host (`DotNetEnv`), a la que hay que
  revisar actualizaciones como a cualquier paquete. Es el precio de no escribir
  un parser casero.
- La precedencia es una regla que hay que recordar: si una variable está en el
  entorno, el `.env` **no** la cambia, y eso puede confundir al principio cuando
  alguien edita el `.env` y no ve el efecto.
- El archivo se busca hacia arriba desde el directorio de trabajo: lanzar la
  aplicación desde un sitio que no es el repositorio no encuentra `.env`, y
  entonces manda el entorno (que es el comportamiento de producción).

## Qué queda fuera

Esta ADR no cambia ninguna variable, su nombre ni su obligatoriedad; no toca
`ConfiguracionEntorno`, los mensajes de error ni RD-10; no introduce secretos en
el repositorio (el `.env` real sigue ignorado); no decide nada sobre dónde viven
las variables en un despliegue real, donde siguen siendo variables de entorno del
sistema; y no cambia el comportamiento cuando el archivo no existe.
