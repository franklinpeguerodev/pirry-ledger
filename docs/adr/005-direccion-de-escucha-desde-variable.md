# ADR 005 - La dirección de escucha sale de `PIRRY_LEDGER_PUBLIC_BASE_URL`

- **Estado:** aceptada
- **Fecha:** 2026-10-04
- **Requisitos:** RD-08, RD-10 (configuración y errores); ningún requisito funcional cambia
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

`docs/bugs/001-puerto-escucha-vs-public-base-url.md` registró que el puerto de
escucha de la API y `PIRRY_LEDGER_PUBLIC_BASE_URL` eran **dos configuraciones
independientes** que coincidían por casualidad, sin ninguna validación entre
ellas:

| | De qué dependía | Valor a fecha del bug |
|---|---|---|
| Puerto donde escucha la API | `applicationUrl` del perfil de `launchSettings.json` | `http://localhost:5243` |
| `PIRRY_LEDGER_PUBLIC_BASE_URL` | Variable de entorno leída al arrancar | `http://localhost:5243` |

La desincronización ya estaba comprobada con tres arranques reales: `dotnet run`
escuchaba en 5243, el DLL compilado en 5000 y el perfil `https` en 7258+5243,
mientras los enlaces de los correos seguían diciendo 5243 en los tres casos. El
correo de activación real de la cola contenía `http://localhost:5243/activar`,
que solo funciona en la misma máquina: el caso de uso del proyecto es el uso
diario desde una tablet (`README.md`).

Además, el README decía que la variable servía para "los enlaces de activación
y recuperación", y la recuperación no lleva enlace: lleva un código suelto
(`CorreoDeRecuperacion.cs`).

## Decisión

`PIRRY_LEDGER_PUBLIC_BASE_URL` pasa a ser la **única fuente de verdad**: define
el host y el puerto donde escucha la API **y** la dirección con la que se
construyen los enlaces de activación.

1. `Program.cs` lee la variable **una sola vez** (`LeerUrlBaseObligatoria`) y ese
   valor, guardado en `urlBase`, alimenta las dos cosas:
   `builder.WebHost.UseUrls(urlBase)` y
   `builder.Services.AddAccessControl(conexion, urlBase)`.
2. `Properties/launchSettings.json` **pierde `applicationUrl`** en ambos
   perfiles. Los perfiles siguen existiendo y solo aportan
   `ASPNETCORE_ENVIRONMENT=Development`.
3. La variable sigue siendo obligatoria: sin ella la aplicación se detiene y
   avisa por el nombre de la variable, sin traza (RD-08). No cambia el texto ni
   el código de salida.
4. `--send-mail` no cambia: la validación corre antes de la rama del comando,
   decisión que ya estaba documentada como intencional en el bug (punto 4).

Si la variable es `http://localhost:5243`, Kestrel se ata a ese host y ese
puerto. Si es `http://192.168.1.10:5243`, la API escucha en esa IP y los
enlaces del correo salen con esa misma dirección: un solo valor que sirve para
ambas cosas, que es exactamente lo que hace falta para abrirlo desde la tablet.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Mantener `launchSettings` como fuente del puerto y solo documentar la diferencia (opción A del bug) | Cero riesgo técnico, pero deja la desincronización silenciosa: el DLL seguiría escuchando en 5000 con los enlaces en 5243 y nadie lo vería. |
| Avisar por consola si el puerto escuchado no coincide con la variable (opción B del bug) | Coge la deriva en lugar de eliminarla, y puede dar falsos positivos legítimos detrás de un proxy. Además ya no haría falta: con una sola fuente no hay nada que comparar. |
| Derivar la URL de la petición entrante y prescindir de la variable (opción C del bug) | El enlace se construye al encolar el correo, en una operación que no siempre tiene una petición delante (reenvío de activación, por ejemplo) y el correo se envía mucho después, por otro proceso. Además eliminaría una variable obligatoria documentada: es cambio de diseño. |
| Escuchar en todos los interfaces (`+:{puerto}`) | Abriría el puerto en todas las interfaces aunque la variable diga `localhost`, y los enlaces seguirían diciendo `localhost`, que no sirve desde la tablet. Contradice la idea de una sola fuente. |
| Mantener `applicationUrl` y confiar en que `UseUrls` gana | Verificado: `UseUrls` gana hoy sobre `ASPNETCORE_URLS`. Pero dejaría una segunda fuente que podría volver a mandar en una versión futura del framework. Quitarla elimina la pregunta. |

## Verificación

Ejecutada el 2026-10-04 en la rama `fix/standardize-listen-url-on-base-env-var`:

- **Antes del cambio**, con `PIRRY_LEDGER_PUBLIC_BASE_URL=http://localhost:5999`
  y `dotnet run --launch-profile http` → `Now listening on: http://localhost:5243`
  (la variable se ignoraba para el puerto: el bug).
- **Después**, `dotnet run --launch-profile http` con la variable del paso 4 del
  README → `Now listening on: http://localhost:5243` y
  `Hosting environment: Development` (igual que antes; el recorrido del README
  no cambia).
- **Después**, DLL compilada directamente
  (`dotnet Src\Host\PirryLedger.Host\bin\Debug\net10.0\PirryLedger.Host.dll`) →
  `Now listening on: http://localhost:5243` (antes era 5000: el bug).
- **Después**, variable de proceso `http://localhost:5999` →
  `Now listening on: http://localhost:5999`, y `GET /yo` responde `401` en ese
  puerto mientras 5243 no responde: la API sigue a la variable.
- **Sonda de precedencia:** `ASPNETCORE_URLS=http://localhost:5888` junto con la
  variable en 5243 → escucha 5243. `UseUrls` manda sobre `ASPNETCORE_URLS`.
- `--launch-profile https` → escucha 5243 (el 7258 ya no existe, ver abajo).
- `--send-mail` → salida idéntica a la anterior (`Correos tomados: 0`).
- Sin la variable → `Falta la variable de entorno
  PIRRY_LEDGER_PUBLIC_BASE_URL. El README explica como definirla.` y código de
  salida 1, sin traza (RD-08).
- `dotnet build pirry-ledger.slnx` con 0 errores y 0 advertencias.
- `dotnet test pirry-ledger.slnx`: 90 pruebas en verde, las mismas que antes del
  cambio (6 de Notifications + 84 de AccessControl).
- `git diff` limitado a `Src/Host/PirryLedger.Host/`: no hay ningún cambio en
  `Src/Core`, `Src/Business` ni en `Tests/`.

## Consecuencias

**A favor**

- Una sola fuente: es imposible que el puerto escuchado y los enlaces del correo
  digan cosas distintas, sin validación ni aviso que mantener.
- Da igual cómo se lance la aplicación (`dotnet run`, otro perfil o el DLL
  compilado): siempre el mismo puerto.
- Para atender desde la tablet se cambia **un** valor, y la API y los enlaces
  cambian juntos.
- El README deja de tener dos verdades sobre el mismo puerto.

**En contra, asumido a conciencia**

- El perfil `https` de `launchSettings.json` deja de escuchar en 7258: ahora
  escucha lo que diga la variable. Ese perfil no estaba documentado en el
  README. Si en el futuro hace falta HTTPS, se decide en otra ADR.
- Cambiar el puerto exige cambiar la variable de entorno (antes bastaba con otro
  perfil). Es el precio de que no haya dos sitios donde cambiarlo.
- Si la variable trae un host que no pertenece a la máquina o un puerto ya
  ocupado, la aplicación se detiene con el error del sistema en lugar de
  arrancar en otro puerto. Es deseable: antes arrancaba "feliz" en un puerto que
  los enlaces no mencionaban.

## Qué queda fuera

Esta ADR no toca la lógica de negocio, autenticación, correo ni pruebas; no
cambia el texto de los correos ni las rutas; no añade validaciones nuevas ni
avisos de arranque; no decide nada sobre HTTPS ni sobre el frontend; y no
modifica ninguna variable obligatoria existente.
