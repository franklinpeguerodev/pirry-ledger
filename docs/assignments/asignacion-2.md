# Asignación 2 — Gestión de permisos y su primera suite de pruebas

Programación III · TDS-007 · ITLA · 2026-C-3
Bloque 3 | Semana 6 | 5 puntos | Individual

| Se publica | Cierra | Entrega tardía |
|---|---|---|
| Miércoles 14 de octubre de 2026 | Martes 20 de octubre de 2026, 11:59 p. m. | Hasta 24 horas después, con 50 % de penalidad. Luego no se acepta. |

Segunda pieza del Core en el repositorio. Se entrega Gestión de permisos (solicitud y aprobación) funcionando, junto con su primera suite de pruebas unitarias. Se califica la pieza y se califican las pruebas: que estén bien construidas y que verifiquen lo que dicen verificar.

En resumen, se entregan tres cosas en el tag `asignacion-2`: la pieza Gestión de permisos funcionando, una primera suite de pruebas sobre ella y la bitácora con el agente.

Las transiciones válidas de una solicitud son Pendiente → Aprobada → Aplicada y Pendiente → Rechazada. Ninguna otra está permitida. Los identificadores son los de los Requerimientos del Core, pieza 2. Úsalos en los commits y en las pruebas.

## 1. Qué se entrega

### 1.1 Gestión de permisos funcionando

Los requisitos RF-GP-01 a RF-GP-07, con su criterio de aceptación, están en `docs/requirements/permissions.md`.

Aplican además RD-04 (las transiciones se resuelven en un solo punto del código), RD-06 (la autorización se verifica en el servidor), RD-07 (entrada inválida produce un rechazo controlado) y RD-09 (las solicitudes sobreviven a reiniciar la aplicación). Los registros de auditoría de la resolución se exigen en la semana 14; aquí no se califican.

El README indica cómo provocar cada criterio de aceptación. Se sigue al pie de la letra al calificar; lo que el README no dice, no se busca.

### 1.2 Suite de pruebas sobre Gestión de permisos

- Pruebas escritas con el framework de pruebas del lenguaje que se usa, dentro del repositorio, que ejercitan Gestión de permisos directamente, sin levantar la aplicación completa (RD-12).
- Al menos una prueba de una transición válida de la tabla de transiciones. Verifica el estado resultante y el dato que esa transición deja registrado (RF-GP-03 o RF-GP-04).
- Al menos una prueba de una transición prohibida. Verifica que el sistema la rechaza y que el estado de la solicitud no cambia (RF-GP-06).
- Cada prueba tiene tres fases identificables (preparar, actuar, verificar), verifica un solo comportamiento y su nombre dice cuál.
- Las pruebas son independientes entre sí: pasan en cualquier orden y cada una por separado.
- Un único comando, documentado en el README bajo el título **Pruebas**, ejecuta toda la suite desde un clon limpio del repositorio. La suite pasa.

### 1.3 Bitácora de sesión con el agente

Archivo `docs/bitacora-asignacion-2.md` dentro del repositorio, incluido en el tag. Documenta al menos una tarea de esta asignación que se le delegó al agente (construir parte de la pieza, proponer los casos de prueba, redactar una prueba, explicar un mensaje de fallo) con: qué se le pidió, qué devolvió, y al menos un caso en que se equivocó, cómo se detectó y cómo se corrigió. Una bitácora donde todo salió bien a la primera está incompleta y se devuelve.

## 2. Cómo se entrega

```powershell
git tag asignacion-2
git push origin --tags
```

En Moodle, en la tarea «Asignación 2»: la URL del repositorio y el nombre de la etiqueta, `asignacion-2`. Se evalúa el punto etiquetado; se puede seguir trabajando en `main` después.

## 3. Rúbrica (5.0 puntos)

Cada criterio se califica como No logrado (0 %), Parcialmente logrado (50 %) o Logrado (100 %).

### Gestión de permisos funcionando — RF-GP-01 a 07 (1.5 pts)

- **No logrado:** la pieza no existe, o no se puede crear una solicitud y resolverla (aprobar o rechazar).
- **Parcialmente logrado:** se crea, se aprueba y se rechaza, pero falta alguna regla: la aplicación automática (RF-GP-05), el rechazo de transiciones fuera de la tabla (RF-GP-06) o que nadie resuelva su propia solicitud (RF-GP-07).
- **Logrado:** las siete reglas cumplen su criterio de aceptación, las solicitudes sobreviven a reiniciar la aplicación y el README explica cómo provocar cada criterio.

### Estructura y ejecución de la suite (1.0 pt)

- **No logrado:** no hay suite, o no corre, o requiere levantar la aplicación completa, o falla en el tag.
- **Parcialmente logrado:** la suite corre y pasa, pero falta el comando documentado en el README, o alguna prueba depende del orden de ejecución, o hay pruebas sin tres fases identificables o con nombres que no dicen qué verifican.
- **Logrado:** un solo comando documentado ejecuta la suite desde un clon limpio, todas las pruebas pasan, son independientes entre sí, tienen preparar–actuar–verificar y nombres que describen el comportamiento.

### Transición válida (0.5 pt)

- **No logrado:** no hay ninguna prueba de una transición válida.
- **Parcialmente logrado:** hay una prueba, pero solo verifica que la operación no falla, o no comprueba el dato que la transición registra.
- **Logrado:** al menos una prueba verifica el estado resultante y el dato registrado (quién y cuándo, o el motivo).

### Transición prohibida (1.0 pt)

- **No logrado:** no hay ninguna prueba de una transición prohibida.
- **Parcialmente logrado:** hay una prueba que verifica el rechazo, pero no comprueba que el estado de la solicitud quedó igual.
- **Logrado:** al menos una prueba intenta una transición fuera de la tabla, verifica el rechazo y verifica que el estado no cambió.

### Bitácora con el agente (1.0 pt)

- **No logrado:** no hay bitácora, o no corresponde a una tarea de esta asignación, o no muestra evidencia de la sesión real.
- **Parcialmente logrado:** documenta qué se pidió y qué devolvió, pero no hay ningún caso de error del agente, o el caso no explica cómo se detectó y corrigió.
- **Logrado:** qué se pidió, qué devolvió, y al menos un error del agente detectado y corregido con evidencia de la sesión.

## 4. Qué se revisa exactamente

- `git clone` del repositorio y `git checkout asignacion-2`.
- El README: arranque de la pieza y cómo provocar cada criterio RF-GP, seguido al pie de la letra.
- Crear, aprobar, rechazar con motivo y aprobar dos veces una solicitud, para comprobar la pieza.
- El comando de la sección «Pruebas» del README, ejecutado tal como está escrito.
- Las pruebas de transición válida y prohibida: qué preparan, qué ejecutan y qué verifican.
- `docs/bitacora-asignacion-2.md`.