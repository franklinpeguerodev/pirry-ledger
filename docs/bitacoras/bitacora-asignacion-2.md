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
