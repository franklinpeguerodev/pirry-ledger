# Bug 001 — El puerto de escucha y `PIRRY_LEDGER_PUBLIC_BASE_URL` no están relacionados

| Campo | Valor |
|---|---|
| Estado | **Resuelto** — Franklin pidió el arreglo el 2026-10-04 |
| Creado | 2026-10-03 |
| Resuelto | 2026-10-04, rama `fix/standardize-listen-url-on-base-env-var` |
| Detectado por | Agente, durante la verificación del README tras el rename snake_case |
| Archivos afectados | `README.md`, `Src/Host/PirryLedger.Host/Program.cs`, `Src/Host/PirryLedger.Host/Properties/launchSettings.json` |

## Condición de cierre (levantada por Franklin el 2026-10-04)

Este bug estaba congelado: solo se arreglaba si Franklin lo pedía de forma
explicita, y ningún rama, commit o PR podía incluir el arreglo sin esa petición.
Franklin lo pidió el 2026-10-04 ("`PIRRY_LEDGER_PUBLIC_BASE_URL` pasa a ser la
única fuente de verdad: define el host:puerto donde escucha la API y la
dirección de los enlaces de activación"), junto con las decisiones de
estandarizar en la variable, quitar `applicationUrl` de `launchSettings.json` y
documentarlo en un ADR.

La sección siguiente conserva lo que se observó, tal como quedó escrito.

## Qué se observó (2026-10-03, histórico)

El README presenta como una sola cosa lo que son dos configuraciones
independientes, y no avisa de que pueden desincronizarse:

| | De qué depende | Valor a fecha de este documento |
|---|---|---|
| Puerto donde escucha la API | Perfil de `launchSettings.json`, vía `dotnet run --launch-profile` | `http://localhost:5243` |
| `PIRRY_LEDGER_PUBLIC_BASE_URL` | Variable de entorno leída al arrancar | `http://localhost:5243` |

Coinciden por casualidad: **no existe ninguna validación** entre ambas.

### 1. El README dice que la variable se usa para los enlaces de "activación y recuperación"

`README.md:107` (tabla de variables, a fecha 2026-10-03). Es incorrecto: la
recuperación no lleva enlace, lleva un código suelto.

### 2. Nada detecta la desincronización

- `Program.cs:142-155` (`LeerUrlBaseObligatoria`) solo exige que la variable
  exista y le recorta el `/` final. No la compara con lo que quedó escuchando.
- Si se arranca el DLL compilado en vez de `dotnet run`, launchSettings no
  aplica y la API escucha en **5000**, mientras la variable y todos los
  comandos del README siguen diciendo 5243. La API arranca sin ningún aviso.
- Perfil `https` (`launchSettings.json:17`): escucha en 7258 y 5243, pero los
  enlaces siguen diciendo `http://localhost:5243`. El README no menciona ese
  perfil.

### 3. Los enlaces con `localhost` solo sirven en la misma máquina

El correo de activación real generado el 2026-10-03 contenía
`http://localhost:5243/activar?token=...`. Quien lo abra en la tablet o en el
celular llega al localhost de *su* dispositivo. El caso de uso del proyecto es
uso diario desde tablet (`README.md:450`). Por ahora el README esquiva el
problema leyendo la cola con SQL (`README.md:285`), pero no lo explica.

### 4. La variable también se exige en `--send-mail`

`Program.cs:27` corre antes de la rama `--send-mail` (`Program.cs:43`), así que
el enviador se niega a arrancar sin ella aunque nunca construye enlaces. El
README (`README.md:118-122`) ya lo documenta correctamente; se deja constancia
de que es intencional o decidido, no un error.

## Evidencia (comandos ejecutados el 2026-10-03)

- `dotnet run --project Src/Host/PirryLedger.Host --no-build` **sin**
  `--launch-profile` → `Now listening on: http://localhost:5243` (toma el
  primer perfil del archivo).
- `dotnet Src\Host\PirryLedger.Host\bin\Debug\net10.0\PirryLedger.Host.dll` →
  `Now listening on: http://localhost:5000` (launchSettings no aplica).
- `ASPNETCORE_URLS` no está definida (Usuario ni Máquina): no hay override.
- Correo de activación real (leído de `not_correos_en_cola`): enlace con
  `http://localhost:5243/activar?token=...`.
- Correo de recuperación real (leído de `not_correos_en_cola`): solo el código
  de 64 caracteres, **sin URL**.
- Consumidores reales de la variable: solo `RegisterUser.cs:103` y
  `ResendActivationLink.cs:85`, ambos vía `CorreoDeActivacion.cs:28`.
  `CorreoDeRecuperacion.cs:7` no la recibe.

## Qué NO está roto

En la máquina de Franklin, a fecha de este documento, todo cuadra: la variable
es `http://localhost:5243`, el perfil escucha en 5243 y los enlaces salen
correctos. Ninguna verificación del README falla por esto. Por eso el bug es de
claridad y de robustez, no una falla activa.

## Opciones de arreglo que se plantearon (2026-10-03)

- **A. Solo documentar.** En la tabla de variables y en el paso 7 del README:
  el puerto de escucha viene del perfil de launchSettings y no de la variable;
  el valor debe ser *la dirección desde la que se abrirán los correos* (para la
  tablet, `http://<IP-del-equipo>:5243`); corregir "activación y recuperación"
  → "activación". Sin código. *(La recomienda el agente.)*
- **B. Aviso al arrancar.** Si el host:puerto de la variable no coincide con lo
  que quedó escuchando, imprimir un aviso por consola. Coge la deriva, pero
  puede dar falsos positivos legítimos detrás de un proxy.
- **C. Derivar la URL de la petición entrante** en vez de usar la variable.
  Elimina la deriva, pero elimina o contradice una variable obligatoria
  documentada: es cambio de diseño, necesitaría ADR y ningún requisito lo pide.

A y B son compatibles entre sí.

**Decisión de Franklin (2026-10-04):** ninguna de las tres literalmente. Elige
estandarizar en la variable, de modo que haya **una sola fuente de verdad**:
`PIRRY_LEDGER_PUBLIC_BASE_URL` fija el host:puerto de escucha *y* la dirección
de los enlaces. Detalle y alternativas descartadas en
`docs/adr/005-direccion-de-escucha-desde-variable.md`.

## Resolución (2026-10-04)

- `Program.cs`: la variable se lee una sola vez (`urlBase`) y ese valor alimenta
  `builder.WebHost.UseUrls(urlBase)` (puerto de escucha) y
  `AddAccessControl(conexion, urlBase)` (enlaces). Sin cambios de lógica.
- `launchSettings.json`: `applicationUrl` fuera de los dos perfiles; solo
  aportan `ASPNETCORE_ENVIRONMENT=Development`.
- `README.md`: la tabla de variables (`README.md:107`) y el paso 7 explican que
  la variable fija host y puerto, de dónde sale el puerto y cómo usarlo desde
  otra tablet; desaparece la referencia a enlaces de recuperación.
- Comentarios de `Program.cs` y `ConfiguracionEntorno.cs`: la recuperación no
  usa esta variable.
- `docs/adr/005-direccion-de-escucha-desde-variable.md` documenta la decisión.

Evidencia (comandos ejecutados el 2026-10-04 en la rama
`fix/standardize-listen-url-on-base-env-var`):

- **Antes del cambio**, variable `http://localhost:5999` + `dotnet run
  --launch-profile http` → `Now listening on: http://localhost:5243` (la
  variable se ignoraba: el bug).
- **Después**, DLL compilada → `Now listening on: http://localhost:5243`
  (antes `5000`).
- **Después**, variable `http://localhost:5999` →
  `Now listening on: http://localhost:5999`, `GET /yo` → `401` en ese puerto y
  5243 sin responder.
- Sonda de precedencia: `ASPNETCORE_URLS=http://localhost:5888` con la variable
  en 5243 → escucha 5243 (`UseUrls` manda).
- Sin la variable → `Falta la variable de entorno
  PIRRY_LEDGER_PUBLIC_BASE_URL. El README explica como definirla.`, salida 1,
  sin traza.
- `dotnet build pirry-ledger.slnx`: 0 errores, 0 advertencias.
- `dotnet test pirry-ledger.slnx`: 90 pruebas en verde, las mismas que antes
  (6 Notifications + 84 AccessControl).
- `git diff` limitado a `Src/Host/PirryLedger.Host/`.

## Criterios de aceptación (cumplidos el 2026-10-04)

1. La tabla de variables del README describe la variable como la que fija el
   host y el puerto de escucha y la dirección de los enlaces de **activación**,
   sin afirmar que la recuperación use enlaces. → `README.md:107`.
2. El README indica de dónde sale el puerto de escucha (de la variable, no del
   perfil) y qué hacer si se usará desde otro dispositivo
   (`http://<IP-del-equipo>:5243`). → paso 7.
3. El arreglo incluye código: con una sola fuente ya no puede haber variable
   apuntando a un puerto distinto del escuchado; se verificó con la variable en
   5999 → escucha 5999, y `dotnet test` en verde.
4. El cambio se documenta en el mismo PR: `README.md` y
   `docs/adr/005-direccion-de-escucha-desde-variable.md`.
