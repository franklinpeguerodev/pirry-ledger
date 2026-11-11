# ADR 017 - Document storage in SeaweedFS over S3

- **Estado:** aceptada
- **Fecha:** 2026-11-10
- **Requisitos:** RF-DOC-01 a RF-DOC-05 (enunciado de la Práctica 4); RD-03, RD-06, RD-07, RD-08, RD-09, RD-10, RD-11, RD-12
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

La Práctica 4 pide dos cosas que se pueden resolver con una sola pieza: el
**Manejador de documentos** (Pieza 3 del Core, RF-DOC-01 a RF-DOC-04) con sus
archivos en un **volumen con nombre** (RF-DOC-05), y **una API externa que exija
una clave** usada por una operación del proyecto (sección 1.4 del enunciado).

Franklin decidió que la API externa sea **SeaweedFS** (open source, Apache-2.0,
autoalojado en la composición). Por eso los documentos no se guardan en una
carpeta: se guardan en SeaweedFS, por la API de S3. El almacenamiento de las
imágenes del catálogo no cambia: sigue en Cloudflare R2 (ADR 015).

Restricciones que la decisión tiene que respetar:

- **RD-03:** el Core no puede depender de `Src/Business`, y el negocio no puede
  depender de una pieza del Core que no sea un contrato.
- **RD-06:** la autorización se verifica en el servidor, no en el cliente.
- **RD-07 y RD-08:** un archivo ausente o vacío, una clave que falta o un
  almacenamiento caído producen un rechazo controlado, sin trazas ni rutas.
- **RD-09:** los datos sobreviven al proceso.
- **RD-10:** la clave viaja por variables de entorno, nunca en el código.
- **RD-11:** las fechas salen del `IClock` inyectado.
- **RD-12:** las pruebas corren sin aplicación, sin base y sin red.
- **Solo Docker:** esta versión no se despliega en la nube.

## Decisión

### 1. Una pieza nueva del Core, con sus cuatro proyectos

`PirryLedger.Core.Documents` con Domain, Application, Infrastructure y Api, más
su proyecto de pruebas. Es la Pieza 3 del Core que pide el enunciado.

**Por qué no una pieza compartida con las imágenes.** El ADR 015 decidió no
crear una `Core.Media` porque las imágenes eran una necesidad del negocio y
habrían sido un cuarto punto de contacto entre el Core y el negocio. Aquí es al
revés: los documentos son una pieza del Core por exigencia del enunciado. Las
dos necesidades no se comparten de momento: unificarlas obligaría a mover el
puerto de las imágenes fuera de Business, que está fuera de este alcance.

### 2. Un puerto en Application y un adaptador en Infrastructure

El puerto `IDocumentStore` vive en `PirryLedger.Core.Documents.Application.Ports`
y tiene tres operaciones: guardar, leer y comprobar existencia. El adaptador
`SeaweedDocumentStore` vive en `...Infrastructure.Storage`.

**Por qué un puerto.** Las pruebas de los casos de uso corren con un doble en
memoria, sin red y sin servidor (RD-12). Además, si algún día el
almacenamiento cambia, el dominio y los casos de uso no se tocan. Es el mismo
razonamiento con el que se eligió `IImageStore` en el ADR 015.

### 3. Se habla S3 con el SDK de AWS

Se reutiliza el patrón ya probado en el ADR 015/016: `AmazonS3Client` con
`ServiceURL` en el endpoint configurado, `ForcePathStyle = true` y
`UseChunkEncoding = false`, porque firmar por bloques fue justo el error que
costó el ADR 016.

**Por qué no se reutiliza la clase del negocio.** `R2ImageStore` está en
`Business.Infrastructure` y trabaja con las revisiones de las imágenes. Importarla
desde el Core rompería RD-03. Se escribe un adaptador pequeño y propio.

### 4. La clave del objeto la genera el servidor

La clave es `documents/{guid}`. El nombre original del archivo se queda en la
base y nunca se usa como parte de la clave.

**Por qué no el nombre del usuario.** Dos usuarios pueden subir `factura.pdf`, y
un nombre de archivo puede traer `../`, caracteres de control o una longitud
absurda. Con un `Guid` la clave es única, corta y predecible solo por el
servidor.

**Por qué no separar por usuario en la ruta.** El identificador del usuario en
la clave no aporta nada al control de acceso (eso se decide en la base) y
filtraría quién subió qué a cualquiera que vea la clave.

### 5. Documentos genéricos, con un tope de 5 MiB

Se acepta cualquier tipo de archivo. No se inspecciona el contenido, no se
decodifica nada y no se valida el tipo declarado por el cliente: se guarda como
dato informativo. El tamaño máximo es **5 MiB** (5 242 880 bytes), configurable
con `PIRRY_LEDGER_DOCUMENT_MAX_BYTES`.

Una petición sin archivo, con más de un archivo o con un archivo vacío se
rechaza de forma controlada (RD-07).

**Por qué no validar el tipo.** El enunciado no lo pide para documentos y
cualquier lista blanca obligaría a decidir qué es "documento". Lo que sí se
hace es no confiar en el tipo al servir el archivo.

### 6. La descarga se sirve siempre como adjunto

`Content-Type: application/octet-stream`, `Content-Disposition: attachment` con
el nombre original codificado y `Content-Length`. Nunca en línea.

**Por qué.** Un HTML, un SVG o un PDF con código se ejecutarían en el navegador
de quien descarga si se sirvieran en línea, y el dominio del API no tiene por
qué alojar contenido ejecutable. El adjunto corta ese camino sin analizar el
archivo.

### 7. El registro vive en la tabla `doc_documents`

| Columna | Para qué |
|---|---|
| `id` | `Guid`, clave primaria |
| `name` | Nombre original del archivo, máximo 255 caracteres |
| `content_type` | Tipo declarado por el cliente; informativo |
| `size_bytes` | Tamaño de los bytes guardados |
| `uploaded_by` | Id del usuario que subió (valor plano, sin FK) |
| `branch_id` | Sucursal de la sesión (valor plano, sin FK) |
| `uploaded_at_utc` | Del `IClock` (RD-11) |
| `deleted_at_utc` | `NULL` mientras el documento está vigente |
| `deleted_by` | Id de quien lo eliminó, cuando aplica |
| `storage_key` | `documents/{guid}`, único |

**Por qué sin claves foráneas a otras piezas.** Cada módulo es dueño de sus
tablas. Una FK a la tabla de usuarios del Core ataría la migración de documentos
al orden de la de acceso, y borrar un usuario tendría efectos en una pieza que
no lo decidió. El identificador es un dato; la existencia del usuario se
comprueba en el caso de uso cuando hace falta.

**Por qué la sucursal sale de la sesión.** El cuerpo de la petición no puede
decidir a qué sucursal pertenece un documento: eso es lo que el filtro de acceso
ya resolvió para el usuario. Un Administrador pertenece a la sucursal global
`Branch.AllId` ("Todas"), que ya existe por el seed.

### 8. Borrado lógico: el objeto no se borra

Eliminar marca `deleted_at_utc` y `deleted_by`. El objeto sigue en SeaweedFS y
el registro sigue en la base. El listado excluye los eliminados.

**Por qué.** RF-DOC-04 lo pide así: "el registro sigue existiendo en el
almacenamiento". Además es lo que hace que la eliminación sea reversible y
auditable cuando llegue la Pieza 6.

### 9. La clave no es obligatoria al arrancar

Si falta la configuración de almacenamiento, **la aplicación arranca** y las
operaciones de documentos responden `503` con un mensaje comprensible. Si
SeaweedFS no responde, pasa lo mismo.

**Por qué no se copia el patrón del ADR 015.** Allí todas las variables son
obligatorias porque una subida de imágenes a medias fallaba tarde y sin
explicación. Aquí el enunciado pide lo contrario de forma explícita: "si la
clave no está definida o la API no responde, la operación devuelve un rechazo
controlado y **el resto del proyecto sigue funcionando**". Una aplicación que no
arranca sin la clave incumple ese punto.

### 10. Acceso declarado en la tabla única

Las cuatro operaciones (`UploadDocument`, `ListDocuments`, `DownloadDocument`,
`DeleteDocument`) se declaran en `Operation` y en `RoleRequirements` con nivel
`AnySession`. La propiedad —dueño o Administrador— se comprueba en el caso de
uso, comparando identidades, como se hizo con RF-GP-07.

**Por qué no dos niveles de acceso.** El acceso no depende solo del rol: depende
de quién es el dueño del documento concreto. Eso no se puede expresar en una
tabla de roles sin inventar un permiso por documento. El endpoint no compara
roles; el caso de uso recibe el usuario de la sesión y decide.

### 11. Las variables y el bucket

| Variable | Para qué |
|---|---|
| `PIRRY_LEDGER_STORAGE_ENDPOINT` | Endpoint S3 de SeaweedFS dentro de la composición |
| `PIRRY_LEDGER_STORAGE_ACCESS_KEY` | Clave de acceso |
| `PIRRY_LEDGER_STORAGE_SECRET_KEY` | Secreto |
| `PIRRY_LEDGER_STORAGE_BUCKET` | Bucket de los documentos |
| `PIRRY_LEDGER_DOCUMENT_MAX_BYTES` | Tope de tamaño |

El bucket lo crea SeaweedFS al arrancar (`S3_BUCKET`); la aplicación nunca crea
buckets. El servicio de SeaweedFS y la aplicación leen las **mismas** variables
de `.env`, así que no pueden desincronizarse.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Un volumen con nombre montado en la aplicación | Es más simple y más fácil de demostrar, pero deja la API externa de la sección 1.4 sin una operación real que la use. Se eligió una sola pieza que cumple las dos cosas. |
| Cloudflare R2, como las imágenes | Obliga a una cuenta real y a credenciales de terceros para una práctica que debe correr con un `.env` copiado. |
| Guardar los bytes en PostgreSQL (`bytea`) | Hincha la base, la respalda con archivos dentro y no es un volumen. RF-DOC-05 pide un volumen. |
| Una carpeta compartida por volumen entre la app y otro servicio | No es una API: no hay protocolo, ni clave, ni servicio externo que integrar. |
| Una API de terceros (clima) para 1.4 y un volumen para los documentos | Cumple 1.4 al pie de la letra, pero son dos integraciones y Franklin decidió una. Queda como camino si el profesor rechaza un servicio autoalojado. |
| URLs firmadas para la descarga, como en las imágenes | El cliente descargaría de SeaweedFS directamente y el endpoint interno quedaría expuesto. La descarga pasa por la API, que es donde se comprueba la propiedad. |
| Clave del objeto con el nombre original | Colisiones entre usuarios y nombres con `../` o caracteres raros. |
| Clave del objeto con el id del usuario | No aporta al control de acceso y filtra quién subió qué. |
| Validar el tipo de archivo contra una lista blanca | El enunciado pide documentos genéricos. La lista obligaría a decidir qué es un documento y a mantenerla. |
| Servir el archivo con su tipo declarado y en línea | Un HTML o un SVG se ejecutarían en el navegador del que descarga. |
| Clave obligatoria al arrancar, como en el ADR 015 | Contradice el requisito explícito de que el resto del proyecto siga funcionando sin la clave. |
| Borrado físico del objeto | RF-DOC-04 pide que el registro siga existiendo; además impediría auditar la eliminación en la Pieza 6. |
| Una pieza `Core.Media` compartida con las imágenes | Movería el puerto de las imágenes fuera de Business, que está fuera de este alcance. |

## Consecuencias

**A favor**

- Una sola integración cumple el volumen persistente y la API externa con clave.
- La aplicación no escribe en disco: la imagen sigue corriendo con el usuario sin
  privilegios y sin directorios de datos.
- La base no crece con los archivos; guarda solo el registro.
- Las pruebas de los casos de uso no necesitan ni red ni servidor.
- La clave de almacenamiento no aparece en el código, ni en el `Dockerfile`, ni
  en la composición, ni en el historial.

**En contra, asumido a conciencia**

- El "volumen con nombre" es el de SeaweedFS, no una carpeta del proyecto. Hay
  que demostrarlo en el README con el volumen y su contenido, y no basta con
  afirmarlo.
- La "clave de ejemplo" del enunciado **funciona**, porque es la misma con la
  que se configura el servidor. El rechazo controlado se demuestra de las dos
  formas que sí existen: sin clave y con el servicio parado.
- Un servicio más en la composición: si SeaweedFS no arranca, las operaciones de
  documentos responden `503`, aunque el resto del proyecto sigue andando.
- El objeto eliminado ocupa espacio hasta que exista una limpieza. Igual que los
  huérfanos de R2 en el ADR 015, se acepta por ahora.

## Qué queda fuera

- La auditoría de subidas y eliminaciones (RF-DOC-06), que llega con la Pieza 6.
- Adjuntar documentos a una entidad del negocio (RF-NEG-10).
- Cuotas por usuario, cifrado en reposo, versionado de objetos, miniaturas y
  conversión de formatos.
- URLs firmadas para la descarga y acceso directo del cliente a SeaweedFS.
- Borrado físico de objetos y limpieza de huérfanos.
- Mover las imágenes del catálogo al mismo almacenamiento.
