# Bitácora de la Práctica 4 — Contenedor, Manejador de documentos y API externa

- **Práctica:** 4 — El proyecto en un contenedor, el Manejador de documentos del Core y una API externa con su clave como secreto (Programación III, Bloque 4, Semana 9).
- **Repositorio:** `franklinpeguerodev/pirry-ledger`.
- **Herramienta:** agente de OpenCode en mi máquina (Windows, PowerShell 5.1, `dotnet` SDK 10.0.400 según `global.json`, 10.0.403 instalado) y un GitHub Codespace para todo lo que necesita Docker, porque mi máquina no lo tiene.
- **Rama de trabajo:** `feature/practica-4-documents-and-container`, desde `develop`.

La regla es la misma de las bitácoras anteriores: **lo que devuelve el agente es un borrador, no una verdad**. Nada se sube sin que yo lo lea y lo ejecute.

---

### Entrada 1 — Alcance, decisiones y las cuatro ADR (2026-11-10)

**Qué se pidió.** Convertir el brief de la Práctica 4 en un archivo de iteración sin decisiones abiertas: qué entra, qué no, cómo se verifica cada criterio y qué se decide antes de escribir código. En particular: dónde guardar los documentos, cómo correr las migraciones dentro de la composición, cómo se envía el correo y qué dirección de escucha usa el contenedor.

**Qué devolví.** Ocho decisiones cerradas conmigo, escritas en `docs/current-iteration.md` y en cuatro ADR:

| Decisión | ADR |
|---|---|
| Los documentos se guardan en SeaweedFS por S3, con una pieza propia del Core y clave generada por el servidor | 017 |
| Las migraciones se aplican dentro de la composición, con el comando `--migrate` y un servicio `migrate` | 018 |
| El correo de la composición va a Mailpit (sin puerto publicado) y lo envía un servicio `mailer` con `--send-mail` en bucle | 019 |
| La variable pública sigue mandando en los enlaces y el puerto, pero el proceso escucha en `0.0.0.0` | 020 (modifica la 005) |

También quedó decidido lo que no es ADR: documentos genéricos de 5 MiB máximo, Estándar ve solo los suyos y el Administrador ve todos, puerto 8090 dentro y fuera, y volumen con nombre para los bytes y otro para la base.

**Qué verifiqué.** Revisé las cuatro ADR y el `current-iteration.md`. `git log` mostró seis commits (`0b3960c` a `cedde2d`), uno por documento. `dotnet build` seguía en 0 errores y 0 advertencias.

---

### Entrada 2 — El Manejador de documentos y su suite (2026-11-10)

**Qué se pidió.** La pieza 3 del Core con su estructura completa, su tabla, su migración y su suite, sin romper nada de lo que ya estaba.

**Qué devolví.** Cuatro commits:

- `c1dbfbc` — la pieza, la entidad y sus reglas (nombre, tipo, tamaño, fecha, usuario, sucursal).
- `626b8d2` — el puerto `IDocumentStore` y el adaptador `SeaweedDocumentStore` (S3 con `ServiceURL`, `ForcePathStyle` y `UseChunkEncoding = false`, como el ADR 016).
- `ba51e4b` — casos de uso, `DocumentDbContext`, repositorio y la migración `doc_documents`.
- `a49cc2a` — los cuatro endpoints con `.WithAccess`, más las operaciones en `Operation` y en `RoleRequirements`.

La regla de propiedad quedó en Application (`DocumentOwnership`, comparando identidades) y no en los endpoints, porque depende del documento concreto y no del rol.

**Qué verifiqué.** `dotnet build pirry-ledger.slnx` en 0 errores y 0 advertencias; `dotnet test` con **480** pruebas (437 anteriores + 37 nuevas + 6 de la configuración SMTP). Vi las pruebas nuevas en rojo antes de cada verde: rompí a propósito la regla del tamaño máximo y falló `ElDocumentoQueSuperaElMaximoSeRechaza`.

---

### Entrada 3 — La composición y la primera subida real (2026-11-11)

**Qué se pidió.** `docker compose up --build` desde un clon limpio, con SeaweedFS, PostgreSQL y el correo, sin pasos manuales, y la aplicación respondiendo en `http://localhost:8090`.

**Qué devolví.** Los commits `d85ad9b` (migraciones), `3415f44` (escucha en `0.0.0.0`), `8eaeb32` (composición), `801a545` (correo) y la documentación del README. El `docker-compose.yml` levanta `db`, `seaweedfs`, `migrate`, `app`, `mailer` y `mailpit`; solo `app` publica un puerto.

**Qué verifiqué, en el Codespace.** La composición levantó completa:

```text
✔ Container pirry-ledger-db-1        Healthy
✔ Container pirry-ledger-migrate-1   Exited
✔ Container pirry-ledger-app-1       Up   0.0.0.0:8090->8090/tcp
```

`docker compose ps` mostró un solo puerto publicado (`app`), y `migrate-1 | Migraciones aplicadas: Documents.` confirmó el esquema. `docker volume inspect pirry-ledger-documents` devolvió el volumen con nombre y `docker compose exec seaweedfs ls -la /data` mostró los datos de SeaweedFS dentro.

**El defecto que apareció.** La primera subida respondió **500**. El log del contenedor dio la causa exacta:

```text
Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException: The database operation was
expected to affect 1 row(s), but actually affected 0 row(s)
   at DocumentRepository.SaveAsync(...) DocumentRepository.cs:line 25
   at UploadDocument.ExecuteAsync(...) UploadDocument.cs:line 64
```

El repositorio guardaba el alta con `Update`, que EF interpreta como un cambio de una fila existente. La corrección fue separar alta y cambio (`AddAsync` / `UpdateAsync`), como hace el módulo de negocio, y **protegerlo con una prueba** que reproduce el fallo sin Docker: `PruebasDelRepositorioDeDocumentos` falló con la misma excepción antes del arreglo y pasó después. `dotnet test` quedó en **483**.

---

### Entrada 4 — La segunda verificación y el camino completo (2026-11-11)

**Qué se pidió.** Repetir la verificación con el arreglo y demostrar los criterios de la rúbrica con salida real.

**Qué verifiqué.**

| Criterio | Salida real |
|---|---|
| Contenedor con un comando | `docker compose up -d` levanta los seis servicios; solo `app` publica `8090` |
| Migraciones dentro de la composición | `migrate-1 \| Migraciones aplicadas: Documents.` |
| Sesión | `login OK` con el Administrador de ejemplo |
| RF-DOC-01 | `subir=201` con el JSON del registro (`name`, `contentType`, `sizeBytes`, `uploadedByUserId`, `branchId`, `uploadedAtUtc`) |
| RF-DOC-02 | `listar=200` con el registro; la sucursal es `Todas` porque subió el Administrador |
| RF-DOC-03 | descarga `200` con `Content-Type: application/octet-stream`, `Content-Disposition: attachment` y el contenido `hola desde el codespace` |
| Rechazo por almacenamiento caído | `503` con `El almacenamiento de documentos no está disponible en este momento.` |
| Rechazo sin clave | `503` con `El almacenamiento de documentos no está configurado. Revisa el README.` |
| Sin claves reales | `docker compose config` mostró solo los valores de ejemplo |
| Volúmenes | `pirry-ledger-documents` y `pirry-ledger-db-data` con nombre |

Los dos rechazos controlados se verificaron en la corrida anterior al arreglo: viven en la capa de almacenamiento, que el arreglo no tocó, y se repetirán en el cierre.

**Qué se corrigió además.**

- `EXPOSE 8080` pasó a `8090`: el `docker compose ps` mostraba dos puertos porque el `Dockerfile` documentaba el puerto viejo.
- Las variables obligatorias de la composición pasaron a la forma `:?` de Compose, para que un `.env` incompleto diga **cuál** falta en vez de imprimir cien avisos y un `dependency failed to start` críptico.
- El README explica que la semilla del primer Administrador **no cambia la contraseña** de una cuenta que ya existe: es una trampa real, porque cambiar la variable del `.env` no tiene efecto y no estaba escrito.

**Qué me costó y por qué queda escrito.** Perdí varias corridas por el `.env`: el Codespace se recreó, el archivo no se versiona y lo escribí a mano, con dos valores distintos de la plantilla (`https` en la dirección pública y una contraseña distinta). La aplicación rechaza `https` a propósito (ADR 020) y la base no arranca sin `POSTGRES_PASSWORD`. De ahí salieron el mensaje claro del Compose y la nota de la semilla.

---

### Entrada 5 — La eliminación lógica y la supervivencia de los datos (2026-11-11)

**Qué se pidió.** Completar los dos criterios que faltaban: RF-DOC-04 (eliminar sin
borrar) y RF-DOC-05 (los datos sobreviven a `docker compose down`).

**Qué verifiqué.**

Eliminación lógica:

```text
== eliminar (logico) ==
eliminar=204
listar=200            (el documento eliminado ya no aparece)
```

y en la base, el registro sigue:

```text
    name    | eliminado | quien
------------+-----------+-------
 otro.txt   | t         | t
```

`eliminado = t` es `deleted_at_utc` no nulo y `quien = t` es `deleted_by` no nulo:
la fila existe, el objeto sigue en SeaweedFS y el listado no lo muestra.

Supervivencia:

```text
[+] down 7/7
[+] up 7/7
✔ Container pirry-ledger-db-1  Healthy
✔ Container pirry-ledger-migrate-1  Exited
sobreviviente
superviviente=200
```

El cuerpo `sobreviviente` es el contenido del documento que se subió **antes** del
`down`: el archivo estaba en el volumen `pirry-ledger-documents` y la base en
`pirry-ledger-db-data`, así que reiniciar la composición sin `-v` no perdió nada.

**Qué quedó pendiente en esta entrada.** Los seis frentes de la revisión
acumulada, que además demuestran el trozo de RF-DOC-02 y RF-DOC-03 que falta: que
otro usuario Estándar sea rechazado al descargar y al eliminar un documento ajeno.

---

### Entrada 6 — Los seis frentes de la revisión acumulada (2026-11-11)

**Qué se pidió.** Recorrer los seis frentes del enunciado con la composición
levantada: alta con activación por correo, sesión, rechazo de un Estándar en una
operación de Administrador, recuperación de contraseña, permisos y negocio.

**Qué verifiqué.**

| Frente | Salida real |
|---|---|
| Alta con activación | `alta=201`, el enlace sale del correo, `activar=200` → `{"activated":true}` |
| Sesión | `me=200` con `"role":"Standard"` y su sucursal, `logout=204`, `me-tras-logout=401` |
| Estándar rechazado | `403` con `No tiene permisos para esta operacion.` |
| Recuperación | `recovery=202`, solicitud `ResetPassword` en `Pending`, el Administrador la aprueba (`204`), el código llega por correo, `reset=204` y el login con la contraseña nueva funciona |
| Permisos | crear `202`, `aprobar=204`, `aprobar-otra-vez=400` (*"Solo se pueden aprobar solicitudes en estado Pendiente."*), `rechazar=204` |
| Negocio | factura `Issued`, `cobrar=200` → `Paid`, `transicion-prohibida=400` con *"La factura no puede pasar de Paid a NotDelivered."* |

Y de paso quedó cerrado el trozo de RF-DOC-02 y RF-DOC-03 que faltaba: con dos
Estándares distintos, Ana ve solo su documento, Beto recibe `403` al descargarlo
y al eliminarlo, y el Administrador sí puede.

**Qué aprendí de mis propios errores.** En esta entrada el agente se equivocó
tres veces, y el README tenía razón las tres:

1. Usó `POST /api/billing/invoices/{id}/payments` para cobrar, y esa ruta es
   `RegisterDebtPayment`, del Administrador. La de cajero es `/collect`.
2. Mandó `receivedAmount` en una factura de delivery, y el servidor lo rechaza
   con *"Una factura de delivery no registra el monto recibido ni el cambio."*
   La regla ya estaba documentada en el README.
3. Para la recuperación de un Estándar no aprobó la solicitud `ResetPassword`
   antes de leer el correo, así que tomó el token de activación como si fuera el
   código de recuperación. El paso de aprobación está en el ADR 014 y en el
   README.

---

### Entrada 7 — La corrida completa y el último defecto real (2026-11-11)

**Qué se pidió.** Una sola corrida desde cero que verifique todo lo calificable y
diga si algo falla.

**Qué devolví.** Un script que borra los volúmenes, levanta la composición,
ejecuta las comprobaciones y resume con `PASS`/`FAIL`. Resultado final: **49 de
49**, sin fallos.

**El último defecto real: el servicio de correo no enviaba nada.** El
`docker compose ps` lo delataba en la columna COMMAND: el contenedor `mailer`
mostraba `dotnet PirryLedger.…` en vez de `sh -c …`. La causa: la imagen trae
`ENTRYPOINT ["dotnet", "PirryLedger.Host.dll"]` y en Compose `command` no
reemplaza al entrypoint, se le agrega como argumentos. El bucle terminó como
argumentos de la API, que arrancó como servidor y nunca ejecutó `--send-mail`.

La corrección: el bucle va en el `entrypoint` (`/bin/sh -c`) y el `command` queda
como el script. `migrate` pasó a `command: ["--migrate"]` y la API se quedó sin
`command`: las dos confían en el entrypoint de la imagen. Está anotado en el ADR
019.

**El correo, verificado por la vía del README.** Con el `.env` apuntando a
Mailpit: `Correos tomados: 1`, `Correos enviados: 1`, `Correos fallidos: 0` y
`{"total":1}` en la bandeja de la composición, con el enlace de activación dentro
del mensaje.

**Lo que costó más de lo que debía: el archivo `.env`.** No se versiona, el
Codespace se recreó dos veces y el agente lo escribió a mano en lugar de copiarlo
de la plantilla. De ahí salieron tres tropiezos que quedaron documentados:

- `PIRRY_LEDGER_PUBLIC_BASE_URL` con `https`: la aplicación se detiene a
  propósito, porque no hay TLS (ADR 020).
- `POSTGRES_PASSWORD` distinto del que inicializó el volumen: `pg_isready` dice
  `healthy` igual, porque no autentica; el que falla es la aplicación.
- La semilla del primer Administrador **no cambia la contraseña** de una cuenta
  que ya existe.

Por eso el `docker-compose.yml` ahora exige las variables obligatorias con `:?`:
un `.env` incompleto da un mensaje con el nombre de la variable en vez de cien
avisos y un `dependency failed to start`.

**Estado al cerrar.** 27 commits en la rama, `dotnet build` en 0 errores y 0
advertencias, 483 pruebas en verde, y la corrida de 49 comprobaciones sin fallos.
Falta el push, el PR y la etiqueta, que son decisiones mías.

---

### Entrada 8 — La promoción a `main`, la etiqueta y el cierre (2026-11-10)

**Qué se pidió.** Cerrar la Práctica 4: preparar el pull request que promueve
`develop` a `main`, con el cuerpo completo y la evidencia de cada criterio de
aceptación, y dejar la práctica cerrada con su bitácora y su iteración marcadas
como terminadas. La etiqueta `practica-4` la pongo yo, porque la decisión de
dónde se etiqueta es del dueño del repositorio.

**Qué devolví.** Tres commits en la rama `docs/practica-4-closing`, desde
`develop`:

1. `69b3f93` — **las cifras del README corregidas contra el código entregado**.
2. `docs: record the closing of Práctica 4 in the logbook` — esta entrada.
3. `docs: mark the Práctica 4 iteration as concluded in current-iteration.md`.

Y el cuerpo del pull request `develop` → `main`, con las cuatro secciones de
`.github/PULL_REQUEST_TEMPLATE.md` y la salida real de cada comprobación.

**Qué se corrigió del README.** El README es lo que el revisor sigue al pie de
la letra, y en cuatro puntos ya no describía lo entregado:

| Punto | Lo que decía | Lo que hace el código |
|---|---|---|
| Variables de entorno | "la plantilla con las dieciocho variables" | `.env.example` tiene **25**: la composición añadió las tres `POSTGRES_*` y las cuatro `PIRRY_LEDGER_STORAGE_*` |
| Tablas del esquema | "catorce tablas de módulo" y "las cinco de `bil_`" | Son **dieciséis**: faltaban `bil_product_images` y `bil_image_revision`, así que la consulta SQL de esa sección devolvía dos filas más de las que el README prometía |
| Piezas del Host | "`Program.cs` solo hace `app.MapAccessControl()`, `app.MapPermissions()` y `app.MapNotifications()`" | Hace **cinco**: faltan `app.MapBusiness()` y `app.MapDocuments()`. El diagrama tampoco tenía la pieza Documents |
| La iteración | "La iteración actual (Práctica 1) entrega la primera pieza funcional del Core" | describe solo el Control de acceso, de la primera práctica |

Un quinto punto era la nota de la dependencia cruzada entre piezas del Core:
apuntaba a "decisión 5" de `docs/current-iteration.md`, y la reescritura de la
Práctica 4 ya no tiene esa decisión. Ahora apunta a `docs/bitacora-asignacion-2.md`,
que es donde vive el razonamiento, y menciona que **Documents** referencia
`AccessControl` por la misma razón que `Permissions`.

**Dónde se equivocó el agente, y cómo lo detecté.** En el plan de cierre
afirmé que el README tenía "un markdown roto" en la línea de la dependencia
cruzada, donde el nombre del archivo quedaba pegado a la palabra anterior.
**Era falso.** Al leer los bytes del archivo, la línea sí tenía el espacio: lo
que leí como markdown roto era un artefacto de la herramienta de edición. La
afirmación real —que el puntero a la "decisión 5" estaba roto— sí era cierta,
y la corregí. La lección: una suposición sobre el contenido de un archivo se
comprueba leyendo el archivo, no mirando el resultado de una edición anterior.

En la misma línea, el agente redactó primero "las cuatro de `bil_` sí se
referencian entre ellas, pero se crean en la misma migración" y, al
comprobar las migraciones una por una, resultó que no era exacto: hay cinco
claves foráneas entre las tablas de `bil_` y dos de ellas se crean en
migraciones distintas (`AddProductCatalog` y `AddProductImages`). La frase
correcta es que se referencian dentro de la misma pieza y las aplica EF Core
en el orden de sus migraciones. Se corrigió antes de commitear.

**Qué verifiqué, con la salida real.**

```powershell
dotnet build pirry-ledger.slnx --configuration Release
```

```
Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

```powershell
dotnet test pirry-ledger.slnx --configuration Release
```

| Suite | Pruebas |
|---|---|
| `PirryLedger.Core.AccessControl.Tests` | 155 |
| `PirryLedger.Business.Tests` | 220 |
| `PirryLedger.Core.Documents.Tests` | 40 |
| `PirryLedger.Core.Permissions.Tests` | 52 |
| `PirryLedger.Core.Notifications.Tests` | 12 |
| `PirryLedger.Integration.Tests` | 4 |

**483 superadas, 0 con error.** Los mismos números que la entrada 7.

La clave fuera del repositorio, comprobado en local:

```powershell
git check-ignore -v .env
git log --all -- .env
```

La primera devuelve `.gitignore:153:.env .env`; la segunda **no devuelve
nada**: no hay ningún `.env` en el historial, ni en ninguna rama.

**Lo que no se pudo verificar.** Docker no está instalado en esta máquina
(`docker` no está en el `PATH`), así que **la parte de contenedor y de API
externa no se pudo volver a correr hoy**. La evidencia de esa parte es la de
la entrada 7, en el Codespace del 2026-11-11: la corrida de 49 comprobaciones
sin fallos, con salida real por criterio. La revisión acumulada de los seis
frentes está en la entrada 6, con su salida real. Lo que sí se verificó hoy en
local es la suite completa y el build.

**Qué tiene que verificar Franklin.**

```powershell
git log --oneline origin/main..develop
git diff --shortstat origin/main..develop
```

Debe decir **30 commits** y **79 archivos, +5010 / −429**: exactamente lo que
el pull request `develop` → `main` lleva a `main`.

```powershell
dotnet build pirry-ledger.slnx --configuration Release
dotnet test pirry-ledger.slnx --configuration Release
```

Debe decir 0 advertencias, 0 errores y **483 superadas, 0 con error**.

Y, para la parte que esta máquina no tiene, la ruta del README en el Codespace:
`cp .env.example .env`, `docker compose up --build`, y los seis frentes con sus
cuerpos exactos.

**Lo que pasó después, con los datos reales.** El 2026-11-10 Franklin mergeó los
dos pull requests:

| Pull request | Base | Merge | Título |
|---|---|---|---|
| `#87 docs/practica-4-closing` | `develop` | `bb799ba` | Merge pull request #87 from franklinpeguerodev/docs/practica-4-closing |
| `#88 develop` | `main` | `0b39f21` | Merge pull request #88 from franklinpeguerodev/develop |

Y a petición explícita de Franklin, el agente creó y empujó la etiqueta:

```powershell
git tag -a practica-4 0b39f21 -m "Practice 4: document manager, container composition and external API"
git push origin practica-4
```

La etiqueta va sobre el commit de merge en `main`, `0b39f21`, igual que la
`practica-3` va sobre `415a874`. Es anotada (`git cat-file -t practica-4`
devuelve `tag`), y se empujó **solo esa etiqueta**, no `--tags`, para no subir
por accidente las demás. Verificado en el remoto:

```
f02732229247fcd0a777abb3346d88086dacc964	refs/tags/practica-4
0b39f21746567e2f05a2c68ba61c27f079f6fab1	refs/tags/practica-4^{}
```

La segunda línea es la etiqueta desreferenciada: el commit de merge en `main`.

**Comprobación final sobre el commit etiquetado**, con el árbol en `0b39f21`:
build en 0 advertencias y 0 errores, y las 483 pruebas superadas, 0 con error.
Además, `git diff --shortstat origin/develop..origin/main` sale vacío: `main` y
`develop` tienen el mismo contenido, así que la promoción no dejó nada atrás.

**Lo que este pull request NO incluye.** La auditoría de subidas y eliminaciones
(RF-DOC-06, Pieza 6, semana 12), adjuntar documentos a una entidad del negocio
(RF-NEG-10, opcional), despliegue en la nube, mover las imágenes del catálogo a
SeaweedFS (siguen en R2, ADR 015), y cualquier cambio en las máquinas de
estados existentes. Ninguna prueba se debilitó ni se eliminó.


