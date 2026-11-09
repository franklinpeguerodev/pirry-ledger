# Bitácora — Imágenes del catálogo

- **Trabajo:** imágenes del catálogo de productos, con almacenamiento privado en Cloudflare R2, firma de descarga y sincronización por cursor de revisión.
- **Repositorio:** `franklinpeguerodev/pirry-ledger`.
- **Herramienta:** agente de OpenCode en mi máquina (Windows, PowerShell 5.1, `dotnet` SDK 10.0.400 según `global.json`, 10.0.403 instalado).
- **Rama de trabajo:** `feature/product-images`, creada desde `develop`.
- **Decisión:** ADR 015, `docs/adr/015-imagenes-del-catalogo.md`.

La regla es la misma de las bitácoras anteriores: **lo que devuelve el agente es un borrador, no una verdad**. Nada se sube sin que yo lo lea y lo ejecute en mi máquina.

---

### Entrada 1 — El diseño del delta: el backend no puede depender del cliente (2026-11-09)

**Qué se pidió.** Agregar una imagen al catálogo de productos: la clave se
guarda en la base de datos propia, el archivo vive en Cloudflare como destino
final, las imágenes son privadas y el backend genera firmas para los clientes.
Para no pedir firmas cada vez que se abre la app, el cliente pide las imágenes
y el backend le devuelve cuáles debe agregar o modificar.

**Qué devolví.** Un plan con el puerto `IImageStore`, el adaptador `R2ImageStore`,
la tabla `bil_product_images` con su historial de revisiones, un contador global
de revisiones y el protocolo de sincronización por cursor.

**En qué se equivocó el agente, y cómo lo detecté.** La primera versión del
protocolo era esta: el cliente mandaba la lista de lo que ya tenía
(`{"known": {"clave": "version"}}`) y el servidor calculaba el delta con esa
información. Al revisar el diseño dijiste: *"la versión se puede falsificar,
o sea de qué tanto el backend depende del cliente, el backend debe ser
independiente del cliente"*.

Tenías razón y el agujero era real: con ese protocolo, un cliente que afirma
una versión equivocada hace que el servidor **omita** una imagen, porque el
servidor está decidiendo con un dato que le dio el cliente. La versión y la
clave las calculaba el servidor, pero la *decisión* de qué enviar se apoyaba en
lo que el cliente afirmaba.

**Cómo se corrigió.** El cliente dejó de mandar contenido y pasó a mandar una
**posición**: el `since` que el propio servidor le emitió en la respuesta
anterior. El delta lo calcula el servidor con su propio historial, así que un
cursor mentiroso solo puede provocar que el cliente reciba más de lo necesario
o que se le pida una sincronización completa; nunca puede hacer que el servidor
olvide un cambio. Se añadió además el contador global de revisiones con su
bloqueo de fila, que es lo que garantiza que el número de revisión y el orden
de confirmación coincidan: sin eso, dos escrituras simultáneas podrían
confirmarse en orden inverso y el cliente se saltaría un cambio para siempre.

Esa corrección cambió el ADR, el modelo de datos y el contrato HTTP, y es la
razón de que el contador de revisiones y su transacción sean parte del diseño y
no un detalle.

---

### Entrada 2 — Qué se construyó y cómo se verificó (2026-11-09)

**Qué se pidió.** Ejecutar el plan aprobado: las dos ramas, los commits, las
pruebas, la documentación y la auditoría de seguridad.

**Qué devolví.** Doce commits en dos ramas:

| Rama | Commit | Qué hace |
|---|---|---|
| `docs/resolve-agents-open-decisions` | `Resolve the open decisions recorded in AGENTS.md` | Las dos decisiones abiertas de `AGENTS.md` (el frontend y el proyecto `Shared`) pasan a la lista de resueltas. |
| `feature/product-images` | `Document product image storage decision (ADR 015)` | La decisión completa: dónde vive el adaptador, por qué R2, por qué el cursor, por qué se quitan los metadatos y por qué los huérfanos se asumen. |
| | `Open product image scope in current iteration` | El alcance de la iteración y la entrada en `entities.md`. |
| | `Add product image domain rules (ADR 015)` | La entidad `ProductImage`, el detector por bytes mágicos y las reglas del dominio. |
| | `Strip metadata from uploaded images (ADR 015)` | La limpieza de EXIF, XMP e IPTC sin re-codificar la imagen. |
| | `Add image storage port and R2 adapter (ADR 015)` | El puerto, el adaptador de R2 y las variables de entorno obligatorias. |
| | `Add product image table and revision counter (ADR 015)` | Las dos tablas nuevas, el repositorio con la transacción del contador y la migración. |
| | `Set and remove product images as administrator (ADR 015)` | Subir, reemplazar y quitar; los límites de petición; el acceso por rol. |
| | `Remove and restore product images with product activation (ADR 015)` | Desactivar saca la foto del conjunto y reactivar la devuelve. |
| | `Sync product images by revision cursor (ADR 015)` | El delta con cursor, las firmas y el catálogo con la referencia de la imagen. |
| | `Document product image endpoints and limits in README` | La sección 3.15 del README, las variables, los endpoints y el esquema. |
| | `Add product image logbook entry` | Esta bitácora. |

**Qué verifiqué, con la salida real.**

```powershell
dotnet build
```

```
Compilación correcta.
    0 Errores
```

```powershell
dotnet test pirry-ledger.slnx
```

```
Correctas! - Con error:     0, Superado:   202, Omitido:     0, Total:   202 - PirryLedger.Business.Tests.dll
Correctas! - Con error:     0, Superado:     6, Omitido:     0, Total:     6 - PirryLedger.Core.Notifications.Tests.dll
Correctas! - Con error:     0, Superado:     4, Omitido:     0, Total:     4 - PirryLedger.Integration.Tests.dll
Correctas! - Con error:     0, Superado:    52, Omitido:     0, Total:    52 - PirryLedger.Core.Permissions.Tests.dll
Correctas! - Con error:     0, Superado:   155, Omitido:     0, Total:   155 - PirryLedger.Core.AccessControl.Tests.dll
```

Son 419 pruebas, 76 más que antes de este trabajo. Ninguna necesita base de
datos, ni bucket, ni SMTP (RD-12).

El arranque sin la configuración del bucket también se comprobó, porque las
siete variables son obligatorias:

```powershell
dotnet run --project Src\Host\PirryLedger.Host
```

```
Falta la variable de entorno PIRRY_LEDGER_R2_ACCOUNT_ID. El README explica como definirla.
```

**Las pruebas pueden fallar.** Cada prueba nueva se mostró roja por la razón
correcta antes de mostrarse verde, rompiendo el código a propósito:

| Control | Qué se rompió | Qué se puso rojo |
|---|---|---|
| La clave del objeto | La clave pasó a llevar un sufijo | `CrearImagenGuardaLaClaveConElProductoYElIdentificador` |
| La limpieza de metadatos | `ShouldKeepJpegSegment` dejó de filtrar los `APPn` | `ElSegmentoExifDelJpegSeElimina` y `UnSegmentoDesconocidoDelJpegSeEliminaSinRechazar` |
| La image vive y la anterior sale del conjunto | Se quitó `anterior.Retire(...)` | `SustituirLaImagenRetiraLaAnteriorYDejaLaNuevaVigente` |
| El delta ve la reactivación | Se quitó `row.product.ImageRevision > since` | `LasAltasTraenLaImagenCuandoElProductoSeReactiva` y `LaSincronizacionTraeLaImagenDeUnProductoQueSeReactivo` |
| El acceso por rol | `SetProductImage` pasó a `AnySession` | `LasOperacionesDeAdministracionExigenElRolDeAdministrador` |

---

### Entrada 3 — Tres errores del agente que encontraron las pruebas (2026-11-09)

**Qué se pidió.** Implementar la limpieza de metadatos sin re-codificar la
imagen, con las reglas que seMurray fijaron: rechazar solo lo que no es una
imagen estructuralmente válida y **nunca** rechazar por un metadato.

**En qué se equivocó el agente.** Tres fallos, los tres encontrados por las
pruebas y ninguno por la lectura del código:

1. **Se perdía el byte `0xFF` del marcador.** Al copiar un segmento de JPEG, el
   segmento se copiaba desde los dos bytes de longitud, así que el `0xFF` y el
   byte del marcador se perdían y la imagen salía rota. Lo detectó
   `UnJpegSinMetadatosQuedaIgual`, que comparaba el archivo de entrada con el
   de salida. La corrección fue que `ReadSegment` devolviera el marcador
   completo.

2. **Los bits de EXIF y XMP de la cabecera `VP8X` no se limpiaban.** El código
   limpiaba los bytes del payload y luego copiaba los bytes **originales** del
   chunk, así que la cabecera seguía anunciando metadatos que ya no estaban.
   Lo detectó `LosBitsDeExifYXmpDeLaCabeceraVp8xSeLimpian`.

3. **Una prueba mía estaba mal, no el código.** `ReemplazarLaImagenBorraElArchivoAnterior`
   falló porque usaba dos JPEG que solo se diferenciaban en el APP1 de EXIF.
   Como el hash se calcula **sobre el contenido ya limpio**, esos dos archivos
   son la misma imagen y la subida no hace nada. El comportamiento del código
   era el correcto; la prueba se corrigió para que las dos fotos tuvieran
   píxeles distintos.

---

### Entrada 4 — Dos decisiones que se corrigieron antes de escribir código (2026-11-09)

**La limpieza de huérfanos.** El plan traía una tabla `bil_image_object_cleanup`,
un servicio en segundo plano y un protocolo de reservas para borrar los objetos
que nadie referencia. Dijiste: *"eso no lo hagas que se asuma imagenes residuales
por ahora"*. Se quitó la tabla, el servicio y su commit, y la limpieza pasó a
la lista de riesgos residuales asumidos. El README lo dice en la sección 3.15 y
en "Qué NO incluye".

**El rechazo de las imágenes.** El plan decía que un JPEG con un metadato en una
posición inesperada se rechazaba con `415`. Dijiste que eso había que
corregirlo. La regla quedó al revés: se rechaza **solo** lo que no es una
imagen estructuralmente válida (falta el marcador inicial o el final, una
longitud que se sale del archivo, un chunk crítico desconocido) y **cualquier
metadato desconocido se elimina sin rechazar**. Un archivo que cumple la
estructura se guarda aunque el cliente no lo sepa mostrar, que es la
contrapartida de no decodificar nunca un archivo no confiable.

También cambiaste el tamaño máximo de 2 MiB a **5 MiB**, pensados para una
foto de 1920×1080 en JPEG, WebP o PNG razonable, y pediste que **todas** las
variables del almacenamiento fueran obligatorias, para que una configuración a
medias no llegue a producción.

---

### Entrada 5 — Lo que no se pudo verificar (2026-11-09)

**La prueba real contra Cloudflare R2.** El agente no tiene credenciales de
R2 y **no abrió el archivo `.env`**. El flujo completo —subir una foto, pedir
la sincronización, descargar con la firma y comprobar que sin firma el bucket
responde `403`— queda como paso manual, documentado en el README, y se ejecuta
cuando las variables estén en el `.env`.

**La migración contra un PostgreSQL real.** El agente levantó un PostgreSQL
desechable en el directorio temporal para aplicar la migración y probar el
bloqueo del contador, pero el servidor de esa instalación no levantaba en este
entorno (el registro muestra `autovacuum worker terminated by exception
0xC0000142` y el proceso se reiniciaba). El cluster se borró. La migración
`AddProductImages` está **generada** y revisada, pero **no aplicada** a la base
de desarrollo: eso lo hace falta con tus credenciales, en la terminal, con la
variable de entorno del proceso (el paso 6 del README).

**El camino relacional del repositorio.** Las pruebas corren contra EF
InMemory (RD-12), así que la rama que usa el `UPDATE` con el bloqueo de fila
no está cubierta por la suite. El SQL es una sola sentencia sobre la tabla
`bil_image_revision`, y la migración crea la fila inicial, pero conviene
aplicarla y probarla contra la base real.

---

### Entrada 6 — La parte del cliente queda fuera (2026-11-09)

**Qué se pidió.** Que en esta iteración el cliente no entre: *"en esta iteración
el client sale totalmente se piense en el backend"*.

**Qué devolví.** El backend completo y su contrato: las tres rutas, el
formato de la sincronización, la firma, los límites y el README. La caché
local del teléfono, el selector de imágenes del administrador y las miniaturas
en el mostrador quedan para la iteración siguiente, y el README lo dice.

---

### Entrada 7 — La subida de imágenes fallaba contra R2 (2026-11-09)

**Qué se pidió.** Averiguar por qué la subida de una imagen devolvía
`Fallo no controlado al procesar /api/admin/products/{id}/image` con la
excepción `Amazon.S3.AmazonS3Exception: STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER
not implemented`.

**Qué encontré.** Dos defectos, uno detrás del otro:

1. **El SDK de AWS mandaba el cuerpo con el checksum en un trailer.** Desde la
   versión 4, `AWSSDK.S3` calcula un checksum y lo envía en un trailer
   (`aws-chunked`) por defecto. R2 no implementa esa variante del protocolo y
   rechaza la petición. No era el bucket, ni las credenciales, ni el endpoint:
   era el formato del cuerpo. Se corrigió fijando
   `RequestChecksumCalculation` y `ResponseChecksumValidation` en
   `WHEN_REQUIRED` al construir el cliente. No se usó la variable de entorno
   `AWS_REQUEST_CHECKSUM_CALCULATION` porque dependería de que cada despliegue
   la defina; el ajuste va en el código y lo cubre una prueba.

2. **El fallo no se convertía en un 503.** El `catch (AmazonClientException)`
   no cubría `AmazonS3Exception`, que en el SDK v4 es una clase hermana y no
   una subclase. La excepción escapaba hasta la frontera del API, que la
   registraba como fallo no controlado y respondía `500` con la traza. Al
   escribir la prueba apareció un tercer caso que el plan no preveía: cuando
   el bucket no responde, el SDK deja escapar `HttpRequestException`. El
   `catch` final cubre las tres: `AmazonServiceException`,
   `AmazonClientException` y `HttpRequestException`. Las tres se traducen a
   `ImageStorageUnavailableException`, que el API responde como `503`. El log
   escribe solo la clave del objeto, nunca el mensaje del SDK (RD-08).

**Qué verifiqué, con la salida real.**

```powershell
dotnet test pirry-ledger.slnx
```

```
Correctas! - Con error:     0, Superado:   204, Omitido:     0, Total:   204 - PirryLedger.Business.Tests.dll
Correctas! - Con error:     0, Superado:     6, Omitido:     0, Total:     6 - PirryLedger.Core.Notifications.Tests.dll
Correctas! - Con error:     0, Superado:     4, Omitido:     0, Total:     4 - PirryLedger.Integration.Tests.dll
Correctas! - Con error:     0, Superado:    52, Omitido:     0, Total:    52 - PirryLedger.Core.Permissions.Tests.dll
Correctas! - Con error:     0, Superado:   155, Omitido:     0, Total:   155 - PirryLedger.Core.AccessControl.Tests.dll
```

Son 421 pruebas, dos más que antes de este arreglo.

**Las dos pruebas nuevas se mostraron rojas por la razón correcta:**

| Prueba | Qué se rompió | Qué se puso rojo |
|---|---|---|
| `ElClienteNoEnviaElChecksumEnTrailerQueR2NoAcepta` | Los dos ajustes pasaron a `WHEN_SUPPORTED` | `Expected: WHEN_REQUIRED / Actual: WHEN_SUPPORTED` |
| `ElFalloDelBucketSeDevuelveComoAlmacenamientoNoDisponible` | El `catch` volvió a `AmazonClientException` | `Expected: ImageStorageUnavailableException / Actual: HttpRequestException` |

**Lo que no se pudo verificar.** La subida real contra R2, porque el agente no
tiene credenciales. Queda como paso manual: con el `.env` real, la misma
petición debe devolver `200`; con una clave incorrecta, `503` con el mensaje
genérico y sin el texto del SDK en el log.
