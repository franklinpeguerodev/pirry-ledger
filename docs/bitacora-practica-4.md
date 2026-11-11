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

### Entrada 5 — Pendiente de cierre

Falta, con salida real:

- Eliminar un documento: `204`, el listado sin él y la fila viva en `doc_documents` con `deleted_at_utc`.
- Supervivencia: subir, `docker compose down` (sin `-v`), `up` y descargar el mismo documento.
- Los dos rechazos controlados repetidos después del arreglo.
- Los seis frentes de la revisión acumulada, recorridos desde el README.
