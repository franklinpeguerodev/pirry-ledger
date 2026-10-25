# Bitácora de la Práctica 2 — Una suite que detecta defectos

- **Práctica:** 2 — Gestión de permisos (Programación III, Bloque 3, Semana 7).
- **Repositorio:** `franklinpeguerodev/pirry-ledger`.
- **Herramienta:** agente de OpenCode en mi máquina (Windows, PowerShell 5.1, `dotnet` SDK 10.0.302).
- **Rama de trabajo:** `docs/practica-2-scope`, desde `develop`.

La regla es la misma de las bitácoras anteriores: **lo que devuelve el agente es un borrador, no una verdad**. Nada se sube sin que yo lo lea y lo ejecute en mi máquina.

---

### Entrada 1 — Auditoría de `docs/` contra el brief y plan por fases (2026-10-25)

**Qué se pidió.** Antes de escribir una línea de la Práctica 2, revisar si algo dentro de `docs/` genera un conflicto con el brief nuevo; verificar después si la suite actual detecta de verdad los seis defectos publicados por el profesor; y proponer un plan dividido en fases donde cada fase tenga su rama, sus commits atómicos, su pull request abierto por el agente, la autorización mía para mergearlo y una petición explícita de permiso antes de pasar a la fase siguiente.

**Qué devolví.**

1. **Seis conflictos en `docs/`**, con el archivo y la línea:

   - `docs/current-iteration.md` seguía declarando la Asignación 2 (tag `asignacion-2`, bitácora de la Asignación 2, RF-GP como trabajo nuevo). Es el límite de alcance que lee el agente: bloqueante.
   - `docs/requirements/README.md:13` marcaba `permissions.md` como "Current (Asignación 2)".
   - La bitácora: el brief de la Práctica 2 no la menciona, pero `AGENTS.md` la exige por pull request y fija su ruta en `current-iteration.md`.
   - `docs/assignments/practica-2.md` §2 contenía una instrucción dirigida al agente metida dentro de una fuente de verdad. La corregí yo mismo antes de que el agente tocara nada.
   - El `README.md` no tenía sección `Defectos` (cero coincidencias de "Defecto") y su línea 3 seguía diciendo "Práctica 1 — Control de acceso"; los conteos de pruebas (96 / 6 / 24) iban a quedar falsos.
   - `docs/maquina-de-estados.md` documentaba solo la máquina de `Invoice`, con el título diciéndolo.

2. **El mapeo de los seis defectos contra el código real**, no contra el plan:

   | Defecto | Sitio | ¿La suite lo detecta hoy? |
   |---|---|---|
   | 1 | `ApprovePermissionRequest.cs:62` | sí |
   | 2 | `PermissionRequestStateMachine.cs:29` | sí |
   | 3 | `ApprovePermissionRequest.cs:71` y `RejectPermissionRequest.cs:62` | sí |
   | 4 | `PermissionRequestRepository.cs:43` | **no** |
   | 5 | `PermissionRequestStateMachine.cs:29` | **no** |
   | 6 | `PermissionRequest.cs:99` | sí |

   El defecto 4 no se detecta porque el filtro `request.RequesterId == requesterId` vive en el repositorio EF y ningún test lo instancia: los ocho archivos de pruebas usan `FakePermissionRequestRepository`. El defecto 5, porque nada afirma que `Approved -> Pending` sea falsa.

3. **Un borrador completo de `docs/current-iteration.md`** para que yo lo revisara, con el alcance, lo que queda fuera, las decisiones mías numeradas y la definición de terminado.

4. **El plan de siete fases** (0 a 6) con rama, commits atómicos, verificación y título de pull request en cada una.

**Dónde se equivocó el agente.**

- **Dijo que documentar la máquina de permisos costaba "unas 15 líneas".** Al leer `docs/maquina-de-estados.md` para escribirlo, resultó que el título del archivo es "Máquina de estados de la factura" y tiene un único `## Transiciones prohibidas y terminales`. Meter una segunda máquina obliga a re-nivelar lo que ya existe: son unas 30 líneas tocadas, casi todas encabezados. Lo detectó el propio agente al abrir el archivo, no una prueba, y lo corrigió antes de escribir nada: el plan final dice el coste real. Queda anotado porque el número que yo había leído primero era el falso.
- **Presentó la Fase 0 como trabajo pendiente** (mergear el pull request #50). Al comprobarlo con la API, el #50 ya estaba mergeado (`3da284c`), y `main`, `develop` y sus remotos apuntaban los cuatro al mismo commit. El agente lo verificó y reescribió la fase como cerrada. La lección para mí: cuando el agente propone un paso de coordinación, lo primero es comprobar el estado real, no el que el plan supone.

**Qué verifiqué.**

- `git diff --staged` salió vacío y `git status --short` mostró los dos briefs como `??`: **no estaban en el stage**, aunque yo creía que sí. Se agregaron después, ya en la rama.
- `git check-ignore -v docs/assignments/practica-2.md` y el mismo comando sobre `practica-2-defectos.md` no devolvieron nada: no están ignorados y se pueden versionar.
- `git log` de los cuatro refs (`main`, `origin/main`, `develop`, `origin/develop`) devolvió `3da284c` en los cuatro, y `git rev-list --left-right --count main...origin/main` devolvió `0 0`.
- Los sitios de los seis defectos se leyeron en el código, línea por línea, no se dedujeron del anexo.
- `Select-String` sobre `Tests/PirryLedger.Core.Permissions.Tests` confirmó que las trece menciones a `PermissionRequestRepository` o `ListByRequesterAsync` son todas al doble de pruebas o a su uso; ninguna instancia el repositorio real.
- La suite actual son 126 pruebas: 96 de AccessControl, 24 de Permissions y 6 de Notifications.

**Lo que decidí yo.**

1. `current-iteration.md` lo redacta el agente como borrador y lo reviso yo.
2. Bitácora nueva: `docs/bitacora-practica-2.md`, para no mezclar dos entregas calificadas.
3. Añadir la máquina de estados de la solicitud a `docs/maquina-de-estados.md` (0 puntos, pero cierra el hueco).
4. Cerrar los dos huecos de la suite antes de etiquetar: prueba del repositorio real con EF InMemory y matriz 4×4 de transiciones.
5. Encabezados de tabla en español y nombres de estado en inglés, porque son identificadores del código.

**Qué contiene este pull request.** Tres commits atómicos, ninguno de código:

1. `f907574 Add the Practice 2 briefs and their defect annex` — los dos documentos del profesor, tal como los subí.
2. `225adfe Declare Practice 2 as the current iteration` — el reemplazo de `docs/current-iteration.md` y el estado de `permissions.md` en el índice de requisitos.
3. Esta entrada de la bitácora.

**Qué NO incluye.** Ni una línea de código, ni la máquina de estados (fase 2), ni las pruebas nuevas (fase 3), ni la sección `Defectos` del README (fase 4), ni etiquetas (fase 6).

### Entrada 2 — La máquina de estados de la solicitud en `maquina-de-estados.md` (2026-10-25)

**Qué se pidió.** Documentar la máquina de estados de la solicitud de permiso en `docs/maquina-de-estados.md`, que hasta ahora solo describía la de la factura. En su propio commit, sin tocar código, con los encabezados de tabla en español y los nombres de estado en inglés porque son identificadores del código (decisión 7 de `docs/current-iteration.md`).

**Qué devolví.** Rama `docs/permission-state-machine` desde `develop` (`3b77928`), commit `254aa0d Document the permission request state machine`. El documento pasó de 24 a 75 líneas:

- El título pasa de "Máquina de estados de la factura" a "Máquinas de estados", con una introducción que dice que hay dos y que cada una resuelve sus transiciones en un único componente (RD-04).
- La máquina de la factura queda como `## Máquina de estados de la factura`: mismo texto y misma tabla, con los encabezados traducidos (`from | to | who executes | condition` → `desde | hacia | quién la ejecuta | condición`) y su sección de prohibidas bajando de `##` a `###`.
- Se añade `## Máquina de estados de la solicitud de permiso` con la tabla de tres transiciones, las prohibidas, la nota de que RF-GP-07 no es una transición y una sección de independencia de la otra máquina (RF-NEG-09).
- Un cambio más, pequeño y deliberado: la frase "En esta iteración solo se entrega su estructura" pasó a "Su estructura se entregó con la Práctica 1". "Esta iteración" ya es la Práctica 2, donde no se entrega nada de la factura, así que la frase había quedado falsa. Es la única línea de la sección de la factura que no es un encabezado.

**Dónde se equivocó el agente.** Commiteó directamente sobre `develop`, saltándose la regla de `AGENTS.md` de que nunca se commitea ahí y la de una rama por fase. Lo detectó él mismo al comprobar `git diff --stat develop..HEAD`, que salió vacío: ese diff solo puede estar vacío si `HEAD` **es** `develop`. Corrección, sin reescribir nada: `git branch docs/permission-state-machine 254aa0d`, checkout de la rama y `git branch -f develop origin/develop`. Quedó verificado con `git rev-parse`: `develop` y `origin/develop` otra vez en `3b77928`, y `git branch --contains 254aa0d` listando solo la rama. Lo anoto porque el commit existía y estaba bien; lo que estaba mal era dónde vivía.

**Qué verifiqué.**

- `PermissionRequestStateMachine.AllowedTransitions` declara exactamente tres pares, y son las tres filas de la tabla nueva. Contado con `Select-String`: 3 coincidencias.
- Los cuatro estados dan dieciséis pares; tres permitidos y trece prohibidos. El documento dice eso y lo ejemplifica.
- `InvoiceStateMachine` declara exactamente cuatro pares, que son las cuatro filas de la tabla de la factura. Contado igual: 4 coincidencias.
- La tabla nueva coincide fila por fila con `docs/requirements/permissions.md` en condición y en quién ejecuta cada transición.
- RF-GP-07 no está en la tabla porque no la comprueba la máquina: `ApprovePermissionRequest.cs:71` y `RejectPermissionRequest.cs:62` comparan `request.RequesterId == caller.Id`. El documento lo dice así.
- Estructura de encabezados: un `#`, dos `##` y tres `###`, sin saltos de nivel.
- `git diff --stat develop..HEAD` tras el arreglo de la rama: solo `docs/maquina-de-estados.md`. Cero cambios en `Src/` y `Tests/`.

**Qué NO incluye.** No toca código, ni pruebas, ni el `README.md`, ni las etiquetas. La máquina de la factura no cambia de contenido salvo encabezados y la frase corregida.

### Entrada 3 — Cerrar los dos huecos que dejaban dos defectos sin detectar (2026-10-25)

**Qué se pidió.** Antes de etiquetar, cerrar los dos huecos que la auditoría encontró: el defecto 4 (el filtro por solicitante del repositorio real, que ningún test ejercitaba) y el defecto 5 (nada afirmaba que `Approved -> Pending` fuera falsa). Cada prueba nueva tenía que verse fallar con su defecto plantado y pasar sin él, sin commitear el defecto.

**Qué devolví.** Rama `test/permissions-suite-gaps` desde `develop` (`3be764a`), tres commits:

1. `2147bed Test the real request repository against EF InMemory (RF-GP-02)` — archivo nuevo `Tests/PirryLedger.Core.Permissions.Tests/Persistencia/PruebasDeListadoPorSolicitante.cs` (53 líneas). Siembra dos solicitudes de dos solicitantes distintos en un `PermissionsDbContext` con `UseInMemoryDatabase`, llama al `PermissionRequestRepository` **real** con `ListByRequesterAsync(A)` y afirma que vuelve una sola y que su `RequesterId` es el de A. Es el mismo patrón que ya usaba `PruebasDeApprovePermissionGrantUnico` para las concesiones.
2. `ee41f3e Enumerate every state transition pair (RF-GP-06)` — una prueba nueva en `PruebasDeLaMaquinaDeEstados`: `SoloLasTresTransicionesDeLaTablaSonValidas`. Recorre los dieciséis pares de los cuatro estados y afirma que `CanTransition` responde `true` solo en los tres permitidos. Una prueba, un comportamiento: "el conjunto permitido es exactamente este".
3. `df6ef34 Update the suite counts in the README` — Permissions pasa de 24 a 26 pruebas, y la frase "Todas usan dobles en memoria" pasa a "Usan dobles en memoria, un contexto EF Core en memoria y un reloj falso", porque con la prueba del repositorio ya no era verdad.

**Qué verifiqué.** El rojo y el verde de cada prueba, con el defecto plantado a mano y revertido antes de commitear:

- Defecto 4 (`==` → `!=` en `PermissionRequestRepository.cs:43`): `ListarPorSolicitanteDevuelveSoloLasSolicitudesDeEseSolicitante` falla (`Con error: 1`). Revertido con `git checkout --` sobre el archivo; `git status` solo mostró el archivo de pruebas nuevo, sin trackear.
- Defecto 2 (añadir `Rejected -> Applied`): `SoloLasTresTransicionesDeLaTablaSonValidas` falla con `La transicion Rejected -> Applied deberia ser prohibida.`
- Defecto 5 (añadir `Approved -> Pending`): la misma prueba falla con `La transicion Approved -> Pending deberia ser prohibida.`
- Sin defectos: `dotnet test Tests/PirryLedger.Core.Permissions.Tests` da **26 superadas, 0 con error** (eran 24).
- Suite completa: `dotnet test pirry-ledger.slnx` da **128 superadas, 0 con error** (96 AccessControl + 26 Permissions + 6 Notifications).

Los tres defectos se plantaron uno por uno y nunca se commitearon: el diff que los contenía se revirtió antes de cada commit, y `git status` quedó limpio salvo el archivo nuevo de la prueba.

**Dónde se equivocó el agente.** En esta fase no hubo un error que corregir. El caso registrado hasta ahora sigue siendo el de la Entrada 2 (el commit que nació en `develop` en vez de en la rama). Lo anoto tal cual: no voy a inventar un fallo para llenar el espacio.

**Qué NO incluye.** La sección `Defectos` del `README.md` y el encabezado de su línea 3 (fase 4), la promoción a `main` (fase 5) y las etiquetas (fase 6). Tampoco se tocó ningún test existente: las 24 pruebas anteriores siguen intactas y verdes.

### Entrada 4 — El mapeo real de los seis defectos y la sección `Defectos` del README (2026-10-25)

**Qué se pidió.** Antes de escribir la sección `Defectos`, comprobar el mapeo de verdad y no sobre el papel: plantar los seis defectos de la variante A uno por uno sobre `develop` (ya con la suite ampliada), correr la suite, anotar qué prueba exacta falla con cada uno y revertir. Sin commits. Después, escribir en el `README.md` una sección `Defectos` con una línea por defecto que diga el nombre de la prueba que lo detecta y el comportamiento que esa prueba comprueba, y corregir el encabezado, que seguía diciendo "Práctica 1".

**Qué devolví.** Rama `docs/readme-defectos` desde `develop` (`b0ad5d4`), commit `5babc86 Document the six planted defects in the README`. Además del encabezado y la sección nueva, una frase en la presentación del repositorio: decía que existían la cola de email y el módulo Business, pero no mencionaba Permissions, que se entregó en la Asignación 2.

El mapeo que salió de plantar los seis defectos:

| Defecto | Cambio plantado | Pruebas que fallan |
|---|---|---|
| 1 | `ApprovePermissionRequest.cs:62`: la guarda lanza solo si el estado es `Rejected` | `AprobarUnaSolicitudYaAprobadaNoCambiaElEstado` |
| 2 | `PermissionRequestStateMachine.cs:28`: añade `Rejected -> Applied` | `RejectedNoTieneSalidas` y `SoloLasTresTransicionesDeLaTablaSonValidas` |
| 3 | `ApprovePermissionRequest.cs:71` y `RejectPermissionRequest.cs:62`: sin comparación de identidades | `UnAdministradorNoPuedeAprobarSuPropiaSolicitud`, `UnEstandarAscendidoNoPuedeAprobarSuPropiaSolicitud` y `UnAdministradorNoPuedeRechazarSuPropiaSolicitud` |
| 4 | `PermissionRequestRepository.cs:43`: `==` pasa a `!=` | `ListarPorSolicitanteDevuelveSoloLasSolicitudesDeEseSolicitante` |
| 5 | `PermissionRequestStateMachine.cs:28`: añade `Approved -> Pending` | `SoloLasTresTransicionesDeLaTablaSonValidas` |
| 6 | `PermissionRequest.cs:99`: elimina `RejectionReason = reason;` | `RechazarUnaSolicitudPendienteLaPasaARechazadaConMotivo` |

**Dónde se equivocó el agente.** Dos veces, y las dos se detectaron antes de commitear:

- **El defecto 1 lo planteó mal la primera vez.** Escribió `!= Rejected`, que es la sustitución literal de la condición, pero la guarda del código está escrita en negativo (`si el estado no es Pending, lanza`). Sustituir así la invierte dos veces: la aprobación deja de funcionar para una solicitud pendiente y solo funciona para una rechazada. Fallaban cinco pruebas, incluida `AprobarUnaSolicitudPendienteLaPasaAAplicadaYRegistraConcesion`, que no tiene nada que ver con el defecto. Lo delató el mensaje de la excepción: "Solo se pueden aprobar solicitudes en estado Pendiente" lanzado sobre una solicitud que estaba pendiente. El anexo pide "el cambio equivalente", y el equivalente es `== Rejected`: así falla exactamente una prueba, la que el defecto debe romper.
- **Citó la línea equivocada en el README.** Escribió `PermissionRequestStateMachine.cs:29` para los defectos 2 y 5, pero la línea 29 es el `};` que cierra el conjunto; la transición nueva se inserta después de la 28. Lo detectó leyendo la línea citada antes de commitear, y se corrigió a 28.

**Qué verifiqué.**

- Los seis defectos se plantaron y se revirtieron con `git checkout --`; después de cada uno, `git status` quedó limpio.
- Cada defecto hizo fallar al menos una prueba, con el mensaje esperado. Por ejemplo, el 2: `La transicion Rejected -> Applied deberia ser prohibida.` Y el 5: `La transicion Approved -> Pending deberia ser prohibida.`
- Con los seis revertidos, `dotnet test pirry-ledger.slnx` da **128 superadas, 0 con error** (96 AccessControl + 26 Permissions + 6 Notifications).
- Cada nombre de prueba citado en la sección `Defectos` existe de verdad: se buscó cada uno en el README y en `Tests/`, y las ocho coincidencias dan una definición por nombre.
- Cada línea citada contiene el código que se planta: la 62 de `ApprovePermissionRequest.cs` es la guarda de estado, la 71 es la comparación de identidades, la 62 de `RejectPermissionRequest.cs` es la misma comparación, la 43 del repositorio es el filtro por solicitante, la 99 de la entidad es `RejectionReason = reason;` y la 28 de la máquina es la última transición de la tabla.

**Qué NO incluye.** No hay etiquetas todavía (fase 6), ni promoción a `main` (fase 5), ni commits de defecto: el mapeo se obtuvo plantando y revirtiendo, sin dejar rastro en la historia. Ningún archivo de código ni de pruebas se modificó en este pull request.
