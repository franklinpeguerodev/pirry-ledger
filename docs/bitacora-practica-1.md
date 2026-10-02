# Bitácora de la Práctica 1 — Control de acceso

## Contexto

- Repositorio: `franklinpeguerodev/pirry-ledger`
- Asignación: 1 — Práctica 1, *Control de acceso completo* (Programación III,
  TDS-007, ITLA, 2026-C-3). Bloque 2, semana 4, 8 puntos, individual.
- Herramienta: agente de OpenCode corriendo en mi máquina (Windows,
  PowerShell 5.1, `dotnet` SDK 10.0.302).
- Fechas: del 2026-09-29 al 2026-10-01.
- Rama de trabajo: `develop`, con una rama por funcionalidad.

Esta bitácora no sustituye a `docs/bitacora-asignacion-1.md`, que cubre el
`.gitignore`, el README inicial y la plantilla de pull request. Aquella dejó la
lección que rigió en esta: **lo que devuelve el agente es un borrador, no una
verdad**. Nada se sube sin que yo lo lea y lo ejecute en mi máquina.

**Sobre los huecos.** Tres apartados están marcados como `[POR COMPLETAR]`. Son
las palabras textuales que le di al agente en cada momento. No las escribo yo
porque no las tengo guardadas y una bitácora que inventa las preguntas que
hice no sirve para defender el trabajo. Reconstruí lo que se puede reconstruir:
los commits, sus requisitos y su orden salen de `git log`, no de la memoria.

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

**Qué se le pidió:** `[POR COMPLETAR: el prompt]`

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

**Qué hice yo.** `[POR COMPLETAR]`

**Qué verifiqué.** Revisé la tabla por SQL siguiendo el README: una fila nace en
`Pendiente` con `intentos = 0` sin que ninguna operación abra una conexión SMTP
(RF-NOT-08); corrí el enviador dos veces seguidas y la segunda respondió
`Correos tomados: 0` sin reenviar nada (RF-NOT-12); borré
`PIRRY_LEDGER_SMTP_PASSWORD` y el correo volvió a `Pendiente` con `intentos = 1`
(RF-NOT-13).

---

## Registro y activación — RF-CA-01, 02, 14, 15, 16, 17

**Qué se le pidió:** `[POR COMPLETAR: el prompt]`

**Qué devolvió el agente.** Tres commits:

| Commit | Qué añadió |
|---|---|
| `d72c524` | Hash Argon2id y la política de contraseña |
| `c169bdb` | Entidades de control de acceso y sus tablas |
| `231db3a` | El flujo completo: registro, activación y reenvío |

**Qué hice yo.** `[POR COMPLETAR]`

**Qué verifiqué.** Leí `ac_usuarios` y los hashes empiezan por
`$argon2id$v=19$m=` y son distintos entre usuarios con la misma contraseña, porque
el salt es por usuario (RF-CA-02). El correo de la cola trae el enlace con un
token de 64 hexadecimales y en `ac_tokens_activacion` solo está su SHA-256
(RF-CA-15). Abrí el enlace: `200 {"activado":true}`; lo abrí otra vez: `400`, con
el mismo `400` que un token inventado (RF-CA-16). Registré el mismo correo en
mayúsculas y respondió `409` (RF-CA-01).

---

## Sesión — RF-CA-03, 07, 18, 19

**Qué se le pidió:** `[POR COMPLETAR: el prompt]`

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

**Qué hice yo.** `[POR COMPLETAR]`

**Qué verifiqué.** Correo inexistente y contraseña equivocada dan el mismo `401`
con el mismo cuerpo, y el caso de uso verifica la contraseña contra un hash
señuelo para que el tiempo de respuesta tampoco distinga (RF-CA-03). Cerré
sesión y la credencial dejó de servir, sin cerrar las demás del mismo usuario
(RF-CA-18). Cinco intentos fallidos bloquearon la cuenta y el sexto, con la
contraseña correcta, se rechazó (RF-CA-19).

---

## Decisiones

`docs/adr/001-credencial-de-sesion.md`, aceptada el 2026-09-30: credencial
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
dotnet test pirry-ledger.slnx      -> 31 superadas, 0 fallidas
```

El proyecto de pruebas de Notifications sigue sin pruebas, y por eso `dotnet test`
advierte que no encuentra ninguna ahí. No es un fallo de esta revisión.

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
- La etiqueta `practica-1` y su publicación: se ejecutan después de la revisión
  final de entrega.
- El proyecto de pruebas de Notifications existe, pero todavía no contiene
  pruebas detectables.

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