# ADR 016 - Full payload signing for R2 uploads

- **Estado:** aceptada
- **Fecha:** 2026-11-10
- **Requisitos:** RD-07, RD-08, RD-10; corrige la nota de implementación del ADR 015
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

El ADR 015 decidió subir las imágenes a Cloudflare R2 hablando el protocolo de S3 y
firmando con SigV4. La primera subida real falló y se anotó, en la nota de
implementación de ese ADR, que el SDK de AWS enviaba un checksum en un trailer
(`aws-chunked`, `STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER`) y que R2 no lo
implementaba. La corrección que se aplicó entonces fue fijar
`RequestChecksumCalculation` y `ResponseChecksumValidation` en `WHEN_REQUIRED`.

El 2026-11-10 la subida volvió a fallar con el mismo mensaje genérico
(`No se pudo subir el objeto … al almacenamiento de imagenes`) y se investigó con
una prueba aislada que usa el mismo SDK y las mismas credenciales. El error real
es:

```
Amazon.S3.AmazonS3Exception
  StatusCode: 501
  ErrorCode: NotImplemented
  Mensaje de R2: STREAMING-AWS4-HMAC-SHA256-PAYLOAD not implemented
```

Ese mensaje **no** es el del trailer. Es la firma **por bloques** de SigV4. Venía
ocurriendo desde el principio: `WHEN_REQUIRED` quita el trailer del checksum, pero
no quita el troceado del cuerpo, así que la anotación del ADR 015 atribuía el fallo
a una causa que no era la única, y la corrección aplicada no lo resolvía.

Se comprobó con el SDK, contra el bucket real, que el mismo archivo se sube sin
error en tres configuraciones distintas:

| Configuración | Resultado |
|---|---|
| La del ADR 015 (con `WHEN_REQUIRED`) | Falla: `STREAMING-AWS4-HMAC-SHA256-PAYLOAD not implemented` |
| `PutObjectRequest.UseChunkEncoding = false` | HTTP 200 |
| `PutObjectRequest.DisablePayloadSigning = true` | HTTP 200 |
| Las dos anteriores a la vez | HTTP 200 |

## Decisión

La subida fija `UseChunkEncoding = false` en la petición, y el cliente deja de
fijar `WHEN_REQUIRED`:

- `UseChunkEncoding = false` **desactiva la firma por bloques**. El SDK envía el
  cuerpo en una sola pieza y lo firma con su sha256 (`x-amz-content-sha256`), que
  es la forma de SigV4 que R2 entiende.
- `RequestChecksumCalculation` y `ResponseChecksumValidation` vuelven a su valor
  por defecto, porque el ajuste no era la causa y no aportaba nada una vez que la
  firma viaja entera.
- El ajuste vive en la petición (`CreatePutObjectRequest`), no en el cliente,
  porque es una propiedad de `PutObjectRequest`, no de `AmazonS3Config`.

**Por qué no `DisablePayloadSigning`.** También hace pasar la subida, pero deja el
cuerpo como `UNSIGNED-PAYLOAD`: el contenido del archivo queda fuera de la firma
SigV4. Con TLS el contenido sigue cifrado, pero se pierde la garantía de que el
cuerpo que llega es el que se firmó, y una petición capturada podría reenviarse con
otro contenido dentro de la ventana de validez de la firma. `UseChunkEncoding =
false` consigue lo mismo que hacía falta (que R2 acepte la petición) sin renunciar
a esa garantía, y con un costo despreciable porque los bytes ya están en memoria.

**Por qué no se actualiza el SDK.** Cambiar la versión es adoptar una dependencia
nueva sin saber todavía si resuelve el problema; `AGENTS.md` pide autorización
explícita para instalar paquetes y no se pidió. Si en el futuro una versión del SDK
deja de firmar por bloques con R2, esta decisión se revisa.

**Por qué no se abre como extensión del ADR 015.** El ADR 015 decide la
arquitectura del almacenamiento (adaptador, claves, firmas de descarga, cursor). Lo
que cambia aquí es **cómo se firma el cuerpo de la subida**, y eso es una pregunta
de modelo de seguridad que un revisor haría como "¿por qué no firmar el cuerpo?".
Se registra aparte y se enlaza desde la nota del ADR 015, que se conserva como
historia de lo que se intentó primero.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| `DisablePayloadSigning = true` | Deja el cuerpo como `UNSIGNED-PAYLOAD`, fuera de la firma. Funciona, pero es la opción más débil de las que funcionan. |
| Mantener `WHEN_REQUIRED` | No resuelve el fallo: el cuerpo se seguía troceando. Además el ADR 015 la presentaba como la solución, y no lo era. |
| Actualizar `AWSSDK.S3` | Exige instalar paquetes, y no hay evidencia de que otra versión cambie la firma por defecto para R2. |
| Firmar a mano cada subida, sin el SDK | Duplica lo que el SDK ya hace bien y aleja el código del resto del proyecto. |
| Cambiar el proveedor de almacenamiento | R2 cumple; el problema era el formato de la firma, no el proveedor. |

## Consecuencias

**A favor**

- La subida funciona contra R2 con el cuerpo firmado entero.
- El contenido de la imagen sigue cubierto por SigV4, y no solo por TLS.
- Se retira un ajuste (`WHEN_REQUIRED`) que no aportaba nada y que el ADR 015
  atribuía a una causa equivocada.
- La prueba nueva fija el comportamiento sin tocar la red (RD-12), y la
  verificación real se hizo aparte, contra el bucket.

**En contra, asumido a conciencia**

- La prueba del proyecto comprueba la **configuración** de la petición, no el
  comportamiento real contra R2: eso solo se puede ver con el bucket, y el
  proyecto no usa servicios externos en las pruebas (RD-12).
- Si una versión futura del SDK deja de respetar `UseChunkEncoding`, la subida
  vuelve a fallar y habrá que revisar esta decisión.

## Qué queda fuera

- El manejo completo de errores del almacenamiento (`DeleteAsync`,
  `CreateDownloadUrl`, cancelaciones y timeouts). Va en otro cambio.
- Quitar `PIRRY_LEDGER_R2_ACCOUNT_ID` como variable obligatoria: el cliente no la
  usa, pero retirarla cambia el contrato del ADR 015 y se decide aparte.
- Limpieza automática de objetos huérfanos (ADR 015, sección 12).
- Pruebas de carga y límites de tasa (ADR 015, "qué queda fuera").
