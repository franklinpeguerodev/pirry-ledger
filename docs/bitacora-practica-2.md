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
