# Bitácora de la Asignación 1

## Contexto

- Repositorio: `franklinpeguerodev/pirry-ledger`
- Asignación: 1 — `.gitignore`, README de ejecución y plantilla de pull
  request.
- Herramienta: agente de OpenCode corriendo en mi máquina (Windows,
  PowerShell 5.1).
- Fecha: 2026-09-27
- Rama de este documento: `feature/create-logbook`

Las tres tareas que aparecen abajo fueron **delegadas al agente**. Ninguno de
los tres archivos base se escribió a mano: se le pidió al agente que los
generara y después los revisé yo antes de subirlos.

La regla que usé durante toda la asignación: lo que devuelve el agente es un
borrador, no una verdad. Nada se sube sin que yo lo lea y lo ejecute en mi
máquina.

---

## Tarea 1 — `.gitignore` y limpieza del índice

### Qué le pedí

```text
Necesito un .gitignore para este repositorio. El proyecto es .NET
(microservicios) con frontend Angular. Requisitos:

1. Debe cubrir: sistema operativo, editores e IDEs, artefactos de build
   de .NET (bin/ y obj/), configuración local y secretos (nunca
   versionar appsettings de entorno ni *.user), tests y cobertura,
   logs, node_modules de Angular, Docker/Kubernetes, y archivos
   generados por herramientas.
2. No puede ignorar archivos que ya están versionados en el repo.
3. No inventes patrones: si no estoy seguro de si algo se versiona,
   pregúntame antes de excluirlo.
4. Al final dame los comandos que debo ejecutar para limpiar el índice,
   por si ya había algo versionado que no debía estarlo.
```

### Qué me devolvió

Un `.gitignore` de 326 líneas organizado en 9 secciones numeradas, más un
encabezado y un pie:

| # | Sección | Ejemplos de patrones |
|---|---------|----------------------|
| 1 | Sistema operativo | `Thumbs.db`, `Desktop.ini` |
| 2 | Editores e IDEs | `.vs/`, `.vscode/`, `*.user` |
| 3 | .NET — build y runtime | `bin/`, `obj/` |
| 4 | Configuración por entorno y secretos | `appsettings.*.local.json`, `user.env` |
| 5 | Tests y cobertura | resultados de test, informes de cobertura |
| 6 | Logs, diagnóstico y dumps | `*.log`, `*.dmp` |
| 7 | Angular / Node | `node_modules/`, artefactos de build de Angular |
| 8 | Docker / Kubernetes | contenedores, manifiestos locales |
| 9 | Herramientas y generados | `artifacts/`, `*.sqlite`, `local.db` |

Además me devolvió el bloque de comandos de limpieza con `git rm -r --cached`
para el caso de que ya hubiera archivos que no debían estar versionados.

### Qué hice yo

- Leí el archivo completo antes de commitear. Verifiqué los patrones más
  delicados con `git check-ignore -v` y confirmé que el `git status` seguía
  limpio, es decir, que el archivo no estaba ignorando nada que ya estuviera
  versionado.
- Resultado: no hubo nada que limpiar del índice, porque en ese momento el
  repositorio solo contenía `Src/.gitkeep`.
- Commit `4d5e691` (*Add .gitignore*), luego `14f434c` para agregar una nota
  al encabezado. PR #1 → merge `397d36a` a `develop`.

---

## Tarea 2 — README de ejecución  *(aquí hubo un error)*

### Qué le pedí

```text
Redacta el README.md inicial del repositorio. Debe tener:
1. Descripción breve del proyecto y su estado actual.
2. Cómo clonar el repositorio.
3. Dependencias: cómo instalarlas.
4. Cómo ejecutar el proyecto.
5. Qué NO incluye el repositorio por ahora.
```

### Qué me devolvió

Un `README.md` completo y bien redactado, con las cinco secciones. El
problema estaba en el contenido de dos de ellas. En *Dependencias* y en *Cómo
ejecutar* el agente puso:

- `npm install` para instalar las dependencias.
- `npm run dev` para levantar el proyecto.

### El error

**El agente inventó los comandos de instalación y de ejecución.**

Los dos comandos no tenían ningún respaldo en el repositorio:

1. **No existe `package.json`.** El repo, en ese momento, solo tenía
   `Src/.gitkeep`. Lo comprobé:

   ```powershell
   Test-Path -LiteralPath ".\package.json"
   # -> False
   ```

   Sin manifiesto de Node no hay nada que instalar con `npm install` ni
   servidor que levantar con `npm run dev`.

2. **El stack no era Node.** El propio `.gitignore` que el mismo agente había
   generado en la Tarea 1 declara en su línea 2 que el proyecto es
   *".NET (Microservicios) + Angular"*, y su sección 7 es justamente la de
   `node_modules/`. O sea, el agente se contradecía a sí mismo entre una
   tarea y otra.

3. **La asignación exige verificar antes de abrir el pull request.** El punto 2
   del enunciado pide que las instrucciones de ejecución sean comprobadas en
   la máquina de quien las escribe. Un README con comandos inventados no
   cumple ese punto aunque se vea bien escrito.

### Cómo lo detecté

Ejecuté los comandos tal como venían en el README, antes de commitear, que es
justo lo que la asignación pide. `npm install` falló de inmediato porque no
encuentra `package.json`, y el `README` describía un proyecto Node que el
repositorio no contenía. Un `git log` después también dejó claro que nunca
hubo un commit con esa versión: el error se detectó a tiempo y no llegó al
historial.

### Cómo lo corregí

Le devolví el mismo prompt con el error descrito y le exigí que reescribiera
*Dependencias* y *Cómo ejecutar* diciendo lo que realmente es cierto: que el
repositorio está en fase inicial, que no hay dependencias configuradas y que
todavía no existe un comando de ejecución. La regla que le puse fue explícita:
*"si no hay un comando, no inventes uno; dilo que no existe"*.

El resultado es el `README.md` que quedó en el repositorio:

- `README.md:16-19` — *Dependencias*: "Actualmente no hay dependencias
  configuradas ni un archivo de proyecto que requiera instalación de
  paquetes."
- `README.md:21-24` — *Cómo ejecutar*: "Todavía no existe un comando de
  ejecución. El repositorio se encuentra en su fase inicial y aún no
  contiene una aplicación implementada."

Commit `550c608` (*docs: add initial project readme*), 32 líneas. PR #3 →
merge `b9fea6f`.

---

## Tarea 3 — Plantilla de pull request

### Qué le pedí

```text
Crea el archivo .github/PULL_REQUEST_TEMPLATE.md con estas cuatro
secciones y solo estas cuatro:

1. Qué cambia
2. Por qué
3. Cómo probarlo
4. Qué NO incluye

Bajo cada encabezado, deja un comentario HTML que le recuerte a quien
llene la plantilla qué escribir en esa sección.
```

### Qué me devolvió

El archivo con exactamente las cuatro secciones acordadas, en ese orden, cada
una con su comentario guía en HTML. Son 15 líneas en total
(`.github/PULL_REQUEST_TEMPLATE.md:1-15`):

- `## Qué cambia` con la guía de describir qué hace y qué difiere del estado
  anterior.
- `## Por qué` con la guía de explicar la razón y el requisito que atiende.
- `## Cómo probarlo` con la guía de pedir comandos exactos y resultado
  esperado.
- `## Qué NO incluye` con la guía de aclarar qué queda fuera del PR.

### Qué hice yo

Conté las secciones y revisé que no hubiera metido ninguna extra, porque el
punto 3 del enunciado pide las cuatro secciones acordadas en clase. Todo
correcto, así que lo subí sin cambios.

Commit `6d32eca` (*docs: add pull request template*). PR #4 → merge `96f9db4`.

---

## Resumen de las entregas

| Commit | Archivo | Tarea | PR |
|--------|---------|-------|-----|
| `4d5e691` | `.gitignore` (326 líneas) | Tarea 1 | #1 → `develop` |
| `14f434c` | `.gitignore` (nota en el encabezado) | Tarea 1 | #1 |
| `550c608` | `README.md` (32 líneas) | Tarea 2 | #3 → `main` |
| `6d32eca` | `PULL_REQUEST_TEMPLATE.md` (15 líneas) | Tarea 3 | #4 → `main` |

En las tres tareas el agente acertó en la estructura y el formato. El único
error fue de contenido: se inventó dos comandos de ejecución en el README.

## Qué cambió en mi forma de trabajar

Después del error del README me quedaron tres reglas que vengo aplicando:

1. **Comprobar las afirmaciones del agente con un comando, no con los
   ojos.** `Test-Path`, `git check-ignore -v`, `git status`. Un archivo puede
   estar perfectamente bien escrito y aun así estar equivocado.
2. **Ejecutar todo comando que el agente recomiende antes de commitear.**
   Es lo que exige el enunciado, y fue justo lo que destapó el error.
3. **Cruzar las tareas entre sí.** El agente se contradijo respecto al stack
   entre la Tarea 1 y la Tarea 2. Comparar lo que dice un archivo con lo que
   dice el otro es barato, y es exactamente la revisión que yo no estaba
   haciendo: solo leía el archivo nuevo.

En resumen: delegar al agente ahorra escribir, pero la revisión es la parte
del trabajo que no se puede delegar. La Tarea 2 es el ejemplo: el agente
devolvió un documento que parecía correcto y no lo era.
