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

---

### Entrada 2 — La máquina de estados de la factura y su suite (2026-10-29)

**Qué se pidió.** Ejecutar la fase 1 del plan: los siete estados, las once transiciones permitidas con sus condiciones, las treinta y ocho prohibidas y las dos terminales, con una prueba por fila, en la rama `feature/invoice-state-machine`.

**Qué devolví.**

1. **El proyecto de pruebas** `Tests/PirryLedger.Business.Tests`, con solo el dominio como referencia, registrado en `pirry-ledger.slnx`.
2. **La suite**, en `MaquinaDeEstados/`:
   - once pruebas, una por fila permitida, cada una con un contexto que satisface solo la condición de esa fila;
   - una prueba por parámetros con las treinta y ocho filas prohibidas, que además comprueba que el estado no cambió;
   - una prueba por estado terminal (`AnuladaNoTieneSalidas`, `NoEntregadaNoTieneSalidas`);
   - ocho pruebas de condiciones (sin ítems, pago incompleto, transferencia, deuda sin nombre, no entregada sin delivery, abono parcial);
   - dos pruebas de la tabla (`LaFacturaDeclaraSieteEstados`, `LaTablaPermiteExactamenteOnceDeLosCuarentaYNuevePares`) y una de independencia (`LaMaquinaDeLaFacturaNoDependeDeLaDePermisos`).
3. **La implementación mínima:** `InvoiceState` con siete estados, `InvoiceTransitionContext` con los hechos de la tentativa, `InvoiceStateMachine` con la tabla única y una comprobación por fila aislada en su método, `Invoice.TransitionTo(estado, contexto)`, y dos excepciones (`InvalidInvoiceTransitionException` para un par prohibido, `InvoiceConditionNotMetException` para una condición incumplida).
4. **La documentación:** `docs/maquina-de-estados.md` con las dos tablas y la nota de que `Draft` no se persiste; RF-NEG-02, 06 y 09 en `business-module.md` con el texto del profesor; README §3.6 con el comando y la línea que hace fallar la suite.

**Dónde se equivocó el agente.**

- **Duplicó una funcionalidad en la lista de RF-NEG-02.** En la fase 0 renombré el punto 1 ("emitir la factura") y dejé el punto 2 con el nombre viejo ("emitir la factura, marcando delivery o no"): la misma funcionalidad dos veces. Lo detecté al escribir `business-module.md`, no con una prueba, y lo corregí en el mismo commit de requisitos: ahora la lista tiene ocho funcionalidades separables, que es más de las cinco que exige RF-NEG-02.
- **La excepción de una condición informaba el par equivocado.** Al escribir las guardas, `EnsurePaymentsCoverTheTotal` construía la excepción con `(Issued, Paid)` fijo, así que el rechazo de `InDebt -> Paid` habría dicho "Issued -> Paid". Lo vi al releer el archivo antes de compilar y pasé `from` y `to` a cada guarda. No llegó a ejecutarse con el error.

**Qué verifiqué.**

- **Rojo primero:** `dotnet test Tests\PirryLedger.Business.Tests\PirryLedger.Business.Tests.csproj` falló con `error CS0246: El nombre del tipo o del espacio de nombres 'InvoiceTransitionContext' no se encontró`. Era el rojo correcto: la API que describen las pruebas no existía.
- **Verde:** `dotnet test pirry-ledger.slnx` → **190 superadas, 0 con error**: Business 62, AccessControl 96, Permissions 26, Notifications 6.
- **La suite puede fallar, y por la razón correcta.** Quité a mano la guarda de `Draft -> Issued` (el defecto 3 del enunciado): `EmitirSinItemsSeRechazaYNoCambiaElEstado` se puso roja y las otras 61 siguieron verdes. Restauré la línea. Después añadí a mano `Issued -> Draft` (el defecto 1): fallaron la fila concreta y `LaTablaPermiteExactamenteOnceDeLosCuarentaYNuevePares`. Restauré la tabla y volvió a verde.
- **Un solo punto de decisión:** ningún caso de uso ni endpoint valida estados por su cuenta; la lista `AllowedTransitions` es la única que decide, y las condiciones están dentro de la misma máquina.

**Lo que decidí yo.**

1. **Una sola verificación de "al menos un ítem", y en la máquina.** El endpoint no la repite: si estuviera duplicada, quitar la guarda de la máquina (defecto 3) dejaría la suite verde y el defecto no se podría plantar.
2. **La máquina recibe los hechos, no la entidad.** `InvoiceTransitionContext` lleva cantidad de ítems, delivery, nombre del deudor, total, pagos acumulados y si hay transferencia. Así la máquina se prueba sin agregado, sin base de datos y sin reloj (RD-12), y en la fase 2 el caso de uso calcula el contexto a partir de los ítems y los pagos.
3. **El permiso de anulación no es una condición de la tabla.** Depende de la factura concreta y vive en una sola línea del caso de uso, que es justo la que elimina el defecto 4. La máquina no conoce la pieza de permisos (RF-NEG-09).
4. **Una prueba por fila, no una matriz.** La matriz de 49 pares no aporta nada cuando cada fila tiene su prueba; sí se conserva el conteo (11 de 49) como red contra una transición añadida por accidente.

**Desvío del plan.** El plan decía dos commits (suite en rojo, implementación en verde). Los hice en uno solo: el proyecto de pruebas no compila sin la implementación, y un commit que no compila rompe el historial para cualquiera que lo revise o lo biseque. El rojo queda registrado aquí con su salida real.

**Qué tiene que verificar Franklin.**

```powershell
dotnet test pirry-ledger.slnx
```

Debe decir 190 superadas y 0 con error. Después, quitar `EnsureHasAtLeastOneItem(from, to, context);` de `InvoiceStateMachine` y repetir: una sola prueba en rojo, `EmitirSinItemsSeRechazaYNoCambiaElEstado`.

---

### Entrada 3 — Las reglas de la factura, su persistencia y los ocho commits de la auditoría (2026-10-29)

**Qué se pidió.** Ejecutar la fase 2 del plan en la rama `feature/invoice-rules`: las reglas de la factura y su persistencia (paso 2 de `current-iteration.md`), con pruebas primero, commits atómicos y la documentación en el mismo pull request. El pull request ya estaba abierto cuando empezó la auditoría, así que los arreglos se agregaron como commits nuevos, sin reescribir el historial.

**Qué devolví.**

1. **Los seis commits originales**, uno por comportamiento:
   - `601e9b9` ítems y regla de emisión;
   - `875fff9` cobro en efectivo o por transferencia;
   - `0ce0961` deuda y abonos;
   - `0677053` factura de delivery no entregada;
   - `6fcc348` edición de los ítems de una factura emitida;
   - `e40768b` persistencia en tablas propias y migración.
2. **La auditoría del propio trabajo, que encontró cinco reglas mal puestas.** Cada una se arregló en su commit, con la prueba escrita antes y mostrada en rojo:
   - `8ac1f81` `RejectTransfer` se podía ejecutar desde cualquier estado; ahora solo desde `PendingVerification`;
   - `2935eeb` `RegisterDebtPayment` se podía ejecutar desde cualquier estado y agregaba el pago antes de transicionar; ahora exige `InDebt` y transiciona primero;
   - `c63eb90` `ConfirmTransfer` se podía ejecutar desde cualquier estado; ahora solo desde `PendingVerification`;
   - `67c8fb5` `EditItems` aceptaba una lista vacía y dejaba la factura sin ítems; ahora la rechaza;
   - `9ef9c30` el nombre del deudor no tenía límite y la columna es `varchar(120)`; ahora se valida antes de escribir.
3. **La prueba de persistencia que faltaba** (`2936b66`): ida y vuelta con EF InMemory y comprobación del esquema de Npgsql sin abrir conexión. RD-09 no estaba probado, solo implementado.
4. **La limpieza** (`ba85ad0`): parámetros que nadie usaba (`LeaveAsDebt` ya no recibe fecha ni usuario, `ConfirmTransfer`, `RejectTransfer` y `MarkAsNotDelivered` pierden su fecha), `PaymentMethod.None` para el abono de deuda y comentarios en español.
5. **El nombre de una prueba** (`df30bd2`): `UnaFacturaEnVerificacionPendienteNoSeEdita` en `PruebasDeNoEntrega` probaba `MarkAsNotDelivered`, no una edición. Ahora se llama `UnaFacturaEnVerificacionPendienteNoSeMarcaComoNoEntregada`.
6. **La documentación:** README §3.10 con la tabla de criterios y la prueba de cada uno, el cuarto comando de migración en el paso 6, las cuatro tablas `bil_` en el esquema, y las decisiones 6, 7 y 9 de `current-iteration.md` cerradas con las reglas que quedaron.

**Dónde se equivocó el agente.**

- **Confundió "el agregado lo permite" con "la regla lo permite".** `RejectTransfer`, `RegisterDebtPayment` y `ConfirmTransfer` delegaban en la máquina de estados, que rechaza el par prohibido, pero no comprobaban el estado de partida que la operación necesita. El resultado era un error de transición en vez de un rechazo de la regla, y en el abono el pago se agregaba antes de saber si la transición valía. Las tres pruebas que lo demuestran se escribieron después de la auditoría, no antes: la suite original estaba verde porque probaba el camino feliz de cada método.
- **Dejó `RD-09` sin prueba.** La persistencia estaba implementada y la migración generada, pero ninguna prueba guardaba y recuperaba una factura. Lo detecté al revisar el criterio de aceptación punto por punto, no al ejecutar la suite.
- **Escribió un nombre de prueba que describe otra cosa.** La prueba de no entrega se llamaba "no se edita". Pasó desapercibido porque el nombre era plausible y la prueba pasaba.
- **Dejó parámetros muertos en la API del agregado** (`now`, `callerId`, `registeredAtUtc` sin uso). No rompen nada, pero mienten sobre lo que el método necesita.

**Qué verifiqué.**

- **Rojo primero, en cada arreglo.** Por ejemplo, `AbonarUnaFacturaQueNoEstaEnDeudaSeRechaza` falló con `Expected: typeof(InvoiceRuleViolationException)` y sin excepción antes de la guarda; `ConfirmarUnaTransferenciaFueraDeVerificacionPendienteSeRechaza` falló con `Actual: typeof(InvalidInvoiceTransitionException)`. Era el rojo correcto en los dos casos: la regla no existía.
- **Verde:** `dotnet test pirry-ledger.slnx` → **232 superadas, 0 con error**: Business 104, AccessControl 96, Permissions 26, Notifications 6. `dotnet build pirry-ledger.slnx` → 0 advertencias, 0 errores.
- **La migración se aplica de verdad.** `dotnet ef migrations list --project Src\Business\PirryLedger.Business\PirryLedger.Business.Infrastructure` mostró `20261029150722_InitialBusinessSchema (Pending)`; después de `dotnet ef database update` sobre la misma pieza, la migración se aplicó (`Applying migration '20261029150722_InitialBusinessSchema'. Done.`) y la lista volvió a mostrarla sin `(Pending)`. La base de desarrollo de mi máquina quedó con las cuatro tablas `bil_`.
- **La prueba de persistencia usa la base real solo para leer el modelo.** `ElEsquemaUsaNombresEnSnakeCase` construye el modelo de Npgsql y comprueba nombres y `numeric(12,2)` sin conectarse; `UnaFacturaGuardadaSeRecuperaConSusItemsYPagosYSuHistorial` usa EF InMemory. Ninguna necesita PostgreSQL ni SMTP (RD-12).
- **La suite puede fallar, y por la razón correcta.** Quitar `if (State != InvoiceState.InDebt)` de `RegisterDebtPayment` pone en rojo `AbonarUnaFacturaQueNoEstaEnDeudaSeRechaza` y deja las otras 103 verdes; quitar `if (items.Count < 1)` de `EditItems` pone en rojo `EditarSinItemsSeRechazaYNoCambiaLaFactura`.

**Lo que decidí yo.**

1. **Editar a cero ítems se rechaza.** Una factura sin ítems no es una factura: si el cliente ya no quiere nada, se anula y se emite otra.
2. **Una factura cobrada de menos se anula y se emite de nuevo.** No se edita una factura pagada ni se le agrega un pago negativo.
3. **Una transferencia rechazada no cuenta.** Queda `Rejected`, el monto pagado baja y la factura pasa a `InDebt` con el nombre de quien debe.
4. **El abono de deuda no tiene método de pago.** `PaymentMethod.None` expresa "no aplica"; no se inventa un método para llenar la columna.
5. **En delivery solo se guarda el monto.** Quien cobra no sabe cuánto entregó ni cuánto devolvió el courier, así que el recibido y el cambio se rechazan en vez de inventarse.
6. **El nombre del deudor se valida contra la columna.** 120 caracteres, el mismo límite de `debtor_name`, para que la base no tenga que truncar ni fallar.

**Desvío del plan.** El plan decía dos commits por comportamiento (prueba en rojo e implementación). En la práctica cada comportamiento quedó en un commit con la prueba y el código juntos, porque el proyecto de pruebas no compila sin la API que describe; el rojo queda registrado aquí con su salida real. La persistencia llegó en un commit y su prueba en otro, después de la auditoría.

**Qué tiene que verificar Franklin.**

```powershell
dotnet test pirry-ledger.slnx
```

Debe decir 232 superadas y 0 con error. Para ver solo las reglas de esta fase:

```powershell
dotnet test Tests\PirryLedger.Business.Tests\PirryLedger.Business.Tests.csproj
```

Debe decir 104. Después, quitar `if (State != InvoiceState.InDebt)` de `RegisterDebtPayment` en `Invoice.cs` y repetir: una sola prueba en rojo, `AbonarUnaFacturaQueNoEstaEnDeudaSeRechaza`.

Si la base no tiene todavía las tablas de facturas, el cuarto comando del paso 6 del README las crea. En mi máquina ya están aplicadas.


---

### Entrada 4 — Consumir una concesión en Permissions (RF-GP-05, RF-NEG-06) (2026-10-30)

**Qué se pidió.** Ejecutar la fase 3 del plan en la rama `feature/permission-consumption`: una operación nueva en el contrato de Permissions que borre la concesión, con pruebas en la suite de Permissions, sin migración y sin tocar Business ni endpoints. Antes de escribir código: presentar el ADR 011 con alternativas y esperar autorización. Franklin eligió la opción B (duplicar el prefijo en el dominio y en los contratos, atados por una prueba) y autorizó el ADR.

**Qué devolví.**

1. **Commit `Add the invoice cancellation permission name (RF-GP-01, RF-NEG-06)`**: `PermissionNames` en `Core.Contracts` (constructor y validador con GUID canónico) y `PermissionCatalog.IsKnown` extendido para aceptar `CancelInvoice:<guid>`. 10 pruebas de catálogo.
2. **Commit `Consume a permission grant in Permissions (RF-GP-05, RF-NEG-06)`**: `ConsumeAsync` en `IPermissionAuthority` y en `IPermissionGrantRepository`; `PermissionGrantRepository.ConsumeAsync` busca, borra y captura `DbUpdateConcurrencyException`; `EfPermissionAuthority` delega; dobles actualizados (`ConsumeAsync` en el doble de Permissions y en el de AccessControl). 7 pruebas de consumo.
3. **Commit `Document the consumption of a permission grant (ADR 011)`**: ADR 011, índice del ADR, README §3.11, `current-iteration.md` con la decisión 10 cerrada y la corrección "ADR 010" → "ADR 011".
4. **Commit `Record phase 3 in the logbook (RF-GP-05)`**: esta entrada.

**Dónde se equivocó el agente.**

- **La primera prueba del catálogo usó un GUID solo de dígitos** (`11111111-…`), así que "mayúsculas" era el mismo texto y la prueba pasaba cuando debía fallar. Lo detecté al ver el rojo malo y cambié el GUID a `abcdef01-2345-6789-abcd-ef0123456789`. La prueba no se debilitó: se corrigió el dato.
- **La carrera de dos consumos simultáneos** (`DbUpdateConcurrencyException`) no se puede reproducir con EF InMemory, así que quedó cubierta por el código y por el razonamiento, no por una prueba. Se probará contra PostgreSQL en la fase 4. Lo dejé anotado en el ADR y en el README.
- **El plan inicial decía "ADR 010"**, pero ese número ya estaba ocupado por `010-role-as-text-in-responses.md`. Lo detecté en la fase 0 y el consumo quedó como ADR 011. Corregí `current-iteration.md` en esta fase.

**Qué verifiqué.**

- **Rojo primero.** El primer intento de compilar las pruebas de catálogo falló con `CS0103: El nombre 'PermissionNames' no existe` y `CS0117: 'PermissionCatalog' no contiene una definición para 'IsKnown'`. El primer intento de compilar las pruebas de consumo falló con `CS1061: 'PermissionGrantRepository' no contiene una definición para 'ConsumeAsync'`. Los dos rojos eran los correctos: la API que describen las pruebas no existía.
- **Verde:** `dotnet test pirry-ledger.slnx` → **249 superadas, 0 con error**: Business 104, AccessControl 96, Permissions 43, Notifications 6. `dotnet build pirry-ledger.slnx` → 0 advertencias, 0 errores.
- **La suite puede fallar, y por la razón correcta.** Comentar `_context.PermissionGrants.Remove(grant)` en `PermissionGrantRepository.ConsumeAsync` pone en rojo `ConsumirUnaConcesionLaBorraYDevuelveTrue` (la fila sigue en la base) y deja las otras 42 verdes. Quitar la guarda `if (userId == Guid.Empty …)` de `EfPermissionAuthority.ConsumeAsync` pone en rojo `LaAutoridadNoConsumeConUsuarioVacio`.
- **Una sola migración en Permissions.** `dotnet ef migrations list` sigue mostrando solo `20261021024258_InitialSchema`: consumir es un `DELETE`, no hay esquema nuevo.
- **No se tocó Business, ni endpoints, ni el Host.** El diff de la fase es solo contratos, dominio, infraestructura de Permissions y pruebas.

**Lo que decidí yo.**

1. **Opción B para el nombre del permiso.** El prefijo vive duplicado en `PermissionCatalog` (dominio) y en `PermissionNames` (contratos), atados por `PruebasDelCatalogoDePermisos`. Evita la referencia `Permissions.Domain -> Core.Contracts`, que hoy no existe en ningún dominio del repositorio; el coste es un literal y un validador duplicados.
2. **GUID canónico (formato `D` y minúsculas).** Mayúsculas, llaves o espacios se rechazan, para que una factura tenga un solo nombre de permiso y no se pida dos veces con dos textos distintos.
3. **`ConsumeAsync` devuelve `true/false`, no lanza.** "No tenía el permiso" es un resultado normal; el mensaje al usuario lo decide el caso de uso de la anulación.
4. **Solo borra el par del llamante.** La concesión de otro usuario para la misma factura es su propio permiso.
5. **Orden B en la anulación.** Transición en memoria → consumo → guardar. Si el consumo devuelve `false`, se rechaza antes de guardar. Si el guardado falla después del consumo, la concesión se pierde; se prefiere a dejar una concesión viva sobre una factura ya anulada.
6. **Sin migración.** La tabla y el índice único ya existen (ADR 008).

**Desvío del plan.** El plan decía un commit por comportamiento (prueba en rojo, implementación en verde). En la práctica la prueba y el código de cada comportamiento quedaron en el mismo commit, porque el proyecto de pruebas no compila sin la API que describe; el rojo queda registrado aquí con su salida real, igual que en las fases 1 y 2.

**Qué tiene que verificar Franklin.**

```powershell
dotnet test pirry-ledger.slnx
```

Debe decir 249 superadas y 0 con error. Para ver solo Permissions:

```powershell
dotnet test Tests\PirryLedger.Core.Permissions.Tests\PirryLedger.Core.Permissions.Tests.csproj
```

Debe decir 43. Después, comentar `_context.PermissionGrants.Remove(grant)` en `PermissionGrantRepository.ConsumeAsync` y repetir: una sola prueba en rojo, `ConsumirUnaConcesionLaBorraYDevuelveTrue`.

Para abrir el pull request: rama `feature/permission-consumption`, base `develop`, título sugerido `Consume a permission grant (RF-GP-05, RF-NEG-06)`. Franklin hace el push, el PR y el merge.




