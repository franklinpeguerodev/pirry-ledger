# Bitácora de la Asignación 2 — Refactor previo a la siguiente iteración

- **Parte 1: refactor previo (preparación de esta asignación)** — contenido actual del archivo, escrito antes de empezar la pieza de permisos.
- **Parte 2: construcción de Gestión de permisos** — entradas que se irán anexando al final del archivo conforme se le deleguen tareas al agente para esta pieza.

## Contexto

- Repositorio: `franklinpeguerodev/pirry-ledger`.
- Asignación: 2 — refactor de soporte, no entrega de funcionalidad
  nueva. La fecha y el alcance de la práctica que viene los pone el
  profesor; este documento deja limpio el terreno sobre el que esa
  práctica va a entrar.
- Herramienta: agente de OpenCode en mi máquina (Windows, PowerShell
  5.1, `dotnet` SDK 10.0.302).
- Fecha: 2026-10-19.
- Rama de trabajo: `develop`. La rama que iba a contener el refactor
  (`feature/translate-code-to-english`) se quedó dos commits por detrás
  y se descartó; los veintinueve commits viven hoy en `develop`, por
  delante de `main` y a un paso del pull request de sincronización.

La regla sigue siendo la misma que en las dos bitácoras anteriores:
**lo que devuelve el agente es un borrador, no una verdad**. Nada se sube
sin que yo lo lea y lo ejecute en mi máquina, y más todavía en un pull
request que mezcla renombres de código, cambios de esquema y una
corrección de tiempo de ejecución.

---

## Por qué hace falta un refactor antes de seguir

Antes de tocar ninguna pieza nueva había tres tipos de fricción que
pagaría la próxima asignación si los dejaba como estaban:

1. **Identificadores mezclando español del enunciado e inglés ya
   madurado.** El núcleo tenía contratos en español
   (`ICorreoEnColaRepository`, `ProcesarColaDeCorreo`) mientras que el
   Host ya hablaba inglés (`PirryLedger.Host`, `ConfiguracionEntorno`).
   Quien lee el código por primera vez no sabía qué lado era la
   convención.
2. **Esquema, rutas y cuerpo de las peticiones a medio traducir.**
   Parte de la documentación decía `not_correos_en_cola` y parte decía
   `not_email_queue`; las rutas `/api/auth/recuperacion` y
   `/api/auth/recovery` aparecían en ejemplos distintos del mismo
   archivo. Si la siguiente pieza agrega un nuevo punto de acceso,
   hereda la inconsistencia.
3. **Migraciones acumuladas con nombres descriptivos en español**,
   mientras que los archivos hoy se llaman en inglés. El resultado era
   una lista de migraciones que ya no cuenta la historia del modelo.

La práctica 2 vuelve a verificar cuatro criterios de la práctica 1
(registro con activación, inicio de sesión, rechazo por rol y
recuperación de contraseña). Si esos criterios se renombran dentro de
un pull request funcional, el pull request de la práctica 2 mezcla
cambio de comportamiento con cambio de nombres y se vuelve más largo de
revisar. Hacer el refactor en su propio lote le deja el camino limpio
a la práctica 2.

---

## Qué hice

### Bloque 1 — Traducción de identificadores (diez commits)

Traduje identificadores a inglés en todo el código nuevo (`Src/` y
`Tests/`) y mantuve en español los textos visibles, los comentarios y
los nombres de las pruebas. La regla está en `AGENTS.md` y el cambio
solo aplica a nombres; ningún comportamiento cambia.

| Commit | Capa |
|---|---|
| `a0225cb` | Contratos compartidos del núcleo |
| `af2a74c` | Dominio de AccessControl |
| `bb672cc` | Capa de aplicación de AccessControl |
| `8ab7a70` | Persistencia y migraciones de AccessControl |
| `202b0ee` | Capa de API y rutas de AccessControl |
| `d7b3a19` | Identificadores y esquema de Notifications |
| `e0a79c6` | Configuración del Host |
| `2e11c59` | Ayudantes de pruebas en inglés (nombres de pruebas en español) |
| `69d97ad` | `README.md` con rutas y esquema nuevos |

Decisiones puntuales del bloque:

- **Las pruebas conservan el nombre en español.** `DarseDeAlta_doble`
  explica mejor lo que prueba que `Register_double`. Las variables,
  los dobles y los datos auxiliares van en inglés.
- **`Bearer`, `Authorization` y `application/json`** se quedaron como
  están: son nombres del estándar HTTP, no traducciones propias.
- Las claves de OpenAPI (`401`, `403`, `404`, `409`, `500`) y los
  nombres de campo de la respuesta de error (`message`) no se tocaron:
  son los que el cliente espera.

### Bloque 2 — Reorganización por capas (`632afaf`)

Cada módulo quedó con cuatro carpetas (`Domain`, `Application`,
`Infrastructure`, `Api`) y la raíz de cada proyecto solo expone el
ensamblado. Es el commit anterior al bloque 1 y la base sobre la que
se renombró cada archivo. RD-02 (la lógica vive en dominio y
aplicación, no en los controladores) y RD-03 (el núcleo no referencia
a Business) se siguen cumpliendo: la regla mental de "borrar Business y
que el núcleo compile" sigue pasando con la nueva estructura.

### Bloque 3 — Migraciones nuevas

Borré las migraciones con nombre en español y dejé una sola
`InitialSchema` por contexto, aplicadas con el orden documentado en el
`README.md`. La base de desarrollo se recreó limpia
(`DROP SCHEMA public CASCADE; CREATE SCHEMA public;`) porque ya estaba
autorizada la pérdida de los datos del escenario de prueba.

| Contexto | Migración |
|---|---|
| AccessControl | `20261018191406_InitialSchema` |
| Notifications | `20261018213308_InitialSchema` |

El esquema resultante sigue el ADR 004 (`snake_case`, prefijos `ac_`
y `not_`) y los nombres nuevos en inglés: `ac_users`, `ac_sessions`,
`ac_activation_tokens`, `ac_recovery_codes`, `not_email_queue`. La
decisión de borrar las migraciones anteriores es consciente: dejarlas
solo le recordaría a quien lee el código que las tablas cambiaron de
nombre, no cómo es el modelo hoy. Una `InitialSchema` limpia dice
"este es el estado que entrega el proyecto en este lote de commits".

### Bloque 4 — Corrección de `--send-mail` (RF-NOT-12)

Durante la traducción se añadió por descuido una llamada a
`HasConversion<string>()` sobre la propiedad `Status` de la entidad
`EmailQueueItem`. La columna pasó de `integer` a `varchar`, pero la
consulta en SQL crudo de `ClaimPendingAsync` siguió comparando con un
número entero mediante `(int)EmailStatus.*`. El resultado, en cuanto
alguien intentó ejecutar el comando, fue:

```text
Npgsql.PostgresException 42883: el operador no existe: character varying = integer
```

Decisión: **restaurar el comportamiento previo** (entero),
regenerar la migración de Notifications y revertir las cuatro
apariciones del `README.md` que documentaban la conversión a texto.
Las dos alternativas que barajé antes de elegir:

- **A (rechazada):** convertir el valor del parámetro a texto en el
  SQL crudo (`'Pending'` en lugar de `(int)EmailStatus.Pending`).
  Cambiaría dos líneas del repositorio a cambio de preservar una
  conversión que no aporta nada (los valores son internos).
- **B (aceptada):** quitar `HasConversion<string>()` y dejar la columna
  como entero, exactamente como estaba antes del refactor.

Elegí B porque es la cantidad mínima de cambios para volver al
comportamiento exigido por RF-NOT-09 y RF-NOT-12. Commit
`6aade1a`.

### Bloque 5 — Regresión HTTP (`c963c92`)

Actualicé `PirryLedger.Host.http` (rutas, cuerpos y consultas SQL al
esquema nuevo) y verifiqué el archivo entero contra la API en
ejecución. El resultado del barrido está documentado más abajo, en
[Qué verifiqué](#qué-verifiqué).

Sobre el escenario del archivo: en la sesión de hoy el escenario pasó
del placeholder `ana@ejemplo.com` a mi correo personal
(`juniorpujol2048@gmail.com`), porque era la única cuenta con
credenciales reales disponibles en el `.env` y la regresión necesita
una cuenta válida para los pasos de recuperación y de forzado. No es
una credencial, sino una dirección de correo, y queda aquí registrado
por transparencia. Si alguien prefiere revertirlo al placeholder antes
de abrir el pull request, lo cambiamos en un commit aparte.

---

## Decisiones que documenté

Ninguna amerita un ADR nuevo en `docs/adr/`. La regla del idioma ya
está en `AGENTS.md`; el `snake_case` ya está en el ADR 004; la
`InitialSchema` nueva es coherente con la decisión de borrar lo
viejo. La corrección de `--send-mail` es restauración de
comportamiento, no un nuevo compromiso de diseño, así que va en esta
bitácora y no en un ADR.

---

## Errores que aparecieron en este refactor

### Conversión a texto que rompió el envío de correo (RF-NOT-12)

Causado por una línea añadida durante la traducción que no cumplía
ningún requisito. Se detectó en cuanto intenté correr `--send-mail`
sobre el árbol traducido. La resolución está en el Bloque 4. Coste:
cero correos enviados; cuatro correos del escenario quedaron en cola y
se perdieron al recrear la base del Bloque 3.

### Cuatro correos del escenario quedaron pendientes

Antes de la corrección ejecuté la regresión HTTP por primera vez, que
encola correos de activación y de recuperación. Al recrear la base
limpia para regenerar la migración, esas cuatro filas se perdieron
junto con el resto del esquema. La pérdida estaba autorizada desde el
Bloque 3, así que no requiere acción adicional, pero conviene tenerla
en cuenta: los correos que `--send-mail` encoló durante la regresión
del 2026-10-19 no llegaron a salir.

### Riesgo corregido en este lote: rutas en español en el `README.md`

El `README.md` tenía dieciséis rutas con nombre en español que ya no
existían en la API (`/api/auth/reenviar-activacion`,
`/api/auth/recuperacion`, `/api/auth/restablecer-password`,
`/api/auth/cambiar-password`,
`/api/admin/usuarios/<id>/forzar-restablecimiento`,
`/api/admin/usuarios/<id>/desactivar`,
`/api/admin/usuarios/<id>/reactivar` y `/api/admin/usuarios`,
más la variable PowerShell `$reenviar` y la frase suelta "nace en
`Pendiente`"). Esas instrucciones ya no funcionaban contra la API y
la corrección entra en este mismo lote, en el commit
`7e0cf67 Translate README endpoints and examples to English routes`.
La prosa en español (verbos como "reenviar") se queda en español
según `AGENTS.md`.

---

## Qué verifiqué

Todos los comandos se ejecutaron hoy contra el árbol final.

### Compilación

```text
dotnet build pirry-ledger.slnx --nologo -v q
Resultado: 8 Advertencia(s), 0 Errores.
```

Las ocho advertencias son NU1902 y NU1903 sobre
`System.Security.Cryptography.Pkcs 10.0.0` y son preexistentes.

### Pruebas automatizadas

```text
dotnet test pirry-ledger.slnx --nologo -v q
Resultado: Superado: 84 (AccessControl) + 6 (Notifications), 0 con error.
```

RF-CA-01 a RF-CA-22 se cubren con pruebas automatizadas; las dos
piezas nuevas (recuperación y administración) tienen su propia
suite. RF-NOT-12 (envío único) se prueba en código, pero la
verificación real sale del comando `--send-mail`.

### Esquema de base de datos

Sobre la base `pirry_ledger` con las dos migraciones aplicadas:

```sql
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'not_email_queue'
  AND column_name IN ('status', 'attempts');
```

Resultado: `status | integer`, `attempts | integer`. Antes de la
corrección, la columna `status` aparecía como `character varying`.

### Envío de correo (`--send-mail`)

```text
dotnet run --project Src/Host/PirryLedger.Host -- --send-mail

Correos tomados: 0
Correos enviados: 0
Correos fallidos: 0
No habia correos pendientes de enviar.
```

Antes de la corrección, la misma línea de comando lanzaba `42883`. La
sentencia `UPDATE ... SELECT` de `ClaimPendingAsync` se ejecuta
siempre, también con la cola vacía, así que el camino roto queda
cubierto.

### Regresión HTTP completa

Con la API arriba y la base recién migrada, se corrió
`PirryLedger.Host.http` de principio a fin:

```text
Peticiones: 53 | No cumplen lo esperado: 0
```

Cubre los cuatro grupos: registro, activación, sesión, recuperación,
restablecimiento forzado, administración de usuarios y el bloqueo de
la cuenta tras cinco intentos. Nota: correrlo dos veces seguidas sin
recrear la base da el `409` esperado del registro duplicado (paso 2);
es la respuesta correcta, no una regresión; el escenario se
restablece con el bloque de reinicio del propio archivo.

Tras el commit `7e0cf67` (las rutas del `README.md` pasadas a
inglés) se volvió a correr la regresión sobre una base recién
migrada y el resultado se mantuvo en
`Peticiones: 53 | No cumplen lo esperado: 0`: la guía escrita y la
guía ejecutable convergen en los mismos puntos de acceso.

---

## Qué NO incluye este lote

- **Ninguna funcionalidad nueva.** Sin inventario, sin auditoría, sin
  vista administrativa de la cola, sin reintentos y sin cola de
  muertos. Todo eso sigue siendo del alcance de las semanas
  siguientes.
- **Ninguna pieza del módulo de Business.** La máquina de estados de
  la `Invoice` se queda como está; sus pruebas son de la semana 8.
- **Ninguna decisión de ADR nueva.** Las decisiones tomadas caben en
  `AGENTS.md` o ya estaban en los ADR 001 a 007.
- **Ninguna rotación de la contraseña de aplicación de Gmail** que
  haya quedado en commits antiguos del historial. Hay que cambiarla;
  el borrado del historial, si se hace, va en otro movimiento.
- **El arreglo de la cabecera del `.gitignore`** (sigue diciendo
  ".NET (microservices) + Angular"). Es su propio pull request.
- **No entran aquí las quince referencias a rutas en español del
  `README.md`**, ni la entrada de bitácora de este pull request: van
  en un commit previo, cuando se autoricen.

---

## Lo que quiero aprender para la Asignación 2

1. **Un refactor no es "cambiar nombres y ya".** El Bloque 4 nació
   precisamente de cambiar un nombre sin pensarlo: la conversión a
   texto del `enum` rompió una ruta que no estaba ni en la lista de
   cosas a tocar. La próxima vez, cualquier renombrado tiene que
   revisar, como mínimo, **el SQL crudo y los `FromSql` y
   `ExecuteSqlRaw` adyacentes** a la columna renombrada.
2. **El borrado de migraciones pide recrear la base, no solo generar
   la nueva.** Si dejo la base vieja y genero una migración que
   agrega la `InitialSchema` pensando que es lo mismo, los datos del
   escenario van a quedar inconsistentes entre lo que dice la
   migración y lo que ven las consultas del `README.md`. El patrón
   limpio es el del Bloque 3: `DROP SCHEMA`, recrear y aplicar las
   migraciones.
3. **Una regresión que necesita credenciales reales no se puede
   automatizar de verdad sin una cuenta real o sin un doble de
   servidor SMTP.** Documentado en el escenario del archivo:
   mientras no haya un servidor SMTP en memoria, la regresión se
   queda en la mitad (encolar y leer los códigos, no enviar). Eso es
   válido para verificar RF-NOT-08, RF-NOT-09 y RF-NOT-12, pero no
   para verificar RF-NOT-13 (los fallos vuelven a pendiente) sin
   credenciales que fallen de verdad. Es un hueco que la siguiente
   práctica puede llenar.
