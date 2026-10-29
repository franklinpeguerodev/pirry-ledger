# Bitácora de la Práctica 3 — Módulo de negocio, sus pruebas y su integración

- **Práctica:** 3 — Módulo de negocio, suite de la máquina de estados e integración con Gestión de permisos (Programación III, Bloque 3, Semana 8).
- **Repositorio:** `franklinpeguerodev/pirry-ledger`.
- **Herramienta:** agente de OpenCode en mi máquina (Windows, PowerShell 5.1, `dotnet` SDK 10.0.400 según `global.json`, 10.0.403 instalado).
- **Rama de trabajo:** `docs/practica-3-scope`, desde `develop`.

La regla es la misma de las bitácoras anteriores: **lo que devuelve el agente es un borrador, no una verdad**. Nada se sube sin que yo lo lea y lo ejecute en mi máquina.

---

### Entrada 1 — Auditoría de `docs/`, reconocimiento del código real y plan por fases (2026-10-29)

**Qué se pidió.** Antes de escribir una línea de la Práctica 3, analizar el contenido nuevo de `docs/` (brief, alcance, requisitos), compararlo con el código que ya existe, señalar las contradicciones y entregar un plan de ejecución dividido en fases, donde cada fase tenga su rama, sus commits atómicos y un pull request que yo autorizo antes de mergear.

**Qué devolví.**

1. **El reconocimiento del código real, no del que el plan suponía:**

   | El documento decía | El código tenía |
   |---|---|
   | Siete estados | Cuatro: `Draft`, `Issued`, `Paid`, `Cancelled` (`InvoiceState.cs:4-10`) |
   | Once transiciones permitidas | Cuatro (`InvoiceStateMachine.cs:9-16`) |
   | `Invoice` con ítems, pagos, deuda e historial | Solo `Id` y `State` (`Invoice.cs:19-21`) |
   | Módulo con Domain, Application, Infrastructure y Api | Domain y Application; `Application` sin un solo archivo `.cs`, sin Infrastructure, sin Api |
   | Suite de la máquina | Ningún proyecto de pruebas de Business |
   | Operación que consume una concesión | `IPermissionAuthority` solo tiene `IsKnown` y `UserHasPermissionAsync` |
   | Permiso `CancelInvoice:<id>` | `PermissionCatalog` es una lista cerrada con `ViewSalesSummary` y `CreatePermissionRequest.cs:57` rechaza con 400 cualquier otro nombre |
   | README §3.6 y sección `Pruebas` | Dicen que la máquina se entrega "sin pruebas, llegan en la semana 8" y "3 a 5 estados, como exige la práctica" |

2. **Las contradicciones que había que resolver antes de tocar código:**

   - `current-iteration.md:95` pide un "ADR 010" para el consumo de la concesión, pero `docs/adr/010-role-as-text-in-responses.md` ya existe: el nuevo es el **011**.
   - La regla de `AGENTS.md` decía que Business solo depende de `Core.Contracts`, y con eso no se pueden declarar rutas (viven en `AccessControl.Api`) ni saber quién llama (`Authenticate` vive en `AccessControl.Application`). `Permissions` ya resuelve lo mismo dependiendo de AccessControl, y su `.csproj` deja anotado que mover `IAuthenticate` a Contracts quedó como trabajo futuro.
   - `AccessLevel` no tiene `Standard`, así que no había forma de rechazar a un Administrador en las operaciones de "quien cobra" sin romper el punto único de RF-CA-05.
   - `Paid` deja de ser terminal en la máquina nueva (`Paid -> Cancelled` con permiso), y el README §3.6 y `maquina-de-estados.md:27-31` afirman lo contrario.
   - `global.json` estaba en `10.0.400` y `AGENTS.md` decía `10.0.302`.

3. **El plan de siete fases**, cada una con rama, commits atómicos, PR, comandos de verificación y lo que no incluye.

4. **Un hallazgo que no estaba en ningún documento:** la prueba entregada `TodaOperacionDeLaTablaTieneUnaRuta` (`PruebasDelPuntoUnicoDeAcceso.cs:96`) exige que toda operación declarada en `RoleRequirements` tenga ruta, pero solo construye las rutas con `MapAccessControl()` y `MapPermissions()`. En cuanto el negocio declare sus operaciones, la prueba se pone roja aunque el código esté bien. Hay que añadir `MapBusiness()` y los tipos de sus casos de uso a esa prueba; no se debilita, se amplía.

**Dónde se equivocó el agente.**

- **Recomendó permitir `Draft -> Cancelled` como solución a los borradores abandonados.** Razonó así: si el borrador se persiste y no hay forma de anularlo, las facturas empezadas y abandonadas quedan en `Draft` para siempre; por eso propuso añadir esa transición y pasar la tabla a 12 permitidas y 37 prohibidas. Era un parche: arreglaba el síntoma (la basura) y no la causa (persistir un borrador que solo existe mientras el cajero escribe). Lo detecté al revisar mi propia recomendación, no con una prueba, y lo corregí antes de escribir código. La decisión correcta la tomó Franklin: **el borrador vive solo en el cliente y no se persiste**, con lo que no hay nada que abandonar ni que anular y la tabla se queda en 11 y 38. Queda anotado porque el número que yo había propuesto primero era el equivocado.

**Qué verifiqué.**

- `dotnet --version` devolvió `10.0.403`; con `global.json` en `10.0.400` y `rollForward: latestFeature` arranca sin error.
- `dotnet test pirry-ledger.slnx` sobre `develop`: **96 de AccessControl, 26 de Permissions y 6 de Notifications, 128 en total, 0 con error**. El README decía 96/26/6 y la bitácora de la Práctica 2 decía 96/24/6: el número real es 26, y desde ahora la bitácora anterior queda como documento histórico, no como fuente de conteo.
- `git status --short` mostró `AGENTS.md`, `docs/current-iteration.md` y `global.json` modificados y `docs/assignments/practica-3.md` sin seguimiento.
- `git check-ignore -v docs/assignments/practica-3.md` no devolvió nada: el brief no está ignorado y se puede versionar.
- Los sitios del código se leyeron línea por línea (`InvoiceStateMachine.cs`, `Invoice.cs`, `PermissionCatalog.cs`, `CreatePermissionRequest.cs`, `IPermissionAuthority.cs`, `RoleRequirements.cs`, `PruebasDelPuntoUnicoDeAcceso.cs`), no se dedujeron del plan.

**Lo que decidí yo.**

1. La dependencia de Business: `Business.Application` solo contra `Core.Contracts` y `Business.Api` enchufado al filtro de `AccessControl.Api`, igual que `Permissions.Api`. Se enmienda `AGENTS.md` en esta misma fase.
2. `AccessLevel.Standard` para las operaciones de quien cobra, con prueba de que un Administrador recibe 403.
3. La operación del negocio pide el permiso en nombre del usuario; no se reutiliza el endpoint de solicitudes a mano.
4. El borrador vive solo en el cliente y no se persiste. `Draft` sigue siendo el estado inicial del agregado en memoria.
5. El Estándar solicita, ejecuta y consume la anulación; el Administrador solo aprueba. Con un solo Administrador sembrado, cualquier otra lectura chocaría con RF-GP-07.
6. El SDK queda en `10.0.400` y se corrige `AGENTS.md`.
7. `ViewSalesSummary` se queda: es la única operación estática de `AccessLevel.Permission` y mantiene viva la guía de la Práctica 2. Quitarla, si se decide, va en un PR aparte.
8. "Eliminar la factura" es pasarla a `Cancelled`, no borrar la fila.
9. Los cuatro defectos se plantan al final, cuando la suite esté verde; no se planifican en detalle todavía.
