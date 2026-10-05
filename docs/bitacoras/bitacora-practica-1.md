# Bitácora de la Práctica 1 — Control de acceso

## Contexto

- Repositorio: `franklinpeguerodev/pirry-ledger`
- Asignación: 1 — Práctica 1, *Control de acceso completo* (Programación III,
  TDS-007, ITLA, 2026-C-3). Bloque 2, semana 4, 8 puntos, individual.
- Herramienta: agente de OpenCode corriendo en mi máquina (Windows,
  PowerShell 5.1, `dotnet` SDK 10.0.302).
- Fechas: del 2026-09-29 al 2026-10-02.
- Rama de trabajo: `develop`, con una rama por funcionalidad.

Esta bitácora no sustituye a `docs/bitacoras/bitacora-asignacion-1.md`, que cubre el
`.gitignore`, el README inicial y la plantilla de pull request. Aquella dejó la
lección que rigió en esta: **lo que devuelve el agente es un borrador, no una
verdad**. Nada se sube sin que yo lo lea y lo ejecute en mi máquina.

**Sobre los huecos.** Los apartados marcados como `[POR COMPLETAR]` no
conservan la palabra textual del prompt original: ahora se reconstruyen a
partir de los commits, sus requisitos y la verificación ejecutada en cada
pieza. La fuente de esa reconstrucción es `git log` y los mensajes de
commit, no la memoria.

**Sobre quién verificó qué.** Cada apartado de pieza tiene un *Qué verifiqué* que
es de las sesiones en que esa funcionalidad se construyó, no de esta revisión de
documentos. En esta pasada solo se vuelva a correr lo que se lista en
[Qué verifiqué en esta revisión](#qué-verifiqué-en-esta-revisión), y todo lo
demás queda como estaba. No hay `gh` en esta máquina, así que los datos de los
pull requests salen de los commits de fusión, no de la API de GitHub.

---

## Cómo se construyó: ramas y pull requests

Los merges y los commits se sacaron de `git log --no-merges <merge>^1..<merge>^2`
sobre cada merge. De la rama de origen de cada pull request solo puedo afirmar lo
que dice el asunto del commit de fusión, porque en esta máquina no hay `gh` ni
`git` con acceso a la API de GitHub:

| PR | Asunto del merge | Base | Commits | Requisitos |
|---|---|---|---|---|
| #11 | `Merge pull request #11 from franklinpeguerodev/develop` | `develop` | 9 | RF-NOT-08, 09, 12, 13 · RD-08, 09, 10 |
| #12 | `Merge pull request #12 from franklinpeguerodev/feature/registro-y-activacion` | `develop` | 3 | RF-CA-01, 02, 04, 14, 15, 16, 17 · RD-05, 07, 08, 09 · RF-NOT-08 |
| #13 | `Merge pull request #13 from franklinpeguerodev/develop` | `develop` | 1 | documentación de ejecución (RF-NOT-08, 09, 12, 13 · RD-10) |
| #14 | `Merge pull request #14 from franklinpeguerodev/feature/sesion` | `develop` | 7 | RF-CA-03, 07, 18, 19 · RD-06, 12 · RD-10 |
| #15 | `Merge pull request #15 from franklinpeguerodev/develop` | `main` | 0 | publicación de `develop` a `main` |

El enunciado pide cuatro grupos de funcionalidad, y hay dos: **registro y
activación** (PR #12) y **sesión** (PR #14). La cola de correo del PR #11 también
se pedía, aunque no venga enunciada como uno de los cuatro grupos. Faltan
**recuperación de contraseña** y **administración de usuarios**.

Los PR #13 y #15 no añaden el cambio que los originó: lo integran. El #13
incorpora `3ca31c8`, que documenta los pasos de ejecución, y el #15 no introduce
ningún commit propio: es la publicación de `develop` a `main`.

---

## Correo por cola — RF-NOT-08, 09, 12, 13

**Qué se le pidió:** implementar el envío de correo por cola de modo que
ninguna operación HTTP abra una conexión SMTP (RF-NOT-08, RF-NOT-09); un
correo enviado no debe volver a enviarse (RF-NOT-12); los fallos vuelven el
correo a la cola y conservan el contador de intentos (RF-NOT-13). El prompt
exacto no se conserva, pero los commits, los identificadores y la
verificación describen lo entregado.

**Qué devolvió el agente.** Nueve commits atómicos, uno por pieza:

| Commit | Qué añadió |
|---|---|
| `37b27e0` | Contratos compartidos: `IClock`, configuración de base de datos, `IEmailQueue` |
| `dc7eaa7` | Entidad `CorreoEnCola` con sus reglas de estado (`Pendiente` → `Procesando` → `Enviado`) |
| `07531c7` | Caso de uso `ProcesarColaDeCorreo` |
| `c238b1b` | Persistencia PostgreSQL con EF Core y su migración |
| `3d39a3f` | Transporte SMTP (MailKit) |
| `c9ee785` | Comando `--send-mail` en el Host |
| `e23ac6a` | Lectura de la configuración desde `PIRRY_LEDGER_*` |
| `4aee76e` | Enmascarado de credenciales SMTP en los errores de envío |
| `d086832` | El remitente se parsea como dirección y no como nombre para mostrar |

**Qué hice yo.** Revisé los nueve commits en `git log`, verifiqué que cada
uno modifica un solo archivo o capa, y ejecuté los comandos listados en
*Qué verifiqué*. Aprobé los commits en el orden en que llegaron.

**Qué verifiqué.** Revisé la tabla por SQL siguiendo el README: una fila nace en
`Pendiente` con `intentos = 0` sin que ninguna operación abra una conexión SMTP
(RF-NOT-08); corrí el enviador dos veces seguidas y la segunda respondió
`Correos tomados: 0` sin reenviar nada (RF-NOT-12); borré
`PIRRY_LEDGER_SMTP_PASSWORD` y el correo volvió a `Pendiente` con `intentos = 1`
(RF-NOT-13).

---

## Registro y activación — RF-CA-01, 02, 14, 15, 16, 17

**Qué se le pidió:** entregar el registro con correo de activación,
almacenando la contraseña como hash con sal por usuario (RF-CA-02), el
enlace de activación con token de un solo uso y ventana corta (RF-CA-14,
RF-CA-15, RF-CA-16) y el reenvío del enlace con respuesta idéntica haya o
no correo (RF-CA-17). El prompt exacto no se conserva; los commits, sus
requisitos y la verificación describen lo entregado.

**Qué devolvió el agente.** Tres commits:

| Commit | Qué añadió |
|---|---|
| `d72c524` | Hash Argon2id y la política de contraseña |
| `c169bdb` | Entidades de control de acceso y sus tablas |
| `231db3a` | El flujo completo: registro, activación y reenvío |

**Qué hice yo.** Revisé los tres commits en `git log`, confirmé que el
flujo está cubierto por los criterios verificados y aprobé la pieza.

**Qué verifiqué.** Leí `ac_usuarios` y los hashes empiezan por
`$argon2id$v=19$m=` y son distintos entre usuarios con la misma contraseña, porque
el salt es por usuario (RF-CA-02). El correo de la cola trae el enlace con un
token de 64 hexadecimales y en `ac_tokens_activacion` solo está su SHA-256
(RF-CA-15). Abrí el enlace: `200 {"activado":true}`; lo abrí otra vez: `400`, con
el mismo `400` que un token inventado (RF-CA-16). Registré el mismo correo en
mayúsculas y respondió `409` (RF-CA-01).

---

## Sesión — RF-CA-03, 07, 18, 19

**Qué se le pidió:** cerrar la brecha de credencial que no se invalida en
el servidor (RF-CA-03), implementar el endpoint `/yo` con la triada
nombre/correo/rol (RF-CA-07), el cierre que solo cierra los pines del
usuario y deja al resto vivas (RF-CA-18), y el bloqueo tras cinco intentos
fallidos con reinicio del contador en un acierto (RF-CA-19). El prompt
exacto no se conserva; los commits, el ADR 001 y la verificación describen
lo entregado.

**Qué devolvió el agente.** Siete commits:

| Commit | Qué añadió |
|---|---|
| `b955e70` | El ADR de la credencial de sesión |
| `ebef007` | Entidad `Sesion` y su tabla |
| `19d9007` | El hash señuelo se calcula una vez por hasher, no por intento |
| `3e46a9c` | Inicio y cierre de sesión, y un único punto de validación |
| `ca4fb1f` | Las pruebas de sesión y la documentación del flujo |
| `85a77b6` | Aclaración de la regla de idioma: los ADR son entregables en español |
| `a4a14ee` | Corrección de la introducción del README |

**Qué hice yo.** Autoricé la decisión del ADR 001 antes de que se
escribiera, revisé los siete commits y ejecuté las cinco verificaciones de
la sección siguiente. Aprobé la pieza.

**Qué verifiqué.** Correo inexistente y contraseña equivocada dan el mismo `401`
con el mismo cuerpo, y el caso de uso verifica la contraseña contra un hash
señuelo para que el tiempo de respuesta tampoco distinga (RF-CA-03). Cerré
sesión y la credencial dejó de servir, sin cerrar las demás del mismo usuario
(RF-CA-18). Cinco intentos fallidos bloquearon la cuenta y el sexto, con la
contraseña correcta, se rechazó (RF-CA-19).

---

## Decisiones

`docs/adr/001-session-credential.md`, aceptada el 2026-09-30: credencial
opaca, tabla `Sesion` y `CredencialVersion` copiada por sesión.

El ADR guarda el razonamiento entero, incluida la comparación con JWT y una lista
de revocación, que es lo que hace falta defender. Lo que sí registro aquí es el
método: antes de escribirlo, el agente detuvo la implementación y presentó las
alternativas con sus trade-offs. La decisión fue mía, el documento lo redactó el
agente.

---

## Errores y correcciones

### 1. Dos commits con el mismo cambio

`12646d3` y `062ff63` tienen el mismo asunto, el mismo autor, la misma fecha y el
mismo *patch-id* (`2e9e9f1d`): son el mismo cambio escrito dos veces.

```
git show 12646d3 -- AGENTS.md | git patch-id --stable
git show 062ff63 -- AGENTS.md | git patch-id --stable
```

`12646d3` cuelga de `a6035bb`, que no está en `main`, y solo existe en la rama
local `chore/update-repo-state`, que no está en el remoto.

### 2. Un commit que llegó a `develop` sin pull request

`062ff63` es la punta de `develop` y de `origin/develop`, y ningún commit de
fusión lo envuelve. Eso es compatible con dos situaciones: que llegara por un
pull request fusionado en *fast-forward*, cuyo merge no deja commit, o que se
commiteara directo a `develop`. En esta máquina no hay forma de distinguirlas,
porque hacer falta la API de GitHub y no hay `gh` instalado. Lo dejo anotado como
**no comprobado**, no como infringe la regla.

Lo que sí es cierto y no depende de GitHub: el mismo cambio está dos veces en el
repositorio, y una de las dos copias vive solo en una rama local que nadie más
tiene (el error 1). La rúbrica califica el historial y los pull requests con
1 punto, así que conviene confirmarlo desde GitHub antes de la entrega.

No lo corregí: arreglarlo exigiría reescribir el historial, y nunca se hace un
`push` forzado sin mi autorización. Queda anotado aquí para no sorprender al
profesor.

### 3. `AGENTS.md` afirmando cosas que dejaron de ser ciertas

La sección *Verified repo state* decía que no había `DbContext`, ni migraciones,
ni endpoints, ni pruebas. Los hay: dos contextos, tres migraciones, seis
endpoints y 31 pruebas. También decía que la fusión con `main` no se había
publicado, y sí lo estaba.

La sección se añadió el 2026-09-29, antes de que existiera el código que después
la contradecía: no fue un descuido al escribirla, fue que describe un momento y
un momento se queda viejo. Por eso ahora `AGENTS.md` solo tiene reglas
permanentes y manda a `docs/current-iteration.md`, al README y a `docs/adr/` por
el estado.

### 4. La revisión de consistencia (este pull request)

Pedí una revisión de todos los documentos contra el código y contra los
requisitos. Salieron veinte hallazgos, agrupados así:

| Grupo | Cuántos | Qué era |
|---|---|---|
| `AGENTS.md` desactualizado | 10 | Estado, ramas, puerto, decisiones ya resueltas, ruta inexistente |
| README que no se cumplía | 8 | Número de endpoints y rutas, orden de las migraciones, requisitos que faltaban, salidas de consola incompletas, un criterio sin forma de probarlo, una errata |
| Brechas de alcance | 5 | `docs/maquina-de-estados.md` inexistente, no hay forma de crear un Administrador, RF-CA-05 sin punto único de rol, contraseñas y administración sin construir, etiqueta `practica-1` sin crear |
| Comentarios de código que no describen el código | 7 | El más serio: `Sesion.cs:63-66` promete que el método devuelve *por qué* se rechaza, y su firma es `bool` |

Este pull request arregla los dos primeros grupos y dos huecos de alcance en los
documentos. Los comentarios de código y las brechas quedan pendientes.

### 5. Arreglé un error del README con otro error

El README pedía comparar "cinco peticiones" al endpoint de reenvío, y la lista no
tenía cinco estados distintos: repetía el mismo caso dos veces con otras palabras.
Mi primer arreglo no fue mejor. Añadí dos peticiones más y las llamé "cuenta
activa con token vivo" y "cuenta activa con token ya usado", creyendo que eran
casos separados.

Son el mismo caso. Activar la cuenta es precisamente lo que deja el token sin
usar, así que una cuenta activa nunca tiene un token vivo que reenviar. El caso
de uso tiene cuatro salidas, no cinco: correo mal formado, correo inexistente,
cuenta ya activa y cuenta pendiente. Lo corregí después de leer
`ResendActivationLink.cs` y de comparar las cuatro peticiones de verdad.

La lección es la del error 3 de esta misma lista, en pequeño: un documento que
nunca se ejecuta sostiene errores que parecen razonables. El README se ve bien
y está mal, y no se nota leyendo.

### 6. Escribí en el README una línea de salida que el programa no imprime

Al documentar el comando `--send-mail` puse que los correos fallidos quedaban
"como pendientes" con esas palabras. La línea real del código es otra:

```
Los correos siguen en la cola como pendientes y se volveran a intentar la proxima vez.
```

Me di cuenta al ejecutarlo y leer `Program.cs`. También faltaban dos cosas que sí
salen: el `Acquiring an exclusive lock for migration application` en cada
`dotnet ef database update`, y el `Failed executing DbCommand` con el `SELECT ...
FROM "__EFMigrationsHistory"` que EF Core imprime en una base recién creada antes
de aplicar nada. Ninguno es un error, y sin embargo todo el mundo lo leería como
un error si el README no lo dice.

### 7. Di por roto un código que estaba bien

Este me costó seis intentos y una conclusión falsa sostenida con seguridad. Gmail
rechazaba el envío con `5.7.9 Please log in with your web browser and then try
again`, y afirmé tres veces que la contraseña era inválida o que el de correo con
MailKit estaba mal. Las dos cosas eran falsas: la contraseña servía desde el
primer intento y el transporte nunca falló.

**La causa real.** Yo lanzaba el comando exportando tres variables de entorno:
la de conexión, la de la URL base y la de la contraseña SMTP. Las otras cinco
—servidor, puerto, seguridad, usuario y remitente— nunca llegaban al proceso. La
aplicación arrancaba con `SmtpConfiguracion.Vacia`, leía las otras variables de
la sesión anterior o no leía nada, y se autenticaba contra Gmail con credenciales
vacías. Por eso el `5.7.9` era idéntico en los seis intentos: no estaba cambiando
la contraseña, estaba mandando siempre la misma, que era ninguna.

**Cómo me engañé.** Escribí una sonda aparte con `System.Net.Mail` y con MailKit,
y las dos autenticaron bien. Leí ese resultado como "el código de la aplicación
está mal y la credencial es buena". No comparé las dos rutas: la sonda leía las
variables directamente del entorno y la aplicación las leía de un proceso donde yo
no las había exportado. Estaban probando cosas distintas y yo las comparé como si
fueran lo mismo.

**Lo que costó.** No fue el tiempo de los seis intentos, fue la conclusión. Si
hubiera comparado primero qué valores recibía cada ruta, el problema se veía en
un minuto. Y como afirmé con tanta seguridad algo falso, la conversación se fue
en defender una hipótesis que ya estaba descartada por mi propia sonda.

**La lección, y ya es la tercera vez que sale en esta bitácora.** Es la del error
5 y la del error 3 juntas: nada de esto se descubre leyendo. Se encontró
ejecutando las dos rutas una al lado de la otra y preguntándose en qué se
diferencian. Un resultado que confirma lo que ya sospechabas no es una
verificación, es una coincidencia.

---

## Qué verifiqué en esta revisión

Todo lo de arriba es de las sesiones en que cada pieza se construyó. Lo de abajo
lo ejecuté en esta revisión, porque el README quedó con salidas de consola nuevas
y no se documenta nada sin correrlo antes.

**Compilación y pruebas**

```
dotnet build pirry-ledger.slnx     -> 0 errores, 0 advertencias
dotnet test pirry-ledger.slnx      -> 90 superadas, 0 fallidas
```

En la revisión inicial el proyecto de pruebas de Notifications todavía no tenía
pruebas detectables. Después se añadió una suite unitaria para la entidad de
cola y el procesador; el envío SMTP real continúa verificándose manualmente.

La suite nueva cubre seis casos: creación pendiente, validación del destinatario,
bloqueo de re-reclamo de un correo enviado, envío único en ejecuciones sucesivas,
devolución a `Pendiente` cuando falla el transporte y rechazo de lotes no
positivos.

**Migraciones.** La base de desarrollo ya estaba al día, así que creé una base
desechable para ver lo que ve alguien que clona el repositorio por primera vez.
Las tres migraciones se aplicaron en el orden que dice el README, y la de
Notifications va aparte, con su propio contexto. Luego borré la base.

```
dotnet ef database list ... --project Src/Core/PirryLedger.Core.Notifications/PirryLedger.Core.Notifications.Infrastructure
  Applying migration '20260930191411_CrearTablaCorreosEnCola'.
```

**La aplicación arranca y responde**

```
Now listening on: http://localhost:5243
Application started. Press Ctrl+C to shut down.
```

| Comprobación | Resultado real |
|---|---|
| Ruta inexistente | `404` |
| `/yo` sin cabecera | `401` `{"mensaje":"Sesion no valida."}` |
| `POST /api/auth/register` | `202` `{"activado":false}` |
| Login con la cuenta **inactiva** y la contraseña correcta | `401` `{"mensaje":"La cuenta no esta activada."}` |
| Login con la contraseña incorrecta | `401` `{"mensaje":"Correo o contrasena incorrectos."}` |
| Reenvío, los **cuatro** casos | `202` `{"enviado":true}` en los cuatro |
| `GET /activar?token=...` | `200` `{"activado":true}` |
| Login con la cuenta ya activada | `200` con token de 43 caracteres |
| `GET /yo` con el token | `200` con el correo del usuario |
| `POST /api/auth/logout` | `204`, y `/yo` vuelve a `401` |

El caso de login con la cuenta inactiva es el que motivo el quinto error de esta
bitácora: la respuesta es un mensaje propio, y solo se distingue porque la
contraseña se verifica primero. Está en el README como ejemplo de por qué hay que
verificar el orden de las operaciones.

**El enviador de correo, primero el fallo y después la entrega.** Con credenciales
que no llegaban al proceso (el error 7), 19 correos pendientes y así respondía:

```
Correos tomados: 19
Correos enviados: 0
Correos fallidos: 19
  - <correo>: No se pudo entregar el correo a <correo> por smtp.gmail.com:587. 534 5.7.9 ...
Los correos siguen en la cola como pendientes y se volveran a intentar la proxima vez.
```

Los 19 volvieron a `Pendiente` con `intentos` incrementado y `fecha_envio_utc`
vacío: ninguno se perdió y no se fingió ningún envío. Es el comportamiento que el
README promete y el que RF-NOT-08 pide, y se comprobó ocho veces seguidas.

Exportadas las seis variables del servidor de correo, el correo llegó a una
dirección real:

```
Correos tomados: 1
Correos enviados: 1
Correos fallidos: 0
Los correos enviados no se volveran a enviar aunque se ejecute el comando otra vez.
```

En `not_correos_en_cola` el registro quedó con `estado = 2` y `fecha_envio_utc`
asignada. La segunda ejecución del comando tomó `0` correos, que es RF-NOT-12:
reenviar no duplica.

**Una prueba que salió mal y sirve de advertencia.** Para aislar el fallo escribí
una sonda aparte con MailKit 4.18.1 y con `System.Net.Mail`, y **las dos
autenticaron bien** cuando la aplicación fallaba. Parecía la prueba de que el
código estaba roto. No lo estaba: la sonda leía las variables del entorno y la
aplicación leía un proceso donde no estaban exportadas. Estaban probando rutas
distintas y las comparé como si fueran la misma. Lo que de verdad faltaba era un
`echo` de qué valores recibe cada ruta.

---

## Entradas por pull request

Cada pull request que abra durante la práctica deja aquí una entrada. El agente la
redacta y yo la reviso antes de que se suba.

### Plantilla

```
### <rama> — <qué cambia en una línea>

- **Qué se le pidió:**
- **Qué devolvió el agente:**
- **Qué verifiqué yo, y con qué comando:**
- **Qué no incluye:**
- **Qué cambió de lo que pidió:**
```

### `docs/fix-agents-verified-state` — reglas permanentes, README corregido y esta bitácora

- **Qué se le pidió:** revisar todos los documentos del repositorio contra el
  código y contra los requisitos, y devolver los hallazgos sin tocar nada.
- **Qué devolvió el agente:** veinte hallazgos en cuatro grupos, y después los
  cambios acordados: `AGENTS.md` sin estado, las cuatro reglas de documentación
  nuevas, el README con las salidas de consola reales y las migraciones en orden,
  `docs/current-iteration.md` refrescado y esta bitácora.
- **Qué verifiqué yo, y con qué comando:** todo lo de
  [Qué verifiqué en esta revisión](#qué-verifiqué-en-esta-revisión).
  `dotnet build` y `dotnet test` en verde, las migraciones sobre una base nueva,
  la aplicación en marcha y las diez comprobaciones de la tabla. Más el envío
  real de correo, que llegó a una dirección externa y quedó con `estado = 2`.
- **Qué no incluye:** comentarios de código, `docs/maquina-de-estados.md`,
  recuperación de contraseña, administración de usuarios, RF-CA-05 y la etiqueta
  `practica-1`. Sigue sin haber forma de crear el primer Administrador, y queda
  anotado como decisión abierta.
- **Qué cambió de lo que pidió:** pedía añadir dos peticiones al criterio de
  reenvío del README y le añadí dos que no distinguían ningún caso; lo corregí a
  las cuatro reales después de leer el caso de uso. Documenté también una línea
  de salida del comando de correo que el programa no imprime. Y en el camino
  diagnostiqué mal un fallo de envío: eran las variables de entorno que yo no
  exportaba al proceso, no el código. Ese error es el 7 de esta bitácora.
- **Ningún archivo de código cambió en este pull request.** El fallo del envío
  estaba en el comando con que lo lanzaba, no en el repositorio. Por eso los
  cuatro commits son solo de documentación.

---

## Administración de usuarios — RF-CA-04, 05, 06, 08, 20, 21

**Qué se pidió:** completar la administración de usuarios en la rama
`feature/user-administration`, con autorización del lado del servidor,
listado, cambio de rol, desactivación y reactivación.

**Qué se implementó:** se centralizó la exigencia de acceso en
`ExigenciasDeRol`, se añadieron las cuatro rutas administrativas y casos de uso
separados, y el listado proyecta únicamente datos públicos del usuario. La
desactivación incrementa `CredencialVersion`, por lo que invalida sesiones
abiertas. Se confirmaron además las protecciones contra cambiarse el rol,
desactivarse y dejar la aplicación sin Administrador activo.

**Qué se verificó:** las pruebas de administración y del punto único ejecutan
sin PostgreSQL. La suite completa debe quedar verde antes de proponer el
commit; la prueba temporal de login se revisó por separado porque mide el
trabajo criptográfico de dos caminos.

## Qué NO incluye todavía

- Auditoría de RF-CA-08, RF-CA-13 y RF-CA-20: corresponde a la semana 14.
- Reintentos, estado fallido, último error y consulta administrativa de la cola:
  corresponden a las semanas 11 y 12.
- Pruebas de la máquina de estados: corresponden a la semana 8.
- La etiqueta `practica-1` ya se publicó al cerrar esta práctica (ver
  [Cierre](#cierre)).
- El proyecto de pruebas de Notifications cubre la entidad de cola y el
  procesamiento con dobles en memoria; el envío SMTP real sigue siendo una
  verificación manual.

## Trazabilidad mínima de cambios de contraseña

**Qué se pidió:** conservar una fecha para poder comprobar cuándo se cambió por
última vez la contraseña del usuario.

**Qué se implementó:** se añadió `Usuario.ContrasenaCambiadaUtc`, un campo UTC
nullable persistido en `ac_usuarios`. Permanece `NULL` para la contraseña
inicial y se actualiza desde la única operación de dominio
`CambiarContrasena`, por lo que cubre recuperación, cambio autenticado y
restablecimiento forzado sin duplicar lógica. La migración agrega la columna sin
alterar las filas existentes.

**Por qué:** aporta trazabilidad mínima para diagnosticar cambios de
credenciales sin almacenar contraseñas ni convertir esta práctica en la
auditoría completa reservada para la semana 14.

**Qué se verificó:** una prueba de dominio confirma que el valor queda igual al
reloj UTC inyectado cuando se reemplaza el hash. La migración se aplicó a la
base local, el build terminó con 0 errores y 0 advertencias, y la suite de
AccessControl terminó con 84 pruebas superadas y 0 fallidas.

## ADR de trazabilidad y revisión del README — RF-CA-12, RD-09, RD-10, RD-11

**Qué se pidió:** documentar con más detalle la decisión de conservar
`ContrasenaCambiadaUtc` y comprobar que el README describiera el comportamiento
real del código, las migraciones, las pruebas, los endpoints y la configuración.

**Qué se implementó:** se creó
`docs/adr/003-password-change-traceability.md`, con título en inglés y contenido
académico en español. El ADR documenta el contexto, la decisión, la razón para
centralizar la fecha en `CambiarContrasena`, las alternativas descartadas, la
persistencia nullable, la compatibilidad con cuentas existentes, las
consecuencias, la evidencia de verificación y los límites explícitos: no se
implementa todavía una auditoría completa, historial de eventos, actor, IP,
dispositivo ni endpoint administrativo para consultar credenciales.

También se enlazó el ADR desde `docs/current-iteration.md` y desde la sección de
`ContrasenaCambiadaUtc` del README.

La revisión del README corrigió y alineó con el código real:

- la migración `20261002002636_AgregarFechaDeCambioDeContrasena`;
- el tipo nullable `timestamptz` de `ContrasenaCambiadaUtc`;
- el resultado real de las pruebas: 84 pruebas de AccessControl superadas;
- que Notifications tiene pruebas unitarias para los estados de la cola, el
  envío único, los fallos del transporte, los intentos y el lote;
- el uso de `PIRRY_LEDGER_PUBLIC_BASE_URL` en los enlaces de activación;
- las cinco rutas administrativas disponibles;
- la cabecera `Authorization` con el esquema `Bearer` del endpoint `/yo`;
- el nombre real de la entidad de negocio: `Invoice`, no `Factura`.

**Qué se verificó:** se ejecutaron los comandos contra la rama de trabajo:

```text
dotnet build pirry-ledger.slnx --configuration Release --no-restore
Resultado: compilación correcta, 0 advertencias, 0 errores.

dotnet test Tests/PirryLedger.Core.AccessControl.Tests/PirryLedger.Core.AccessControl.Tests.csproj --configuration Release --no-restore
Resultado: 84 superadas, 0 fallidas.

dotnet test Tests/PirryLedger.Core.Notifications.Tests/PirryLedger.Core.Notifications.Tests.csproj --configuration Release --no-restore
Resultado: 6 superadas, 0 fallidas.

git diff --check
Resultado: sin errores.
```

No se modificó código de ejecución en esta revisión; los cambios fueron
documentales y quedaron pendientes de commit para revisión de Franklin.

---

## Recuperación de contraseña — RF-CA-09 a 13, 22

**Qué se implementó:** solicitud de recuperación con respuesta uniforme,
códigos almacenados como SHA-256, validez de 15 minutos, consumo único,
restablecimiento que invalida sesiones, cambio autenticado que exige la
contraseña actual y restablecimiento forzado por Administrador. Los tres flujos
encolan correo y no conectan directamente con SMTP.

**Qué se verificó:** la suite de AccessControl cubre la respuesta uniforme,
vencimiento y uso único. La compilación del Host y la suite completa se
ejecutaron después de integrar esta pieza.

## Máquina de estados de negocio — RF-NEG-03, 04, 05, RD-04

Franklin decidió que la entidad central es `Invoice` y que sus estados son
`Draft`, `Issued`, `Paid` y `Cancelled`. Las transiciones viven en
`InvoiceStateMachine`; `Paid` y `Cancelled` son terminales. La tabla completa se
encuentra en `docs/maquina-de-estados.md`. Las pruebas se reservan para la
semana 8 según el alcance de Práctica 1.

## Diagrama de la base de datos

**Qué se pidió:** añadir al README una vista rápida de la base de datos, cerca
de las instrucciones de migraciones y arranque, sin sustituir la explicación
detallada de las tablas.

**Qué se implementó:** se añadió un diagrama `mermaid erDiagram` después de la
sección de preparación de la base de datos. Muestra `ac_usuarios`,
`ac_codigos_recuperacion`, `ac_tokens_activacion`, `ac_sesiones` y
`not_correos_en_cola`, con sus columnas principales, claves primarias y
relaciones de clave foránea.

El diagrama también aclara el límite entre módulos: Notifications comparte la
base PostgreSQL, pero su cola no tiene una FK hacia `ac_usuarios`. El vínculo
entre el flujo de AccessControl y la cola ocurre mediante el contrato de
encolado, no mediante una relación directa entre tablas.

**Qué se verificó:** los nombres de tablas, columnas y relaciones se
contrastaron con las configuraciones EF Core y las migraciones existentes.
`git diff --check` terminó sin errores.

## Reorganización guiada del README

**Qué se pidió:** reorganizar el README para que una persona que no conoce el
proyecto pueda clonarlo, configurarlo, ejecutarlo y probarlo siguiendo un orden
claro, sin tener que deducir qué sección debe leer primero.

**Qué se implementó:** se añadió una ruta guiada al inicio del README con nueve
pasos: instalación de requisitos, clonación, preparación de PostgreSQL,
variables de entorno, build y pruebas, migraciones, arranque de la API, primer
registro y continuación con los criterios de aceptación. La ruta enlaza las
secciones detalladas para no duplicar toda la documentación.

También se aclaró:

- qué variables son mínimas para arrancar;
- cuáles son opcionales para probar SMTP;
- qué variables permiten crear el primer Administrador;
- dónde queda disponible la API;
- qué respuesta esperar del primer registro;
- cómo obtener una sesión y consultar `/yo`;
- en qué orden continuar con las pruebas manuales.

**Qué se verificó:** los comandos documentados ya habían sido ejecutados en esta
revisión: build correcto, 90 pruebas superadas y migraciones comprobadas. En
esta modificación adicional solo se reorganizó documentación y
`git diff --check` se mantuvo sin errores.

Después se completó la trazabilidad de criterios que faltaba en la guía:
RF-CA-04, RF-CA-06, vencimiento de RF-CA-16, reinicio del contador de
RF-CA-19, separación de RF-CA-09 a RF-CA-12 y la tabla de evidencia de RD-05 a
RD-12. El plan de ejecución que había en `docs/` se eliminó el 2026-10-04 por
decisión de Franklin; las bitácoras se concentran en `docs/bitacoras/`.

También se amplió el Paso 3 del README con el SQL para crear el rol de
PostgreSQL y la base `pirry_ledger`, además del comando `psql` para conectarse
y la salida de la sesión. Los valores continúan siendo placeholders para no
documentar credenciales reales.

Después se añadió el Paso opcional 4A para configurar Gmail como servidor SMTP.
La guía usa `Read-Host -AsSecureString`, convierte la contraseña de aplicación
solo para guardarla como variable de entorno del usuario, elimina los espacios
de presentación y limpia las variables de memoria. También explica que se debe
usar una contraseña de aplicación, no la contraseña normal de Gmail, y abrir una
terminal nueva antes de iniciar la API o `--send-mail`.

Finalmente se movió la sección `Requisitos` al inicio del README, antes de la
ruta guiada. Así una persona nueva conoce primero las herramientas y versiones
necesarias antes de comenzar los pasos de instalación y ejecución.

También se aclaró la diferencia entre la herramienta global `dotnet-ef` y el
paquete `Microsoft.EntityFrameworkCore.Design`. Este último ya está declarado
en los dos proyectos de infraestructura, por lo que el usuario no debe
instalarlo manualmente; `dotnet restore` lo obtiene desde los archivos de
proyecto.

Se reorganizó la ruta de ejecución para evitar repeticiones y errores por
configuración duplicada. El README ahora ofrece dos opciones explícitas:
variables persistentes con alcance `User` o variables temporales de la terminal
actual. También indica que la creación del rol/base de PostgreSQL y la
aplicación de migraciones se hacen solo cuando corresponda, y que la opción
temporal debe repetirse en cada terminal nueva.

La instalación de `dotnet-ef` también quedó centralizada: la sección de
requisitos solo identifica la herramienta y enlaza al Paso 1, que es el único
lugar con el comando de instalación y la comprobación de versión.

---

## Qué cambió en mi forma de trabajar

1. **Un documento que describe el estado se pudre solo.** `AGENTS.md` tenía una
   sección de "estado verificado" que dejó de ser verdad. Ahora el estado vive
   en los documentos que se reescriben en cada entrega, y `AGENTS.md` solo tiene
   reglas que no dependen del código.
2. **Cruzar los documentos entre sí, no solo contra el código.** La contradicción
   más cara —los commits directos a `develop`— no se ve leyendo el código. Se ve
   comparando lo que `AGENTS.md` promete con lo que `git log` enseña.
3. **Una hipótesis comprobada es una hipótesis que se descarta.** Llegué a la
   conclusión de que `3ca31c8` era un commit directo a `develop`. Era falsa: su
   padre es `d086832`, la punta de una rama, y no el merge. Un `git log` con los
   rangos correctos lo dejó claro. Casi lo escribo en esta bitácora.
4. **Un documento que se ejecuta se revisa; uno que solo se lee, no.** Escribí
   "cinco peticiones" donde había cuatro estados, y después añadí dos más para
   cuadrar el número, sin mirar el caso de uso. Corregirlo tomó cinco minutos
   leyendo cuarenta líneas de `ResendActivationLink.cs`, y cinco minutos
   ejecutando lo que el README promete. Lo que no se ejecuta se documenta en
   función de lo que uno supone que hace el código.
5. **Documentar una salida de consola es copiarla, no redactarla.** La línea de
   los correos fallidos la escribí de memoria y el programa imprimía otra. Ahora
   la línea sale de correr el comando y copiar lo que devuelve.
6. **Antes de culpar al código, comprobar qué recibe cada ruta.** Six intentos
   buscando un bug en el transporte de correo que no existía: el fallo eran las
   variables de entorno que yo no exportaba al proceso. Cuando dos rutas dan
   resultados distintos, la pregunta no es cuál tiene la culpa, es **qué recibe
   cada una**. Y si una sonda confirma lo que ya sospechaba, esa sonda está
   midiendo otra cosa: hay que imprimir los valores, no confiar en que coinciden.
5. **Documentar una salida de consola es copiar la salida, no redactarla.** La
   línea de los correos fallidos la escribí de memoria y el programa imprimía
   otra. Ahora la línea sale de correr el comando y copiar.

---

## Unificación de nombres de columnas a snake_case — ADR 004 (2026-10-03)

**Qué se pidió.** Al revisar la base noté que `not_correos_en_cola` tenía sus
columnas en minúsculas (`fecha_creacion_utc`) mientras que las tablas de
AccessControl las tenían en PascalCase (`"Correo"`, `"HashDeContrasena"`).
Pedí evaluar cuál de los dos estilos era el correcto. Después de presentar las
alternativas, la decisión fue la **opción A: unificar todo a snake_case**,
creando un ADR, aplicando el update a la base y sin hacer commits ni tocar los
README por ahora.

**Qué se decidió y por qué.** El ADR completo está en
`docs/adr/004-snake-case-column-naming.md`; el resumen del porqué:

- **PostgreSQL pliega los identificadores sin comillas a minúsculas.** Con
  columnas PascalCase, toda consulta manual exige comillas dobles
  (`SELECT "Correo" ...`) y `SELECT Correo` falla con *column does not
  exist*. La comilla deja de ser una elección y se vuelve un requisito
  perpetuo que solo se puede recordar o equivocar.
- **Las tablas ya eran snake_case**, así que unificar las columnas deja un
  solo estilo en toda la base.
- **El PascalCase de AccessControl no lo decidió nadie**: los
  `*Configuration.cs` no declaraban `HasColumnName`, así que EF Core aplicó su
  convención por defecto. Notifications sí lo decidió a mano, con
  `HasColumnName` explícito en `CorreoEnColaConfiguration.cs`. Es decir, la
  opción A no introduce nada nuevo: le pone nombre explícito a lo que un
  módulo ya había elegido.
- **La opción B (unificar a PascalCase) se descartó** porque habría roto el
  SQL crudo de `CorreoEnColaRepository.cs` (`UPDATE not_correos_en_cola ...
  FOR UPDATE SKIP LOCKED`), que es el reclamo atómico de RF-NOT-12, y habría
  que reescribirlo y revalidar el envío único.
- Los prefijos `ac_` y `not_` **no se tocan**: son el límite de módulo a nivel
  de datos (las dos piezas comparten base, no tablas) y ya están documentados
  en `ConfiguracionDeBaseDeDatos.cs`.

**Qué se implementó.**

- `HasColumnName(...)` explícito para las 30 columnas en las cuatro
  configuraciones de AccessControl (`UsuarioConfiguration`,
  `CodigoRecuperacionConfiguration`, `TokenActivacionConfiguration`,
  `SesionConfiguration`), con un comentario que cita el ADR 004. Cuatro
  columnas que antes no estaban declaradas en absoluto (`bloqueo_hasta_utc`,
  `usado_utc` de las dos tablas de un solo uso y `cerrada_utc`) ahora tienen
  mapeo propio.
- Migración `20261003142652_UnificarNombresDeColumnas`: 30 `RenameColumn` y
  la recreación de las tres claves foráneas con nombre nuevo en minúsculas.
  Sin `DropTable`, sin cambios de tipo. `Down()` completo.
- `dotnet ef database update` aplicado a la base `pirry_ledger`.
- ADR 004, con la tabla de alternativas descartadas y las consecuencias.

**Qué verifiqué.**

- **Conteos antes y después, idénticos:** `ac_usuarios` 3,
  `ac_codigos_recuperacion` 2, `ac_tokens_activacion` 2, `ac_sesiones` 8,
  `not_correos_en_cola` 4 (psql contra la base real, antes y después del
  update).
- **Esquema:** `information_schema.columns` devuelve las cuatro tablas `ac_*`
  con todas sus columnas en snake_case minúscula.
- **Consulta estilo README, sin comillas dobles:**
  `SELECT correo, activo, left(hash_de_contrasena, 25) FROM ac_usuarios` →
  devuelve las tres filas con los hashes Argon2 intactos.
- **Runtime:** la API arrancó y su semilla generó
  `SELECT EXISTS (SELECT 1 FROM ac_usuarios AS a WHERE a.rol = 'Administrador')`
  (columnas nuevas, sin comillas) y quedó en `http://localhost:5243`; `/yo`
  sin credencial respondió `401`.
- **`dotnet build pirry-ledger.slnx --configuration Release`:** 0
  advertencias, 0 errores.
- **`dotnet test pirry-ledger.slnx --configuration Release`:** 90 pruebas
  superadas, 0 con error (las pruebas no referencian nombres de columna).

**Segunda pasada: README corregido y promovido (2026-10-03).** Franklin pidió
retomar el pendiente de arriba: corregir todo el SQL, la prosa y el diagrama que
el rename dejó obsoleto, y eliminar `README.md` para promover `readmecopy.md`
como README principal.

**Qué cambió.**

- Todas las consultas del README pasaron a snake_case sin comillas dobles
  (`SELECT cuerpo FROM not_correos_en_cola ...`,
  `UPDATE ac_tokens_activacion SET expira_utc ...`, el `JOIN` de tokens vivos, el
  listado por `fecha_de_creacion_utc`), más las referencias en prosa
  (`usado_utc`, `bloqueo_hasta_utc`, `contrasena_cambiada_utc`;
  `CredencialVersion`/`ContrasenaCambiadaUtc` se conservan donde nombran la
  propiedad de C#) y las columnas de las cuatro tablas `ac_*` en el diagrama
  Mermaid.
- El bloque de "Resultado esperado" de `dotnet ef database update` se contrastó
  contra una base recién creada: los cuatro `Applying migration` (incluido
  `20261003142652_UnificarNombresDeColumnas`) y la rama
  `No migrations were applied. The database is already up to date.`
- `readmecopy.md` reemplazó a `README.md`; el original quedó respaldado fuera
  del repositorio. El ejemplo de login dejó de usar credenciales inventadas y
  ahora referencia `<correo-del-administrador>` y `<contrasena-del-administrador>`.
- ADR 004 y esta bitácora dejaron de declarar el README como pendiente.

**Qué verifiqué (comandos ejecutados).**

- Los diez SELECT y el UPDATE/INSERT del README, uno por uno con
  `psql -v ON_ERROR_STOP=1` contra la base real (los de escritura dentro de
  `BEGIN ... ROLLBACK`): todos responden y los hashes Argon2 siguen intactos.
- JSON contra la API corriendo en `http://localhost:5243`: `GET /yo` →
  `correo, nombre, rol`; `GET /api/admin/usuarios` con token de Administrador →
  `activo, correo, fechaDeCreacionUtc, id, nombre, rol`, sin hashes ni
  `credencialVersion`.
- `dotnet ef database update` sobre una base temporal creada desde cero →
  esquema snake_case en `information_schema.columns`; base temporal eliminada.
- Conteos finales de la base real: `ac_usuarios` 3, `ac_codigos_recuperacion` 2,
  `ac_tokens_activacion` 2, `ac_sesiones` 8, `not_correos_en_cola` 4 — los mismos
  de antes de la pasada; el usuario de prueba y sus filas se borraron.

**Cambio de entorno autorizado por Franklin.** La contraseña del Administrador de
desarrollo no coincidía con `PIRRY_LEDGER_FIRST_ADMIN_PASSWORD` (ocho intentos
fallidos y bloqueo de quince minutos). Con su autorización se restableció con el
propio flujo del producto (RF-CA-09 → RF-CA-10) y se actualizó la variable de
entorno; el valor no aparece ni en este documento ni en el repositorio.

---

## Puerto de escucha desde la variable de entorno — bug 001 y ADR 005 (2026-10-04)

**Qué se pidió.** Franklin me pidió analizar el proyecto y confirmar si el bug
del puerto de escucha, registrado el 2026-10-03, era real tal
como estaba planteado. Lo es: lo verifiqué leyendo el código y corriendo la
aplicación. Después pidió arreglarlo con una indicación concreta:
`PIRRY_LEDGER_PUBLIC_BASE_URL` pasa a ser la única fuente de verdad — define el
host:puerto donde escucha la API y la dirección de los enlaces de activación —,
sin dañar, romper ni cambiar lógica, y creando la rama pertinente.

Antes de tocar código presenté el plan y cuatro decisiones; las resolvió así:
ADR para la decisión, quitar `applicationUrl` de `launchSettings.json`, escuchar
exactamente lo que diga la variable (no todos los interfaces) y actualizar el
registro de bug, la bitácora y `current-iteration.md` además del README.

**Qué devolvió el agente.**

- Rama `fix/standardize-listen-url-on-base-env-var` desde `develop`.
- `Program.cs`: la variable se lee una sola vez y ese `urlBase` alimenta
  `builder.WebHost.UseUrls(urlBase)` (escucha) y
  `AddAccessControl(conexion, urlBase)` (enlaces). Una sola fuente.
- `launchSettings.json`: `applicationUrl` fuera de los dos perfiles; solo quedan
  `ASPNETCORE_ENVIRONMENT=Development`.
- Corrección de comentarios que repetían el error del README: la recuperación no
  usa la variable (`Program.cs`, `ConfiguracionEntorno.cs`).
- `README.md`: tabla de variables (`PIRRY_LEDGER_PUBLIC_BASE_URL` fija host y
  puerto y ya no menciona enlaces de recuperación), paso 7 (de dónde sale el
  puerto, que el perfil solo fija el entorno, y cómo usarlo desde otra tablet),
  descripción del Host y el índice de `docs/`.
- `docs/adr/005-listen-address-from-public-base-url.md`, con la tabla de
  alternativas descartadas (las opciones A, B y C que se plantearon y la de
  escuchar en todos los interfaces).
- El registro del bug pasó a **Resuelto** con sus cuatro criterios de
  aceptación cumplidos y su evidencia; la carpeta `docs/bugs/` se eliminó más
  tarde por decisión de Franklin (ver la segunda pasada, abajo), y hoy lo que
  queda de eso es esta entrada y el ADR 005.
- Entrada de bitácora (esta) y línea en `docs/current-iteration.md`.

**Qué verifiqué, y con qué comando.**

- *Antes del cambio*, `PIRRY_LEDGER_PUBLIC_BASE_URL=http://localhost:5999` +
  `dotnet run --launch-profile http` → `Now listening on: http://localhost:5243`:
  la variable se ignoraba para el puerto. Ese es el bug.
- *Después*, `dotnet run --project Src/Host/PirryLedger.Host --no-build
  --launch-profile http` → `Now listening on: http://localhost:5243`,
  `Hosting environment: Development` (el recorrido del README no cambia).
- *Después*, `dotnet Src\Host\PirryLedger.Host\bin\Debug\net10.0\PirryLedger.Host.dll`
  → `Now listening on: http://localhost:5243`; antes decía 5000.
- *Después*, variable de proceso `http://localhost:5999` →
  `Now listening on: http://localhost:5999`, `GET /yo` → `401` en ese puerto y
  `GET http://localhost:5243/yo` → sin respuesta: la API sigue a la variable.
- Sonda de precedencia: `ASPNETCORE_URLS=http://localhost:5888` junto con la
  variable en 5243 → escucha 5243. `UseUrls` manda sobre `ASPNETCORE_URLS`.
- `dotnet run ... -- --send-mail` → salida idéntica a la de antes
  (`Correos tomados: 0`, `No habia correos pendientes de enviar.`).
- Sin la variable → `Falta la variable de entorno
  PIRRY_LEDGER_PUBLIC_BASE_URL. El README explica como definirla.` y código de
  salida 1, sin traza.
- `dotnet build pirry-ledger.slnx` → 0 errores, 0 advertencias.
- `dotnet test pirry-ledger.slnx` → 90 pruebas en verde (6 Notifications + 84
  AccessControl), las mismas que el baseline de antes de empezar.
- `git diff --stat` → solo tres archivos de `Src/Host/PirryLedger.Host/`; nada
  en `Src/Core`, `Src/Business` ni en `Tests/`.

**Qué verificó Franklin, y con qué comando.** Pendiente: revisa este trabajo y
los comandos anteriores antes de que se suba.

**Qué no incluye.** Nada de lógica de negocio, autenticación, correo ni pruebas;
no cambia textos de correos ni rutas; no añade validaciones ni avisos nuevos;
no decide HTTPS (el perfil `https` dejó de escuchar en 7258, queda anotado en el
ADR 005); no toca `docs/requirements/`, `.gitignore` ni bitácoras anteriores. El
arreglo no se commitea ni se sube sin su aprobación.

**Qué cambió de lo que pidió.** Nada esencial. Dos desviaciones menores que
conviene saber: (1) para poder verificar, tuve que recompilar, porque el DLL en
`bin/` estaba desactualizado respecto a la migración snake_case y la aplicación
se caía al arrancar con `no existe la columna a.Rol` — era un artefacto local,
`dotnet build` lo resolvió y no forma parte del arreglo; (2) la sonda de
precedencia confirmó que `UseUrls` gana sobre `ASPNETCORE_URLS`, así que el
README no necesita advertencia sobre esa variable.

**Segunda pasada: las dos observaciones del análisis final (2026-10-04).** Al
preguntar si completarlas rompía algún requisito, la respuesta fue que no:
`grep` en `docs/requirements/` sobre `https|TLS|SSL|cifrad|redirec|transporte|puerto|certific`
no devuelve ninguna coincidencia. Lo más parecido que existe es
`PIRRY_LEDGER_SMTP_SECURITY`, que cifra el correo saliente por SMTP y no tiene
relación con la entrada HTTP de la API.

- **`app.UseHttpsRedirection()` retirado de `Program.cs`.** Con la variable en
  `http` no hay ningún endpoint HTTPS al que redirigir, así que el middleware no
  redirigía nada: solo imprimía
  `warn: Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware[3] Failed to
  determine the https port for redirect.` en la primera petición. Se comprobó
  con `git stash` (código anterior, perfil `http`) que ese aviso ya salía antes
  de este trabajo, es decir, era preexistente y no una regresión. Queda como
  decisión y alternativa descartada en el ADR 005, punto 5.
- **`PirryLedger.Host.http`**: comentario sobre `@host` indicando que debe
  coincidir con `PIRRY_LEDGER_PUBLIC_BASE_URL`, para que la guía de regresión no
  se quede con un puerto que ya no decide launchSettings.
- **Carpeta `docs/bugs/` eliminada** (2026-10-04, decisión de Franklin). El
  registro del bug 001 ya no está en el repositorio; lo que sobrevive de él es
  el ADR 005 (contexto y alternativas), esta entrada y las referencias de
  `README.md` y `current-iteration.md`, todas ellas actualizadas para que no
  queden rutas rotas.

**Qué verifiqué en la segunda pasada, y con qué comando.**

- `dotnet build pirry-ledger.slnx` → 0 errores, 0 advertencias.
- `dotnet run --project Src/Host/PirryLedger.Host --no-build --launch-profile
  http` → `Now listening on: http://localhost:5243`,
  `Hosting environment: Development`; `GET /yo` → `401`; y
  `POST /api/auth/login` con credenciales malas → sin ninguna línea `warn`,
  `fail` o `https` en la salida (antes salía la del middleware).
- `dotnet test pirry-ledger.slnx` → 90 pruebas en verde (6 Notifications + 84
  AccessControl).

## Variables de entorno desde un archivo `.env` — DotNetEnv (2026-10-04)

**Qué se pidió.** Franklin planteó si no sería mejor tener un `.env` con todas
las variables de entorno en lugar de los once comandos
`SetEnvironmentVariable(..., 'User')` que el README pidió hasta ahora. Presenté
tres caminos con sus contras (paquete `DotNetEnv`, loader propio o script de
PowerShell) y él eligió el primero, autorizó **solo** ese paquete y pidió
actualizar README, docs y bitácora.

**Qué se decidió y por qué.**

- El `.env` se carga **antes** de `WebApplication.CreateBuilder`, así que las
  variables entran por el mismo camino de siempre: `IConfiguration` →
  `ConfiguracionEntorno`. RD-10 sigue cumplido porque al final siguen siendo
  variables de entorno, no un mecanismo de configuración distinto.
- **El entorno real manda:** `Env.NoClobber()`, de modo que una variable ya
  definida en la terminal o en el sistema no la pisa el archivo. Un despliegue
  con variables de verdad sigue funcionando igual.
- **Si no hay `.env` no es error.** El loader busca el archivo desde el
  directorio de trabajo hacia arriba y, si no lo encuentra, sigue sin más: la
  aplicación queda como estaba antes, con las variables del entorno.
- El `.gitignore` ya lo tenía previsto (`.env`, `.env.*`, `!.env.example`), así
  que `.env` nunca se sube y sí se sube `.env.example` con nombres y
  descripciones, sin valores reales.

**Qué hizo el agente.**

- `dotnet add ... package DotNetEnv` (3.2.0, paquete único autorizado; arrastra
  `Superpower` 3.0.0, que es dependencia suya).
- `Program.cs`: `using DotNetEnv;`, la llamada `CargarArchivoEnv()` antes de
  construir el `builder` y la función local que busca el `.env` hacia arriba y
  lo carga con `Env.NoClobber().Load(archivo)`.
- `.env.example` con las once variables agrupadas en obligatorias, primer
  Administrador y SMTP, con su explicación.
- `README.md`: el paso 4 se reescribe (Opción A `.env`, Opción B1 usuario de
  Windows, Opción B2 `$env:` en la terminal), se actualizan las referencias a
  esas opciones en migraciones, arranque, semilla, enviador y el paso SMTP, y el
  árbol del repositorio pasa a mostrar `.env.example`.
- La plantilla SMTP del README añade un bloque que escribe las seis variables en
  el `.env` con `Add-Content`, con la contraseña entre comillas dobles.

**Qué verifiqué, y con qué comando.**

- `git check-ignore -v .env` → `.gitignore:153:.env`; `git status --short` →
  `?? .env.example`, es decir, el ejemplo se puede versionar y el `.env` real no.
- `dotnet build pirry-ledger.slnx` → 0 errores y 0 advertencias;
  `dotnet test pirry-ledger.slnx` → 90 pruebas en verde (6 + 84).
- **Solo `.env`, sin ninguna variable de entorno en el proceso** (se borraron
  las once con `Remove-Item Env:` antes de lanzar) →
  `Now listening on: http://localhost:5243`, `GET /yo` → `401`, `stderr` vacío.
- **Precedencia:** `$env:PIRRY_LEDGER_PUBLIC_BASE_URL = 'http://localhost:5999'`
  con el `.env` en 5243 → `Now listening on: http://localhost:5999`,
  `GET :5999/yo` → `401` y `GET :5243/yo` → sin respuesta.
- **Sin `.env` y sin variables:** renombrado el archivo, `ExitCode: 1` y
  `Falta la variable de entorno ConnectionStrings__PirryLedger. El README
  explica como definirla.` sin traza (RD-08), igual que antes del cambio.
- **Parseo del bloque SMTP del README**, con `dotnet fsi` y el paquete instalado,
  sobre un archivo temporal con `PIRRY_LEDGER_SMTP_PASSWORD="clave de prueba
  12345"` → `SMTP_PASSWORD parseado = [clave de prueba 12345]`, es decir, el
  valor con espacios llega completo y sin comillas.

**Qué NO se hizo.** No cambió ninguna lógica de negocio, ninguna variable ni su
nombre, ni las pruebas. No se tocaron `ConfiguracionEntorno` ni los mensajes de
error. La decisión quedó documentada en `docs/adr/006-local-env-configuration.md`
tras la autorización de Franklin (2026-10-04), con las alternativas descartadas:
loader propio, script de PowerShell, mantener `SetEnvironmentVariable`,
`appsettings.*.local.json` y `dotnet user-secrets`.

---

## Cierre

**Estado de la Práctica 1:** completada.

La práctica cumple los cuatro grupos funcionales del enunciado y la rúbrica
de ocho puntos. La decisión sobre la entrega final fue tomada por Franklin
el 2026-10-05.

### Decisiones abiertas que ya están cerradas

Las notas que quedaron como "pendiente" o "decisión abierta" durante la
construcción de la práctica se resolvieron en commits posteriores y se
documentan aquí, no en las entradas históricas (cambiarlas falsearía la
historia). Cada una tiene su evidencia:

- **Creación del primer Administrador.** La pieza
  `PirryLedger.Core.AccessControl.Application.SeedFirstAdministrator`, montada
  en `Program.cs`, toma el correo y la contraseña de
  `PIRRY_LEDGER_FIRST_ADMIN_*`, hashea la contraseña con Argon2id y siembra
  la cuenta solo si no existe ningún Administrador. La decisión está
  documentada en [`docs/adr/002-first-administrator.md`](../adr/002-first-administrator.md),
  aceptada el 2026-10-01. La pieza está cubierta por las pruebas
  unitarias de `PruebasDelPrimerAdministrador.cs`.
- **Etiqueta `practica-1`.** `git tag --list` la muestra publicada en el
  repositorio, junto a `asignacion-1`. La rama `main` quedó alineada con
  `develop` mediante el PR #15.
- **`docs/maquina-de-estados.md`.** Existe en la ruta exacta que pide el
  enunciado (`docs/maquina-de-estados.md`) con la tabla de transiciones de
  `Invoice` (Draft, Issued, Paid, Cancelled) y la transición prohibida
  `Paid → Cancelled`. La entidad es `Invoice` en `Src/Business/.../Domain/`
  y la traducción al español ("factura") se usa solo en la prosa de la
  documentación.
- **Punto único de rol (RF-CA-05).** `ExigenciasDeRol.cs` declara la
  exigencia de cada `Operacion` en una sola tabla legible, leída por
  `FiltroDeAcceso` antes de cada endpoint. Las pruebas del punto único
  viven en `PruebasDelPuntoUnicoDeAcceso.cs`.
- **Recuperación de contraseña (RF-CA-09 a 13, 22).** Cubierta por los
  casos de uso `PasswordRecovery`, `ForcePasswordReset` y `ChangeOwnPassword`,
  con códigos SHA-256 de un solo uso y 15 minutos de validez.
- **Administración de usuarios (RF-CA-04, 05, 06, 08, 20, 21).** Cubierta
  por `ListUsers`, `ChangeUserRole`, `DeactivateUser`, con la centralización
  de acceso en `ExigenciasDeRol` y las pruebas en
  `PruebasDeAdministracionDeUsuarios.cs`.

### Verificación final

| Comando | Resultado |
|---|---|
| `dotnet build pirry-ledger.slnx --configuration Release --no-restore` | 0 advertencias, 0 errores. |
| `dotnet test Tests/PirryLedger.Core.AccessControl.Tests/PirryLedger.Core.AccessControl.Tests.csproj --configuration Release --no-restore` | 84 superadas, 0 fallidas. |
| `dotnet test Tests/PirryLedger.Core.Notifications.Tests/PirryLedger.Core.Notifications.Tests.csproj --configuration Release --no-restore` | 6 superadas, 0 fallidas. |
| `git tag --list` | `asignacion-1`, `practica-1`. |
| `dotnet run --project Src/Host/PirryLedger.Host -- --send-mail` (sin correos pendientes) | `Correos tomados: 0`, `No habia correos pendientes de enviar.`. |

Esta bitácora queda cerrada. La próxima pieza del curso abre su propia
bitácora.
