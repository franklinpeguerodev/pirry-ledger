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

## Parte 2 — Construcción de Gestión de permisos

### Entrada 1 — Reorganización documental para la Asignación 2 (2026-10-19)

**Qué se pidió.** Rearmar la documentación del repositorio para preparar la Asignación 2 sin tocar código: mover el brief de Práctica 1 a la nueva carpeta `docs/assignments/`, reescribir el alcance en `docs/current-iteration.md`, crear el índice de ADR en `docs/adr/README.md`, y actualizar el árbol y la tabla de ADR del `README.md` (corrigiendo el rango a 001–007 y añadiendo el ADR 007). Once pasos en commits atómicos en inglés, sin push hasta tu aprobación. Ajustes posteriores: el README no lleva cifras de pruebas (escribe "todas las pruebas pasan" + el comando), la bitácora se queda en `docs/bitacoras/` y la nota pre-tag sobre la ruta se añade por separado.

**Qué devolví.** Una rama `docs/asignacion-2-scope` desde `develop` (`ce6ff0a`) con nueve commits atómicos en inglés: `ab6a0a1` (brief de la Asignación 2 y `permissions.md`), `abe6402` (`git mv` de `practice-1.md`), `01a78be` (`entities.md` con `SolicitudPermiso`), `a162944` (índice de `docs/requirements`), `2573224` (scope de `current-iteration`), `17bbdd8` (`AGENTS.md` con sección Testing y subdivisión del lenguaje), `86bacd1` (árbol y tabla de ADR del `README.md` con "todas las pruebas pasan"), `810e315` (índice de los siete ADR) y `dab224d` (índice Parte 1/Parte 2 en la bitácora). La rama no se ha pusheado; dentro de esta misma serie faltan esta entrada de la Parte 2 (este commit) y la nota pre-tag en `docs/current-iteration.md` sobre la ruta del logbook antes del tag `asignacion-2`.

**Qué verifiqué.** Cada commit se stageó individualmente y te mostré `git diff --staged` antes de ejecutarlo; aprobaste cada uno con "si". El `git grep` por `docs/requirements/practice-1.md` solo encontró `docs/current-iteration.md:85`, que ya apuntaba a la nueva ruta, así que el `git mv` no dejó enlaces rotos. Para reescribir la línea "Resultado esperado" del paso 5 del `README.md` corrí `dotnet test pirry-ledger.slnx --configuration Release --nologo`: todas las pruebas pasan (AccessControl y Notifications, 0 con error). El índice de ADR se generó leyendo la primera línea y la fecha de cada archivo; ningún ADR fue modificado — el `git diff --staged` del commit 8 solo añadió el nuevo `docs/adr/README.md`. La inserción bajo el título de la bitácora es puramente aditiva: el `git diff --staged` del commit 9 añadió tres líneas sin tocar ninguna previa. `git log --follow` sobre `docs/bitacoras/bitacora-asignacion-2.md` devuelve `1262416` (2026-10-19, Franklin Peguero, "Open Assignment 2 logbook with refactor summary") y `e4dd961` (mismo autor y fecha, "Update bitacora-asignacion-2 noting README route fix"); `git blame` confirma que las 340 líneas de la Parte 1 quedan atribuidas a esos commits.

### Entrada 2 — Documentación de la demostración de RF-GP-07 (2026-10-20)

**Qué se pidió.** Reemplazar el punto 2 de "Decisions taken by Franklin" en `docs/current-iteration.md` por un texto que describe RF-GP-07 (la regla compara identidades, no roles) y reemplaza el cuerpo de "Open decisions (ask, do not assume)" por "None for this iteration." Sin ADR, sin tocar código, sin commit directo a `develop`. El commit sale en una rama desde `develop`, con `git diff --staged` revisado antes de ejecutar `git commit` y push solo con autorización explícita. Cuando la rama base (develop) se verifique, debe contener los dos reemplazos antes de seguir con la pieza.

**Qué devolví.** Rama `docs/record-gp-07-demonstration` desde `develop` (`7ce15f3`). Commit del cambio: `9475b79 Record how RF-GP-07 is demonstrated`. Cambios: dos reemplazos en `docs/current-iteration.md` (punto 2 corto por el texto completo con RF-GP-07, bullet de "Can an Administrator create a request?" por "None for this iteration."), todo lo demás del archivo intacto. Antes del commit, `git diff --staged --stat` confirmó 1 archivo, 2 inserciones y 2 eliminaciones; tras el commit, `git status` quedó limpio y `git diff` sobre `Src/` y `Tests/` salió vacío (cero cambios fuera del archivo modificado). Una vez autorizado el push, la rama se subió a `origin/docs/record-gp-07-demonstration` apuntando a `9475b79`.

**Qué verifiqué.** Que el cambio vive donde tiene que vivir y no se ha colado en otra parte por descuido, que es justo lo que la instrucción pedía antes de seguir con la pieza:

- `git show develop:docs/current-iteration.md` después del commit: punto 2 sigue siendo la versión corta (`A Standard user creates requests; an Administrator approves or rejects them.`) y "Open decisions" sigue con el bullet sobre "Can an Administrator create a request?". `develop` no recibió el cambio.
- `git branch --contains 9475b79`: solo `docs/record-gp-07-demonstration`. Ni `develop`, ni `main`, ni `origin/develop`.
- `git log --oneline develop..HEAD`: solo `9475b79`. La rama está exactamente un commit por encima de `develop`, nada más.
- `git diff develop..HEAD -- docs/current-iteration.md`: solo los dos reemplazos pedidos, ninguna otra línea tocada.

La base del plan de la pieza (`develop` en `7ce15f3`) sigue siendo la de antes del cambio. La única manera de que llegue a `develop` es por el merge del PR abierto desde esta rama, que es lo que la instrucción exigía.

### Entrada 3 — Invalidación de sesiones al cambiar el role (ADR 009) (2026-10-20)

**Qué se pidió.** Implementar ADR 009 en código: `User.ChangeRole` debe incrementar `CredentialVersion`, igual que `ChangePassword` y `Deactivate`, para que un cambio de role (en particular una promoción) invalide las sesiones abiertas y el usuario tenga que re-login. Como esto contradice el comportamiento previo (`ChangeRole` no incrementaba), el cambio se documentó en `docs/adr/009-role-changes-invalidate-sessions.md` antes de tocar código. La prueba existente `CambiarDeRolNoInvalidaLaSesion` (en `PruebasDeRolYEstado`) afirmaba el comportamiento viejo y debe actualizarse: el nombre y el cuerpo del test se invierten para reflejar la nueva regla. La firma de `ChangeRole(Role)` se mantiene (no se añade un parámetro `utcNow` que no se usa en el cuerpo: sería código muerto; `CredentialVersion` no necesita timestamp porque el incremento es atómico).

**Qué devolví.** Commit `9592ad9 Invalidate sessions on role change (ADR 009, RF-CA-08)` en la rama `chore/accesscontrol-permissions-foundations` desde `develop` (`10ef228`). Tres archivos tocados:

- `Src/Core/PirryLedger.Core.AccessControl/PirryLedger.Core.AccessControl.Domain/Entities/User.cs`: `ChangeRole(Role newRole)` ahora hace `Role = newRole; CredentialVersion++;`. Comentario actualizado para explicar la regla y referenciar ADR 009.
- `Tests/PirryLedger.Core.AccessControl.Tests/Credencial/PruebasDeRolYEstado.cs`: el test `CambiarDeRolNoInvalidaLaSesion` se renombra a `CambiarDeRolInvalidaLaSesion` y su cuerpo se invierte: tras `ChangeRole(Role.Standard)`, la sesión anterior queda invalidada y `Authenticate.ExecuteAsync(token)` lanza `InvalidSessionException` en vez de pasar.
- `Tests/PirryLedger.Core.AccessControl.Tests/Credencial/PruebasDeAdministracionDeUsuarios.cs`: nuevo test `ElCambioDeRolIncrementaCredentialVersion` que verifica que `user.CredentialVersion` pasa de `versionInicial` a `versionInicial + 1` tras `ChangeRole(Role.Standard)`.

`ChangeUserRole` (Application) y `FakeUserRepository.Create` no se tocan: ambos ya llaman a `user.ChangeRole(role)` con la firma anterior, que sigue siendo válida.

**Qué verifiqué.** El patrón pedido por AGENTS.md: la prueba se ve fallar por la razón correcta antes de pasar.

- Pre-cambio: corrí `dotnet test --filter "CambiarDeRolInvalida|ElCambioDeRolIncrementa"`: ambas pruebas fallan. `ElCambioDeRolIncrementaCredentialVersion` falla con `Expected: 1, Actual: 0` (la versión no se incrementa). `CambiarDeRolInvalidaLaSesion` falla con `No exception was thrown` (la sesión no se invalida).
- Post-cambio: tras añadir `CredentialVersion++` al cuerpo de `ChangeRole`, ambas pasan.
- Suite completa: `dotnet test pirry-ledger.slnx --nologo` da **91 verde, 0 con error** (85 AccessControl — el viejo test se reemplazó por el nuevo, mismo conteo más uno — y 6 Notifications). Ninguna prueba existente se debilitó; las 84 AccessControl que ya existían siguen verdes.
- `git grep CredentialVersion Src/Core/PirryLedger.Core.AccessControl/PirryLedger.Core.AccessControl.Domain/Entities/User.cs` muestra exactamente tres puntos de incremento: `ChangePassword` (línea 177), `Deactivate` (línea 203) y `ChangeRole` (línea 214). El mecanismo es uniforme, como exige la decisión.

### Entrada 4 — Mudanza parcial de `Operation` y `AccessLevel` a `PirryLedger.Core.Contracts.Auth` (2026-10-20)

**Qué se pidió.** Aplicar el primer paso del plan §6 opción B (mover `Operation`, `AccessLevel`, `Requirement`, `RoleRequirements`, `WithAccess` y `OperationMetadata` de `PirryLedger.Core.AccessControl.*` a `PirryLedger.Core.Contracts.Auth`) antes de empezar la pieza de Permissions. La mudanza completa es necesaria para que el módulo de Permissions pueda declarar rutas y casos de uso en la misma tabla de exigencias de acceso, sin duplicar el filtro.

**Qué devolví.** Mudanza parcial en el commit `06d928f Move Operation and AccessLevel enums to Contracts (RF-CA-05)`. Lo que se movió y lo que no:

- **Movido a `Src/Core/PirryLedger.Core.Contracts/Auth/Operation.cs`**: el enum `Operation` y el enum `AccessLevel`. Son tipos de datos puros que no dependen de nada de AccessControl.
- **Borrado**: `Src/Core/PirryLedger.Core.AccessControl/PirryLedger.Core.AccessControl.Application/Helpers/Operation.cs`.
- **NO se movió (desviación del plan)**: `RoleRequirements` (la tabla que empareja operaciones con niveles), `Role` (el enum), `WithAccess`, `OperationMetadata`, `AccessFilter` (incluyendo `ApplyAsync` y `RequireDeclaredOperationAsync`).

**Desviación explicada.** Mudar `RoleRequirements` a `Contracts` exigiría que `Contracts` referencie `AccessControl.Domain.Enums` (por `Role`) o mover `Role` también. Mudar `Role` arrastra cambios en `User.cs`, en `AccessControlDbContext`, en todas las configuraciones EF, en los `FakeUserRepository` de los tests y en los seeds: una cascada mucho mayor que el alcance de esta iteración. Mudar `WithAccess`/`ApplyAsync` a `Contracts` crearía una dependencia circular: `ApplyAsync` (que vive en el filtro) llama a `Authenticate.ExecuteWithRoleAsync` (que vive en `AccessControl.Application`); mover `ApplyAsync` obligaría a `Contracts` a referenciar `AccessControl.Application`, invirtiendo la dirección de dependencia. Para desacoplar `ApplyAsync` de `Authenticate` hace falta introducir una interfaz `IAuthenticator` en `Contracts` con implementaciones en `AccessControl.Application` y (en su día) en `Permissions.Application`. Es factible y se deja como "futuro"; en esta iteración no es necesario.

**Consecuencia operativa.** `PirryLedger.Core.Permissions.Api` (en PR #38) declarará sus rutas con `.WithAccess(Operation.X)`. Para ello referenciará `PirryLedger.Core.AccessControl.Api` por `WithAccess` (igual que ya referencia `PirryLedger.Core.AccessControl.Application` para `Authenticate`). Es la dependencia cruzada entre módulos del Core que el plan §6 quería evitar y que se mantiene hasta que `IAuthenticator` se introduzca.

**Archivos tocados.** 12 archivos, +14/-8 (más un rename detectado por git al 91%):

- Nuevo: `Src/Core/PirryLedger.Core.Contracts/Auth/Operation.cs` (contiene `Operation` y `AccessLevel`).
- Borrado: `Src/Core/PirryLedger.Core.AccessControl/PirryLedger.Core.AccessControl.Application/Helpers/Operation.cs` (git lo cuenta como rename del 91%).
- Modificados con `using PirryLedger.Core.Contracts.Auth;` añadido: `RoleRequirements.cs` (Application), `OperationWithoutRoleRequirementException.cs` (Application), `ChangeUserRole.cs`, `DeactivateUser.cs`, `ForcePasswordReset.cs`, `ListUsers.cs` (Application), `AccessFilter.cs`, `EndpointRegistration.cs` (Api), `PruebasDelPuntoUnicoDeAcceso.cs`, `PruebasDeAdministracionDeUsuarios.cs`, `PruebasDeAccesoHttp.cs` (Tests).

**Riesgo principal:** la prueba `PruebasDelPuntoUnicoDeAcceso` reflexiona sobre `RouteEndpoint` reales del Host. La mitigación que acordamos (mover el tipo tal cual, solo cambia el namespace; la prueba solo añade el `using`) funciona.

**Verificación:**

- `dotnet build pirry-ledger.slnx` con 0 errores.
- `dotnet test pirry-ledger.slnx` con todos los tests verdes: **91 verde, 0 con error** (85 AccessControl — la mudanza no añade ni quita tests — y 6 Notifications).
- `dotnet test --filter "FullyQualifiedName~PruebasDelPuntoUnicoDeAcceso"` → **14 verde**. La prueba que enumera las rutas reales del Host sigue funcionando: sigue leyendo `OperationMetadata` (que sigue en `AccessFilter.cs`, en `PirryLedger.Core.AccessControl.Api.Auth`) y los tipos que el metadato referencia (`Operation`, `AccessLevel`, ahora en `PirryLedger.Core.Contracts.Auth`). El conjunto de rutas reales no cambia; la prueba los enumera, los cruza con `RoleRequirements.DeclaredOperations()`, y comprueba que cada entrada del enum tiene una ruta y viceversa.
- `git grep -n "Application\.Helpers"` → cero resultados en `Src/` y `Tests/`. La mudanza está limpia.

**Documentación en el mismo PR:**

- `README.md`: el árbol del repositorio menciona ahora que `PirryLedger.Core.Contracts` contiene también `Auth (Operation, AccessLevel)`.
- `docs/current-iteration.md`: nuevo punto 5 en "Decisions taken by Franklin" que documenta la decisión parcial, explica la desviación del plan §6 opción B y registra la consecuencia operativa para `Permissions.Api`.

### Entrada 5 — Esqueleto de `PirryLedger.Core.Permissions` (2026-10-20)

**Qué se pidió.** Empezar la pieza de Gestión de permisos. El plan §3.2 "Estructura del módulo, su proyecto de pruebas, la solución y las referencias" define los cinco proyectos (Domain, Application, Infrastructure, Api) más el proyecto de pruebas, todos sin lógica de negocio todavía. La mudanza de ADR 008 y ADR 009 ya está mergeada en `develop`, así que la base es la correcta.

**Qué devolví.** Rama `feature/permissions-skeleton` desde `develop` (`5729655`), con cinco commits atómicos:

1. `b9a993a Add permissions module skeleton (RF-GP-*)` — cinco `.csproj` nuevos (Domain, Application, Infrastructure, Api + el test project), `pirry-ledger.slnx` actualizado con las carpetas `Src/Core/PirryLedger.Core.Permissions/` y `Tests/PirryLedger.Core.Permissions.Tests/`, y `PirryLedger.Host.csproj` con los `ProjectReference` a los nuevos Infrastructure y Api.
2. `efb446c Add permissions Domain layer (RF-GP-*, RF-NEG-09, RD-04)` — `PermissionRequestState` enum (Pending, Approved, Applied, Rejected), `PermissionCatalog` con la constante `ViewSalesSummary`, `PermissionRequestStateMachine` con la tabla de transiciones (`Pending→Approved`, `Approved→Applied`, `Pending→Rejected`) y `InvalidPermissionRequestTransitionException`, y las entidades `PermissionRequest` y `PermissionGrant` con sus fabricas (`Create`) y mutaciones (`Approve`, `MarkApplied`, `Reject`) que delegan a la máquina de estados.
3. `195dce3 Add IPermissionAuthority contract (RF-GP-05)` — la interfaz `IPermissionAuthority` en `PirryLedger.Core.Contracts/Permissions/` con `IsKnown` y `UserHasPermissionAsync`.
4. `349fa0a Add permissions application ports` — `IPermissionRequestRepository` y `IPermissionGrantRepository` en `Application/Ports/`. Las implementaciones EF se añaden en el siguiente commit.
5. `a96774e Add permissions infrastructure and migration (RF-GP-*, ADR 008, ADR 004)` — `PermissionsDbContext`, las configuraciones EF (`PermissionRequestConfiguration` con snake_case, FK por nombre de tabla a `ac_users` sin propiedad de navegación, índices por `state` y `requester_id`; `PermissionGrantConfiguration` con índice único `(user_id, permission_name)` segundo ADR 008), las implementaciones `EfPermissionRequestRepository` y `EfPermissionGrantRepository`, la implementación `EfPermissionAuthority` que usa el catálogo y la tabla de concesiones, `DependencyInjection.AddPermissions(connectionString)` que registra el `DbContext` y los puertos, y la migración `20261020100523_InitialSchema` que crea las dos tablas `per_permission_requests` y `per_permission_grants` con sus FK a `ac_users`.

**Lo que NO se hizo en este PR.** Casos de uso (`CreatePermissionRequest`, `ApprovePermissionRequest`, `RejectPermissionRequest`, `ListOwnPermissionRequests`, `ListPendingPermissionRequests`), endpoints HTTP, ruta de demostración `/api/permissions/sales-summary`, y entradas en `RoleRequirements` para `Operation.CreatePermissionRequest`/`ListOwnPermissionRequests`/`ListPendingPermissionRequests`/`ApprovePermissionRequest`/`RejectPermissionRequest`/`ViewSalesSummary`. Todo eso entra en PR #39.

**Lo que se verificó.**

- `dotnet build pirry-ledger.slnx --nologo -v q` con 0 errores (las 8 advertencias NU1902/NU1903 son preexistentes del paquete Pkcs).
- `dotnet test pirry-ledger.slnx --nologo` con **100 verde, 0 con error** (85 AccessControl + 6 Notifications + 9 Permissions nuevas).
- `dotnet test --filter "FullyQualifiedName~MaquinaDeEstados"` → 9 verde. Las nueve pruebas (Pending a Aprobada, Pending a Rechazada, Aprobada a Aplicada, Pending directo a Aplicada lanza, Aprobada a Rechazada lanza, Applied terminal, Rejected terminal, transición al mismo estado lanza, las dos máquinas no comparten proyecto) cubren cada celda de la tabla de transiciones de RF-GP-*.
- `dotnet build pirry-ledger.slnx` con cero referencias rotas: `git grep "PirryLedger.Core.AccessControl.Domain"` desde `Src/Core/PirryLedger.Core.Permissions/` devuelve cero resultados. El módulo de Permissions no ve entidades de AccessControl; las FK se declaran por nombre de tabla en la migración.

**Documentación en el mismo PR.**

- `README.md`: el árbol de `Src/Core/` muestra ahora los cinco módulos; un aviso bajo el mapa de requisitos indica que la Asignación 2 está en marcha y que el alcance vigente para el resto del README sigue siendo el de Práctica 1.

### Entrada 6 — Pieza completa de Gestión de permisos (2026-10-20)

**Qué se pidió.** PR #39 (`feature/permissions-pipeline`) sobre `develop` (`75c66ee`). El plan §3 fija los casos de uso (RF-GP-01..04), la regla de auto-resolución por identidades (RF-GP-07), la aplicación automática en la misma transacción (RF-GP-05), las seis rutas HTTP, la ruta de demostración con `ViewSalesSummary` y la suite completa de pruebas.

**Qué devolví.** Tres commits atómicos más uno de docs:

- `6a1ecb7 Add Permission access level to auth machinery (RF-GP-05, RF-GP-07)` — `AccessLevel.Permission` con un nuevo campo `Permission: string?` en `Requirement`; `Operation` recibe `CreatePermissionRequest`, `ListOwnPermissionRequests`, `ListPendingPermissionRequests`, `ApprovePermissionRequest`, `RejectPermissionRequest` y `ViewSalesSummary`; `RoleRequirements` declara cada uno; `RequiredPermission` se añade al lado de `RequiredRole`; nueva excepción `InsufficientPermissionException` (403); `Authenticate.ExecuteWithPermissionAsync(token, permissionName)` consulta `IPermissionAuthority.UserHasPermissionAsync` y lanza 403 si false; `AccessFilter.ApplyAsync` maneja el nuevo nivel (mismo orden de 401→403 que role); `TokenBearer` se hace `public` (la alternativa era moverlo a `Contracts`, apuntado como trabajo futuro); DI de AccessControl inyecta `IPermissionAuthority`; tests de AccessControl añaden `FakePermissionAuthority` y enumeran `MapPermissions()`.
- `2483524 Add permissions use cases and endpoints (RF-GP-01..07)` — `PermissionRequestDtos` con `ListedPermissionRequest` y `PermissionRequestMapping`; excepciones `PermissionRequestRejectedException`, `InvalidPermissionRequestTransitionApplicationException` y `SelfResolutionRejectedException`; los cinco casos de uso (`Create`, `ListOwn`, `ListPending`, `Approve`, `Reject`); la ruta de demostración `/api/permissions/sales-summary` que devuelve datos fijos; `EndpointRegistration.MapPermissions()` con las seis rutas declaradas con `.WithAccess(Operation.X)`; DI de Permissions registra los cinco casos de uso; `Program.cs` llama a `AddPermissions(connection)` y `MapPermissions()`.
- `532d38f Add full permissions test suite (RF-GP-01..07)` — diez pruebas de casos de uso que cubren: aprobar pendiente con `Applied + INSERT en grants` (RF-GP-03 + RF-GP-05), aprobar dos veces (RF-GP-06), aprobar inexistente (RF-CA-12), auto-resolución directa (Admin) y realista (Estándar ascendido — RF-GP-07), rechazar con motivo (RF-GP-04), rechazar sin motivo, rechazar auto-resolución, listar propias (RF-GP-02 caso propio), listar pendientes (RF-GP-02 caso Administrador). Cada prueba con tres fases, independientes, con `FakeClock`, `FakePermissionRequestRepository`, `FakePermissionGrantRepository` y `FakePermissionAuthority`. La creación de la solicitud (RF-GP-01) se cubrió después, en la [Entrada 10](#entrada-10--rechazo-controlado-de-la-entrada-y-cobertura-de-rf-gp-01).

**Lo que NO se hizo en este PR.** Revocación de permisos (futura). Notificaciones al solicitante y al resolverdor (semana 11). Auditoría completa (semana 14). Lotes pendientes en la ruta `sales-summary` (la ruta devuelve datos fijos; no consulta base de datos). Pruebas a nivel de endpoint HTTP (las pruebas son a nivel de caso de uso; el endpoint HTTP queda cubierto por la prueba `TodaOperacionDeLaTablaTieneUnaRuta` de AccessControl, que ahora también recorre `MapPermissions()`).

**Lo que se verificó.**

- `dotnet build pirry-ledger.slnx --nologo -v q` con 0 errores.
- `dotnet test pirry-ledger.slnx --no-build --nologo` con **110 verde, 0 con error** (85 AccessControl + 6 Notifications + 19 Permissions: 9 de la máquina de estados y 10 de casos de uso nuevas).
- `dotnet test --filter "FullyQualifiedName~TodaOperacionDeLaTablaTieneUnaRuta"` → 1 verde. La prueba reflexiona las rutas reales del Host tras añadir `MapPermissions()`: las seis nuevas entradas de `RoleRequirements` están cubiertas por sus rutas correspondientes; si alguien añadiera una operación al enum sin ruta, la prueba falla con la frase "estas operaciones están en la tabla pero ninguna Route las usa" y enumera cuál falta.
- `dotnet test --filter "FullyQualifiedName~AprobarUnaSolicitudPendiente"` → 1 verde: tras aprobar, la solicitud queda en `Applied` con `ApproverId` igual al `Admin.Id`, `ApprovedAtUtc` y `AppliedAtUtc` rellenos, y una fila nueva en `per_permission_grants` (RF-GP-05 + ADR 008).
- `dotnet test --filter "FullyQualifiedName~UnEstandarAscendidoNoPuede"` → 1 verde: el camino realista de RF-GP-07 — un Estándar crea, el Admin lo asciende (`User.ChangeRole` incrementa `CredentialVersion` por ADR 009), el usuario re-inicia sesión ya como Administrator, intenta aprobar su propia solicitud y es rechazado por identidad (`request.RequesterId == caller.Id`).
- `dotnet test --filter "FullyQualifiedName~RechazarSinMotivo"` → 1 verde: el motivo vacío lanza antes de tocar la solicitud.
- `dotnet test --filter "FullyQualifiedName~UnEstandarSoloVeSusPropiasSolicitudes"` → 1 verde: el filtro `requesterId == caller.Id` se aplica en el caso de uso, no en el endpoint.
- `git grep -n "Application\.Helpers"` → cero resultados: la mudanza del PR #37 sigue limpia.

**Documentación en el mismo PR.**

- `README.md`: nueva sección "Endpoints de la Asignación 2 (gestión de permisos)" con las seis rutas y un párrafo explicativo de que el resto del README sigue describiendo Práctica 1.

### Entrada 7 — Concesión duplicada: el `23505` que la aprobación anterior no vio (2026-10-20)

**Qué se pidió.** Cerrar la pieza de permisos antes del tag: reproducir el
script de regresión HTTP contra la base local y corregir lo que fallara.

**Qué devolví.** El guion HTTP de regresión devolvió un `500` en el segundo
bloque de aprobación. La causa estaba en `PermissionGrantRepository.SaveAsync`:
insertaba una fila nueva cada vez, sin mirar si el par `(user_id,
permission_name)` ya existía. Como el índice único
`ux_per_permission_grants_user_permission` (ADR 008) rechaza el segundo
`INSERT`, la segunda aprobación de un permiso que el usuario ya tenía moría
con `23505`, y el endpoint lo devolvía como `500` sin explicación.

El arreglo busca primero el par y, si existe, re-sella la fila con
`PermissionGrant.Regrant` en vez de insertar. La decisión vive en el
repositorio porque es una cuestión de persistencia; la entidad solo expone el
re-sello.

**Lo que se verificó.**

- La prueba nueva cubre el repositorio real sobre un contexto EF Core en
  memoria, no un doble: sin el arreglo, dos de las tres pruebas fallan con
  `Assert.Single() Failure: The collection contained 2 items`, que es
  exactamente la fila duplicada. Con el arreglo, las tres pasan.
- Se añadió `Microsoft.EntityFrameworkCore.InMemory` **solo al proyecto de
  pruebas**; ningún proyecto de producción lo referencia.
- El proveedor en memoria **no aplica índices únicos**, y por eso no reproduce
  el `23505` por sí solo: sirve para afirmar la lógica del repositorio, no para
  reproducir el error de la base. Esa limitación quedó anotada en el mensaje del
  commit.
- Suite completa: `dotnet test pirry-ledger.slnx` → 85 + 6 + 22 = **113, 0 con
  error**.

### Entrada 8 — Una migración que EF nunca aplicaba (2026-10-20)

**Qué se pidió.** Auditar la entrega contra los criterios de aceptación antes
del tag.

**Qué devolví, y un error mío.** En la auditoría afirmé que la migración de
permisos creaba sus tablas y que el profesor no se vería afectado al arrancar la
pieza. **Era falso, y lo dije sin ejecutarlo**: leí el SQL del archivo de
migración y di por bueno que eso era lo que pasaba en la base. El error fue de
método, no de lectura: afirmé un comportamiento de ejecución sin ejecutar.

**Cómo se detectó.** Con una base de datos nueva creada para la prueba y los
tres comandos del paso 6 del `README.md`, en orden:

```text
=== dotnet ef database update --project .../AccessControl.Infrastructure ===
   Applying migration '20261018191406_InitialSchema'.
   Done.
=== dotnet ef database update --project .../Notifications.Infrastructure ===
   Applying migration '20261018213308_InitialSchema'.
   Done.
=== dotnet ef database update --project .../Permissions.Infrastructure ===
   No migrations were applied. The database is already up to date.
   Done.                                      exit=0
```

El tercer comando terminaba con éxito y **no creaba nada**. En la base solo
quedaban `__EFMigrationsHistory` (vacía), las cuatro tablas `ac_` y
`not_email_queue`.

**La causa.** El repositorio tenía un solo archivo de migración para la pieza,
sin su `.Designer.cs` ni su `PermissionsDbContextModelSnapshot.cs`. El
`.Designer.cs` es el que lleva los atributos `[DbContext]` y `[Migration]` con
los que EF descubre una migración; sin él, EF ignora el archivo **en silencio** y
responde que la base está al día. AccessControl y Notifications sí tienen sus
tres archivos.

**Cómo se corrigió.** Se borró el archivo huérfano y se regeneró la migración con
`dotnet ef migrations add InitialSchema`, que emite los tres archivos. Antes, la
fábrica en tiempo de diseño de la pieza llevaba una **cadena de conexión fija**,
así que `dotnet ef` apuntaba a otra base y nunca leía la variable de entorno; ahora
lee `ConnectionStrings__PirryLedger` por `DatabaseConfiguration.ConnectionVariable`,
igual que las otras dos piezas, y falla con el mismo mensaje si falta.

**Lo que se verificó después.**

- `dotnet ef migrations list` ahora lista la migración (antes no listaba ninguna).
- `dotnet ef migrations has-pending-model-changes` → `No changes have been made to the model since the last migration.`
- Base nueva, tres migraciones aplicadas: aparecen `per_permission_requests` y
  `per_permission_grants` con `ux_per_permission_grants_user_permission`.
- Orden inverso (permisos primero) aplicado sin error, así que el paso 6
  documenta que el orden da igual.

**Diferencia con la migración anterior, y lo que se aplaza.** La migración
anterior declaraba cuatro claves foráneas hacia `ac_users`; la regenerada **no
tiene ninguna**, porque el modelo nunca las declaró y EF solo genera lo que el
modelo dice. Las tablas, columnas e índices son idénticos, incluido el único.
La integridad de esas cuatro columnas (`requester_id`, `approver_id`, `user_id`,
`granted_by_user_id`) **pasa a la aplicación**, que es de donde salen las
identidades. Queda anotado como diferido en `docs/current-iteration.md`: declarar
las claves foráneas en el modelo por nombre de tabla, o dejarlas fuera de
permanente, es una decisión que roza la regla de que ningún módulo toca las
tablas de otro.

**Otro error mío, de medición.** Al reproducir el fallo del `23505` obtuve un
falso negativo: restauré el repositorio viejo con `Copy-Item`, que **conserva la
marca de tiempo del archivo**, MSBuild lo vio más viejo que el ensamblado, no
recompiló y ejecuté las pruebas contra el binario anterior. El síntoma fue un
"falla con el arreglo puesto" que no era real. A partir de ese momento, antes de
medir, se compila con `dotnet build --no-incremental`.

### Entrada 10 — Rechazo controlado de la entrada y cobertura de RF-GP-01

**Qué se pidió.** Una auditoría de todo el software contra lo que promete el
`README.md`, lo que promete `docs/` y lo que promete el `AGENTS.md`, y la
corrección de lo que no se cumplía. Los tres puntos que Franklin priorizó:

1. Un cuerpo JSON que no se puede leer devolvía la excepción cruda del
   framework: traza de pila, ruta absoluta del proyecto y la cabecera
   `Authorization` con el token del cliente. Y un `Content-Type` que la ruta no
   admitía devolvía un `500` con un mensaje que culpaba al cliente de algo que
   no había hecho.
2. El `README.md` prometía un contrato HTTP que no era el real: el nombre de la
   clave de error (`mensaje` en vez de `message`), los nombres de los campos de
   las respuestas, el texto de los mensajes de la consola del `--send-mail` y
   el nombre del role.
3. La pieza de permisos no tenía prueba de RF-GP-01 (la creación de la
   solicitud), que era el criterio de aceptación sin cubrir.

**Qué devolví.** Un commit en la rama `fix/http-error-handling`:

- `Src/Core/PirryLedger.Core.AccessControl/PirryLedger.Core.AccessControl.Api/Auth/ApiErrorHandling.cs`
  (nuevo): la frontera de error del API. Convierte cualquier excepción en un
  rechazo controlado con el formato `{"message": "..."}` y escribe el cuerpo
  cuando el framework responde con un status de error y sin cuerpo.
- `Src/Core/PirryLedger.Core.AccessControl/PirryLedger.Core.AccessControl.Api/Auth/AccessFilter.cs`:
  el middleware `RequireDeclaredOperationAsync` deja pasar los endpoints del
  framework (el `415 HTTP Unsupported Media Type` que selecciona el enrutador
  cuando el `Content-Type` no es el que la ruta admite) en vez de confundirlos
  con "una ruta nuestra sin operación declarada".
- `Src/Host/PirryLedger.Host/Program.cs`: registra el middleware nuevo y quita
  el `try/catch` inline. El Host sigue siendo composición.
- `Tests/PirryLedger.Core.AccessControl.Tests/Credencial/PruebasDeManejoDeErroresHttp.cs`
  (nuevo): cinco pruebas de la frontera de error.
- `Tests/PirryLedger.Core.Permissions.Tests/CasosDeUso/PruebasDeCreatePermissionRequest.cs`
  (nuevo): dos pruebas de RF-GP-01, la del camino bueno y la del rechazo, que
  además comprueba que no queda nada guardado.
- `README.md`: una sección 3.9 con el contrato de rechazo controlado y las
  filas RD-07 y RD-08 de la tabla de trazabilidad, corregidas.

**Qué verifiqué.** Con la API levantada contra PostgreSQL, en desarrollo y en
producción, y con la suite completa:

```text
dotnet build pirry-ledger.slnx --configuration Release
  0 Errores

dotnet test pirry-ledger.slnx --configuration Release
  Superado: 90 (AccessControl) + 24 (Permissions) + 6 (Notifications) = 120, 0 con error
```

Las diez peticiones de la tabla de la sección 3.9 del README, en los dos
entornos:

```text
########## PRODUCTION ##########
JSON malformado                    STATUS=400 BODY={"message":"El cuerpo de la peticion no es valido."}
cuerpo vacio                       STATUS=400 BODY={"message":"El cuerpo de la peticion no es valido."}
Content-Type form                  STATUS=415 BODY={"message":"El tipo de contenido debe ser application/json."}
SIN Content-Type                   STATUS=415 BODY={"message":"El tipo de contenido debe ser application/json."}
ruta inexistente                    STATUS=404 BODY=
id no-guid                         STATUS=404 BODY=
login mal password                 STATUS=401 BODY={"message":"Email o contrasena incorrectos."}
permiso inventado                  STATUS=400 BODY={"message":"El permiso solicitado no existe."}

########## DEVELOPMENT ##########
JSON malformado                    STATUS=400 BODY={"message":"El cuerpo de la peticion no es valido."}
cuerpo vacio                       STATUS=400 BODY={"message":"El cuerpo de la peticion no es valido."}
Content-Type form                  STATUS=415 BODY={"message":"El tipo de contenido debe ser application/json."}
ruta inexistente                    STATUS=404 BODY=
id no-guid                         STATUS=404 BODY=
permiso inventado                  STATUS=400 BODY={"message":"El permiso solicitado no existe."}
```

Las pruebas se vieron fallar por la razón correcta antes de darse por buenas,
rompiendo el código a propósito:

- Sin el `catch` de `BadHttpRequestException`, la prueba del cuerpo ilegible
  falla con `Expected: 400, Actual: 500`: la excepción cae en el `catch`
  genérico y el cliente recibe un 500 en vez de un 400.
- Con `PermissionRequest.Create` naciendo en `Approved` en vez de `Pending`, la
  prueba de RF-GP-01 falla con `Expected: Pending, Actual: Approved`.

**Qué cambió o se corrigió durante el trabajo.** Cuatro cosas:

1. **El diagnóstico del `500` por `Content-Type` llegó a la conclusión
   equivocada dos veces.** Primero asumí que era un fallo de binding y probé
   un filtro de endpoint; el filtro no lo atrapaba, porque la excepción se
   lanza por encima de la cadena de filtros. Segundo, supuse que el mensaje
   "La operacion no tiene una exigencia de acceso declarada" lo producía la
   resolución de rutas. La instrumentación temporal que se puso en
   `RequireDeclaredOperationAsync` y en `Program.cs` dio la respuesta: el
   enrutador selecciona un endpoint **del framework** (`Microsoft.AspNetCore.Http.Endpoint`,
   sin `RoutePattern` y sin metadata) con el nombre "415 HTTP Unsupported Media
   Type", y nuestro middleware lo confundía con una ruta nuestra sin operación
   declarada. Sin esa instrumentación, el arreglo habría sido una suposición.
2. **El primer arreglo no cambiaba nada en producción.** En desarrollo el
   `DeveloperExceptionPageMiddleware` activa `ThrowOnBadRequest`, así que la
   excepción se lanza y el middleware la captura; en producción no hay página de
   excepciones, el framework responde 400 **sin lanzar** y sin cuerpo, y la
   respuesta se quedaba vacía. El arreglo definitivo cubre los dos caminos:
   capturar la excepción cuando se lanza y escribir el cuerpo cuando el
   framework responde con un status de error sin cuerpo.
3. **Un fallo mío en la primera versión de las pruebas.** Leía el cuerpo de la
   respuesta con `MemoryStream.ToString()`, que devuelve el nombre del tipo y
   no lo escrito: las tres pruebas fallaban con "Sub-string not found" aunque el
   middleware funcionase. Ahora se lee con `Encoding.UTF8.GetString(cuerpo.ToArray())`.
4. **Un corte deliberado que no compiló.** Para probar que la prueba del 400
   fallaba por la razón correcta, se cambió el `catch (BadHttpRequestException)`
   por `catch (InvalidOperationException)`, y el compilador lo rechazó
   (`CS0160`: una cláusula `catch` previa ya detecta todas las excepciones de
   este tipo). Se cambió por un filtro `when (error.StatusCode == 999)`, que
   compila y no se cumple nunca.

**Qué NO se hizo en este commit.** El `README.md` se corrigió solo en lo que
cambia el comportamiento de la API (la sección 3.9 y las filas RD-07 y RD-08).
Los doce literales que el README prometía y la API no cumplía, y el resto de
los documentos desactualizados, van en los commits siguientes de la misma
rama de trabajo.

### Entrada 9 — Rechazar sin cuerpo devuelve 500 (2026-10-20)

**Qué se pidió.** Corregir en el guion HTTP las expectativas que no coincidían
con lo que responde la API, ejecutándola antes.

**Qué se vio.** Al recorrer la pieza con la API levantada, una petición de
rechazo **sin cuerpo** respondió:

```text
500 {"message":"La operacion no tiene una exigencia de acceso declarada."}
```

Con cuerpo presente y el motivo vacío, el `400` controlado sí llega. El
problema es que el parámetro de cuerpo de esa ruta no es opcional: el binding
falla antes de que corra el caso de uso y el filtro de acceso responde con su
mensaje de exigencia no declarada. Es una violación de RD-07/RD-08 (entrada
inválida produce un rechazo controlado, nunca una excepción sin manejar) y
contradice la restricción explícita de `docs/current-iteration.md`.

**Qué devolví.** Se intentó hacer opcional el cuerpo de la ruta y la prueba no
dio el resultado: la respuesta siguió siendo `500`, y el JSON malformado
devolvió un `400` **con traza de excepción** en el entorno de desarrollo. Como
el tiempo de la entrega estaba encima, se revirtió el cambio en vez de dejar a
medias un arreglo sin verificar, y el guion HTTP documenta esa petición como
defecto conocido en lugar de presentarla como resultado esperado. RF-GP-04 no se
ve afectado: exige motivo escrito, y el rechazo sin motivo escrito responde
`400`.

**Lo que NO se hizo.** El arreglo de la ruta y su prueba. Queda pendiente para
después del tag, junto con lo que ya estaba diferido.

**Cómo terminó.** La [Entrada 10](#entrada-10--rechazo-controlado-de-la-entrada-y-cobertura-de-rf-gp-01)
resuelve el defecto: un cuerpo ausente o mal formado responde `400` con un
mensaje controlado y un `Content-Type` que la ruta no admite responde `415`
con un mensaje controlado. Ninguno de los dos casos devuelve ya una traza. El
guion HTTP se actualizó con esas respuestas.

### Entrada 12 — El README dice lo que la API hace (2026-10-24)

**Qué se pidió.** Corregir el `README.md`: doce sitios donde el documento
prometía un contrato HTTP que la API no cumplía, y cinco sitios donde la
descripción del repositorio se había quedado atrás.

**Qué devolví.** Un commit en la rama `docs/readme-contract-fixes`, sobre
`develop`. Solo toca el README y esta bitácora; no cambia el comportamiento de
la API.

Los doce literales, todos verificados contra la API levantada antes de
corregirlos:

| El README decía | La API responde |
|---|---|
| `{"mensaje":"La cuenta no esta activada."}` | `{"message":"La cuenta no esta activada."}` |
| `200 {"activado":true}` | `200 {"activated":true}` |
| `{"enviado":true}` | `{"sent":true}` |
| `/me` devuelve `nombre` | `/me` devuelve `name` |
| `email o password incorrectos.` | `Email o contrasena incorrectos.` |
| `emails tomados / enviados / fallidos` | `Correos tomados / enviados / fallidos` |
| `No habia emails pendientes de enviar.` | `No habia correos pendientes de enviar.` |
| `Los emails siguen en la cola…` | `Los correos siguen en la cola…` |
| `Los emails enviados no se volveran…` | `Los correos enviados no se volveran…` |
| `[usuario SMTP]` y `[password SMTP]` | `[User SMTP]` y `[contrasena SMTP]` |
| `servidor de email (RF-NOT-13)` | `servidor de Email (RF-NOT-13)` |
| `Se creo el primer Administrador…` | `Se creo el primer Administrator…` |

Los cinco sitios de descripción:

- El conteo de tablas: son **siete** tablas de módulo, no ocho. La octava fila
  de la consulta es `__EFMigrationsHistory`, que crea EF Core y no es de ninguna
  pieza.
- El diagrama de arquitectura no tenía el módulo de permisos, ni la
  dependencia cruzada entre `Permissions` y `AccessControl` que sí existe en
  los `.csproj`. Los dos están ahora en el diagrama y la dependencia con su
  explicación.
- La frase "`Program.cs` solo hace `app.MapAccessControl()` y
  `app.MapNotifications()`" no mencionaba `app.MapPermissions()`, y daba a
  entender que la pieza de notificaciones expone rutas: `MapNotifications()` no
  mapea ninguna, porque el envío de correo es un comando.
- El árbol del repositorio listaba dos proyectos de pruebas de tres, y la
  tabla de trazabilidad decía que solo AccessControl y Notifications tienen
  suite.
- El diagrama de la base de datos no incluía las dos tablas de permisos. Ahora
  están, con la nota de que sus cuatro columnas de usuario **no** tienen clave
  foránea.

**Qué verifiqué.** Con la API levantada contra PostgreSQL, las tres respuestas
que el README cita literalmente:

```text
POST /api/auth/login (cuenta inactiva)   401 {"message":"La cuenta no esta activada."}
POST /api/auth/login (correo inexistente)  401 {"message":"Email o contrasena incorrectos."}
POST /api/auth/resend-activation          202 {"sent":true}
```

Y una búsqueda de los doce literales viejos en el README, que ya no devuelve
ninguno.

**Qué NO se hizo en esta entrega.** La línea del listado administrativo
(`id`, `name`, `email`, `role`, `active`, `createdAtUtc`) la cambia el pull
request #43, porque el campo `role` también cambia ahí. Los ADR, la bitácora de
Práctica 1, `AGENTS.md` y los siete sitios de código con texto en inglés o en
chino van en los pull requests siguientes.
