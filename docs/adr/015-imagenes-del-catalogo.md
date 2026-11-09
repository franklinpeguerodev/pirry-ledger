# ADR 015 - Product image storage and synchronization

- **Estado:** aceptada
- **Fecha:** 2026-11-09
- **Requisitos:** extensión propia de Franklin; RD-02, RD-03, RD-06, RD-07, RD-08, RD-09, RD-10, RD-11, RD-12
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

El catálogo de productos (`bil_products`) tiene nombre, descripción, precio y estado,
pero no tiene imagen. El mostrador de una sucursal necesita ver la foto del producto
para cobrar, y el Administrador necesita poder ponerla y quitarla.

Franklin pidió tres cosas:

1. La **clave** de la imagen se guarda en la base de datos propia y el **archivo** vive
   en Cloudflare (R2), que es el destino final.
2. Las imágenes son **privadas**: no hay URL pública. El backend genera **firmas** para
   los clientes.
3. Para no pedir firmas cada vez que se abre la aplicación, el cliente pide las
   imágenes y el backend le devuelve **cuáles debe agregar o modificar**.

Durante la conversación se corrigieron dos puntos del diseño inicial:

- **La versión no la manda el cliente.** La primera propuesta era que el cliente
  enviara la lista de lo que ya tenía y el servidor calculara el delta con esa
  información. Eso hace que el backend dependa de lo que afirma el cliente: un
  cliente que miente, o un error, hace que el servidor omita una imagen. La versión
  y la clave las calcula el servidor, siempre.
- **Esta iteración es solo el backend.** El cliente móvil queda para la iteración
  siguiente, con el contrato que este ADR fija.

## Decisión

### 1. El adaptador vive dentro del módulo Business

El puerto `IImageStore` va en `PirryLedger.Business.Application.Ports` y la
implementación `R2ImageStore` en `PirryLedger.Business.Infrastructure.Storage`. No
hay una pieza `Core.Media`.

**Por qué:** `AGENTS.md` dice que el Core se conecta al módulo de negocio por tres
puntos solamente: notificar y alimentar reportes, adjuntar documentos (opcional) y
proteger endpoints con roles del Core. Una pieza de Core para las imágenes sería un
cuarto punto de contacto, y el alcance actual dice que no se toca el Core. Además,
una pieza nueva son cuatro proyectos, una tabla y una migración para algo que solo
el módulo de negocio necesita. El puerto deja la puerta abierta para que, si otro
módulo necesita guardar archivos, el adaptador se mueva a un `Core.Media` sin tocar
el dominio ni la API.

### 2. Cloudflare R2 con URL firmadas

R2 es compatible con S3, así que las firmas son las *presigned URLs* de SigV4 que
genera el SDK de AWS (`AWSSDK.S3`). El bucket es privado: el único acceso es por
firma.

La firma dura **15 minutos**. El cliente guarda los **bytes** en su disco, no la
URL, así que la firma solo tiene que vivir lo que dura la descarga.

La firma se genera **en local**: es un cálculo con una clave, sin llamada de red.

### 3. La clave del objeto

```
products/{productId}/{imageId}.{ext}
```

`imageId` es un `Guid` nuevo en cada subida. El nombre del archivo que envía el
cliente se descarta.

**Por qué no el sha256 en la clave.** Con la clave basada en el contenido, este
cambio se puede perder:

1. Se sube A (clave `products/P/{sha256(A)}.jpg`).
2. Se reemplaza por B. El borrado de A se ejecuta después de confirmar la base.
3. Se vuelve a subir A. El servidor sube la misma clave otra vez.
4. El borrado del paso 2 se ejecuta después del paso 3 y borra el objeto recién
   subido.
5. La base confirma una imagen vigente cuyo objeto ya no existe.

Con una clave propia por subida, borrar una clave nunca afecta a otra subida. El
sha256 sigue en la columna `version`, que es la que el cliente usa para verificar
la descarga.

### 4. La versión la calcula el servidor

`version` es el sha256 de los bytes **que se guardan**, después de quitar los
metadatos. La calcula el servidor en el momento de la subida y nadie la puede
elegir. El cliente la usa para comprobar que lo que descargó es lo que el servidor
prometió, y la envía de vuelta **nunca**: la sincronización no lleva versiones del
cliente.

### 5. Qué se acepta en una subida

| Regla | Valor |
|---|---|
| Tipos | `image/jpeg`, `image/png`, `image/webp` |
| Cómo se decide el tipo | Por los **bytes mágicos** del archivo, no por el `Content-Type` declarado |
| Tamaño máximo | 5 MiB (5 242 880 bytes) |
| Partes del formulario | Exactamente una, llamada `file` |
| Estructura rota | Se rechaza con `415` |

El tamaño de 5 MiB cubre una foto 1920×1080 en JPEG, WebP o PNG razonable. Con
2 MiB, muchas fotos de celular en JPEG de calidad alta no entrarían.

### 6. Los metadatos se quitan, y no se re-codifica la imagen

Una foto de celular trae fecha, modelo y, a menudo, **coordenadas GPS**. Esos datos
no deben salir del sistema en un catálogo de productos.

La limpieza quita los segmentos de metadatos y **solo** ellos: en JPEG, los `APPn`
que no sean `APP0` (JFIF), `APP2` (perfil ICC) o `APP14` (Adobe), y `COM`; en PNG,
los chunks auxiliares que no sean parte de la imagen; en WebP, `EXIF` y `XMP `. Los
píxeles no cambian.

**Por qué no se re-codifica.** Re-codificar obliga a **decodificar** un archivo no
confiable, y un archivo pequeño puede declarar dimensiones enormes y agotar la
memoria. Esa es la superficie de ataque clásica de los decodificadores de imagen.

**Por qué no una librería de imágenes.** Las librerías de .NET más usadas tienen
licencias comerciales para organizaciones grandes, y esa licencia hay que verificarla
antes de adoptarla. El trabajo es pequeño: los tres formatos son segmentos o chunks
con longitud explícita, y cada longitud se valida contra lo que queda del archivo.

**Regla de rechazo:** se rechaza solo lo que no es una imagen estructuralmente
válida (falta el marcador inicial o el final, una longitud que se sale del archivo,
un chunk crítico desconocido). **Los metadatos nunca son motivo de rechazo**: un
segmento o chunk desconocido se elimina y la subida continúa.

### 7. El modelo de datos: el historial es el propio dato

Cada cambio en el conjunto de imágenes **inserta una fila**. Una fila existente solo
se modifica para registrar su salida del conjunto.

`bil_product_images`:

| Columna | Para qué |
|---|---|
| `id` | `Guid` generado antes de subir; forma parte de la clave del objeto |
| `product_id` | Referencia a `bil_products` |
| `image_key` | `products/{productId}/{id}.{ext}` |
| `version` | sha256 en hexadecimal minúsculas |
| `content_type` | Lista blanca |
| `size_bytes` | Tamaño de los bytes guardados |
| `revision_created` | Revisión en la que entró al conjunto |
| `revision_removed` | Revisión en la que salió; `NULL` mientras sea vigente |
| `created_at_utc`, `removed_at_utc` | Del reloj inyectado (RD-11) |

Índice único parcial: `UNIQUE (product_id) WHERE revision_removed IS NULL`. Una sola
imagen vigente por producto, garantizada en la base de datos y no solo en el código.

`bil_image_revision` es una tabla de una sola fila con el contador global de
revisiones. Cada operación que cambia el conjunto toma el bloqueo de esa fila
(`SELECT … FOR UPDATE`), incrementa el valor y lo usa en todas las filas que cambian.

**Por qué un contador y no una `identity`.** Una columna identity se asigna solo al
insertar, y una baja necesita una revisión nueva **en una fila que ya existe**. Por
eso no sirve.

**Por qué un contador y no una fecha.** Dos cambios en el mismo instante empatarían y
el cursor quedaría ambiguo.

**Por qué el bloqueo.** Sin él, dos escrituras simultáneas podrían confirmarse en
orden inverso al de sus revisiones, y un cliente se saltaría un cambio para siempre.
Con el bloqueo, las revisiones se confirman en orden.

### 8. El cursor y el delta

El **cursor** es un número que emite el servidor. Cada respuesta de la
sincronización incluye la revisión actual; el cliente la guarda y la envía como
`since` en la siguiente consulta.

El cliente no afirma qué tiene. Dice "me diste hasta la revisión N" y el servidor
calcula qué falta a partir de su propio historial. Si el cliente miente, lo peor que
pasa es que reciba más de lo necesario o que pida una sincronización completa; nunca
puede hacer que el servidor olvide un cambio.

Con `since`:

- **Altas:** filas con `revision_created > since` y `revision_removed` vacío. Solo
  estas se firman.
- **Bajas:** claves con `revision_removed > since` y `revision_created <= since`. Son
  las que el cliente pudo tener y que salieron después de su cursor.
- Una clave **nunca** aparece en las dos listas. Si está en altas, se quita de
  bajas, así el cliente la vuelve a descargar en vez de borrarla. Esto pasa cuando
  un producto se desactiva y se reactiva.
- Una fila creada y removida después de `since` no aparece en ninguna lista: el
  cliente nunca la tuvo.
- Sin `since`: sincronización completa. Todas las vigentes en altas, bajas vacío.
- `since` mayor que la revisión actual: `400`. Ese cursor no lo emitió el servidor.

**El conjunto son las imágenes de los productos activos.** Desactivar un producto
genera una baja de su imagen; reactivarlo genera un alta. Por eso el conjunto es el
mismo en todos los dispositivos y no depende de en qué estado esté cada teléfono.

**Consistencia de la lectura.** Quien escribe mantiene el bloqueo del contador hasta
confirmar. Quien lee lee el contador (V) y las filas en una misma transacción de
lectura consistente, y solo devuelve filas con revisión menor o igual a V. Cuando el
lector ve V, todas las revisiones hasta V ya están confirmadas, así que el cursor
que devuelve es exacto.

### 9. Quién puede hacer qué

| Operación | Acceso |
|---|---|
| `PUT /api/admin/products/{id}/image` | Administrator |
| `DELETE /api/admin/products/{id}/image` | Administrator |
| `POST /api/billing/product-images/sync` | Cualquier sesión válida |
| `GET /api/billing/products` | Cualquier sesión válida |

Las tres operaciones nuevas se declaran en `RoleRequirements`, que es el punto
único del sistema donde se lee la exigencia de rol de cada operación (RF-CA-05).

El catálogo es global: no lleva filtro por sucursal, igual que hoy.

### 10. Límites de petición

El proyecto **no tenía ningún límite de tamaño de petición**: Kestrel acepta 30 MB
por defecto y los carga en memoria antes de que la aplicación decida. Se fija:

| Límite | Valor |
|---|---|
| Cuerpo general de la aplicación | 1 MiB |
| Cuerpo de la ruta de subida | 5 MiB + 64 KiB de cabeceras multipart = 5 373 952 bytes |
| `FormOptions.MultipartBodyLengthLimit` | 5 373 952 bytes |
| Lectura del archivo | Se corta al superar 5 MiB, mientras se lee |

### 11. Las variables de entorno son todas obligatorias

Las siete variables de R2 y de imagen son obligatorias. Si falta alguna, la
aplicación **no arranca**: avisa por el nombre de la variable que falta y termina.
Es el mismo patrón que la cadena de conexión y que `PIRRY_LEDGER_PUBLIC_BASE_URL`.

**Por qué no hay valores por defecto.** Una configuración a medias es la fuente
clásica de errores que aparecen en producción y no en desarrollo: la aplicación
levanta sin almacenamiento, las rutas de imagen fallan con un `503` y nadie sabe
por qué. Con todas las variables obligatorias, el error aparece al arrancar, con el
nombre de la variable en el mensaje.

### 12. Los objetos huérfanos se aceptan por ahora

Un objeto en R2 puede quedar sin referencia cuando el borrado inmediato falla: una
transacción que no confirma, o un reemplazo o una eliminación cuyo borrado en R2
falló.

No se implementa limpieza automática en esta iteración. Los objetos huérfanos
ocupan espacio y nada más: **no** rompen ninguna imagen vigente ni exponen datos.
Los fallos se registran en el log con la clave del objeto y un código corto, nunca
con el mensaje de R2, el nombre del bucket ni el endpoint (RD-08).

El orden de las operaciones protege lo que importa: el objeto se sube **antes** de
la transacción, así que una imagen vigente nunca apunta a un objeto que no existe; y
el objeto anterior se borra **después** de confirmar, nunca antes.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Una pieza `Core.Media` | Sería un cuarto punto de contacto entre el Core y el negocio, que `AGENTS.md` no permite. Para algo que solo el negocio necesita, son cuatro proyectos y una tabla de más. |
| Que el cliente mande la lista de lo que ya tiene | Hace que el backend dependa de lo que afirma el cliente. Un cliente que miente hace que el servidor omita una imagen. |
| Que el cliente calcule el delta | Contradice lo pedido: el backend debe devolver cuáles agregar o modificar. |
| Un cursor con número de fila o una fecha | La fecha empata; el número de fila no sirve para un conjunto de datos de otra tabla. Un contador con bloqueo resuelve las dos cosas. |
| Re-codificar la imagen para quitar los metadatos | Obliga a decodificar un archivo no confiable, que es la superficie de ataque clásica de los decodificadores. |
| Una librería de imágenes | Las licencias comerciales de las más usadas hay que verificarlas antes de depender de ellas. |
| Re-encodificar y quitar los metadatos en el móvil | El móvil no es la fuente de verdad: el objeto guardado debe estar limpio, no la copia de cada teléfono. |
| Clave del objeto basada en el sha256 | Con contenido repetido, un borrado pendiente puede borrar un objeto recién subido. Una clave propia por subida elimina la carrera. |
| Firmas de 60 minutos | El cliente guarda los bytes, no la URL. Quince minutos sobran. |
| Firma por cada imagen al pedir el catálogo | Obligaría a firmar en cada apertura de la aplicación, que es justo lo que se pidió evitar. |
| Limpieza automática de huérfanos | Franklin decidió asumir los huérfanos por ahora. Añade una tabla, un servicio en segundo plano y un protocolo de reservas para un beneficio que hoy no hace falta. |
| Variables de entorno con valores por defecto | Una configuración a medias esconde el error hasta que algo falla en producción. |
| Límite de 2 MiB por imagen | No entran muchas fotos 1920×1080 en JPEG de calidad alta. |
| Validar las dimensiones en píxeles | Habría que leer la cabecera de cada formato para nada: el tope en bytes ya acota el archivo. |

## Consecuencias

**A favor**

- El bucket es privado de verdad: sin firma no se descarga nada.
- El backend no depende de lo que afirma el cliente: la versión, la clave y el delta
  los calcula el servidor.
- Un cliente que abre la aplicación pide una vez el delta y recibe solo lo que
  cambió; las firmas no se generan para lo que ya tiene.
- Las fotos del catálogo no filtran coordenadas GPS, fecha ni modelo del teléfono.
- Una imagen vigente nunca apunta a un objeto inexistente.
- Los límites de petición impiden que una subida grande agote la memoria.
- La aplicación no arranca con la configuración incompleta: el error dice qué
  variable falta.

**En contra, asumido a conciencia**

- Quedan objetos huérfanos en R2 cuando el borrado inmediato falla. Ocupan espacio y
  hay que limpiarlos en una iteración futura.
- La clave del objeto no dice qué imagen es (no lleva el sha256): hay que abrir el
  objeto para saberlo. El `version` de la base sí lo dice.
- Un archivo que cumple la estructura se guarda aunque el cliente no lo pueda
  mostrar, porque no se decodifica.
- Sin limpieza automática, un día el bucket puede tener muchas imágenes viejas.
- La firma de 15 minutos obliga al cliente a volver a pedirla si una descarga se
  corta y se reanuda mucho después.

## Qué queda fuera

- El cliente móvil: la caché local, el `image_picker` del Administrador y las
  miniaturas en el mostrador. Es la iteración siguiente, con el contrato de este ADR.
- Miniaturas y varios tamaños de la misma foto.
- Limpieza automática de los objetos huérfanos.
- Poda del historial de revisiones.
- Límites de tasa para las subidas y para la sincronización.
- Eliminar los metadatos re-codificando la imagen.
- Re-empezar el cálculo del delta desde cero si el cliente pierde su cursor: por
  ahora, el cliente siempre puede pedir una sincronización completa.
