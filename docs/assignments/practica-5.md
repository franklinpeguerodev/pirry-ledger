# Práctica 5 — Notificaciones y reportes

Transcripción textual del PDF facilitado por Franklin. Los saltos de línea y
las tablas conservan el orden de extracción; no se adaptan los requisitos.

## Página 1

PROGRAMACIÓN III · TDS-007 · ITLA · 2026-C-3
Práctica 5 — Delegas al agente notificaciones y reportes
Bloque 5 | Semana 10 | 8 puntos | Individual

Plazo Entrega tardía
El que indica la plataforma virtual. Hasta 24 horas después, con 50 % de penalidad.
Luego no se acepta.

Quinto y último hito de práctica de tu repositorio. Por primera vez le pides al agente una funcionalidad
completa y nueva, no un ajuste sobre código que ya existe: el motor de notificaciones con su cola de
correos y los reportes con agregación. Antes de delegar, escribes el contexto que el agente necesita
y los criterios con los que vas a aceptar su trabajo. Además se verifica que lo construido hasta la
Práctica 4 siga funcionando.
En resumen: entregas, en el tag practica-5, el archivo de contexto y los criterios escritos antes de
delegar, y el proyecto con notificaciones, cola de correos y reportes funcionando, sin que se haya roto nada
de lo anterior.

1. Alcance
1.1 Contexto y criterios de aceptación, antes de delegar
El agente resuelve bien lo que se le pide sin ambigüedad. Esta parte se entrega como archivos del
repositorio y se evalúa en el historial de git, no en lo que digas.
• Archivo de contexto del agente en la raíz del repositorio: AGENTS.md, GEMINI.md o el que use el agente
que elegiste. Declara el propósito del proyecto, el lenguaje y el framework, el comando para correrlo, el
comando para probarlo, las convenciones de código y las restricciones: qué partes no puede tocar y
que nunca debe escribir credenciales ni leer el archivo .env.
• Criterios de aceptación en docs/criterios-notificaciones-reportes.md. Cada criterio es una
condición que se puede comprobar, escrita como "dado / cuando / entonces" o equivalente, y cita el
requisito que cubre (RF-NOT-xx, RF-REP-xx). Cubre las dos funcionalidades que delegas.
• Un criterio vale solo si alguien más puede comprobarlo sin preguntarte. "Las notificaciones funcionan
bien" no es un criterio; "al crear una solicitud, cada Administrador activo tiene una notificación nueva sin
leer y ningún Estándar la tiene" sí lo es.
• El commit que agrega el archivo de contexto y los criterios tiene que ser anterior al primer commit del
motor de notificaciones. Delegas con los criterios en la mano, no los escribes después para justificar lo
que salió.
1.2 Notificaciones (pieza 4)
Lo construye el agente a partir de tus criterios; tú eres responsable de lo que se entrega. Todas las
operaciones requieren sesión válida (RD-06).
ID Requisito Criterio de aceptación
RF-NOT-01 Crear una solicitud de permiso
notifica a los Administradores.
Todos los Administradores reciben
la notificación; nadie más la recibe.
RF-NOT-02 Resolver una solicitud de permiso
notifica al solicitante.
Indica si fue aprobada o rechazada
y, en el segundo caso, el motivo.
RF-NOT-03 Al menos un evento propio de tu El evento está descrito en tu

## Página 2

módulo de negocio dispara una
notificación.
README y se puede provocar
siguiendo sus pasos.
RF-NOT-04 Consultar las notificaciones
propias, distinguiendo leídas de no
leídas.
Un usuario no ve las notificaciones
de otro.
RF-NOT-05 Marcar una notificación como leída. Intentar marcar la notificación de
otro usuario se rechaza.
RF-NOT-06 Toda notificación se entrega por
dos canales: dentro del sistema y
por correo.
El destinatario la ve en su bandeja
interna y recibe además el correo
correspondiente.
RF-NOT-07 El destinatario puede desactivar el
canal de correo sin perder el
interno.
Con el correo desactivado la
notificación aparece dentro del
sistema y no se encola ningún
correo.

1.3 Cola de correos
Desde la Práctica 1 existe la versión mínima de la cola: encola, un proceso independiente envía y no
duplica (RF-NOT-08, 09, 12 y 13). Esta práctica la completa y la expone.
ID Requisito Criterio de aceptación
RF-NOT-08 Los correos se encolan; no se
envían dentro de la operación que
los origina.
La operación termina
correctamente aunque el servidor
de correo no responda.
RF-NOT-09 Un proceso independiente toma los
correos pendientes y los envía.
Se ejecuta sin que el flujo que creó
el correo vuelva a correr.
RF-NOT-10 Un correo que falla se reintenta un
número limitado de veces y luego
queda fallido.
No se reintenta indefinidamente ni
desaparece sin dejar rastro. El
máximo de intentos está declarado
en tu README.
RF-NOT-11 Cada correo registra estado
(pendiente, enviado o fallido),
intentos, fechas y último error.
Un Administrador consulta la cola y
sabe qué pasó con cualquier
correo; un Estándar recibe
rechazo.
RF-NOT-12 Un correo enviado no se vuelve a
enviar.
Ejecutar el procesador dos veces
seguidas no duplica ningún envío.
RF-NOT-13 Las credenciales del servidor de
correo se leen de variables de
entorno.
No aparecen en el repositorio ni en
el historial (RD-10).

Tu README, en una sección Notificaciones, indica cómo provocar cada evento, cómo ejecutar el
procesador de la cola, cómo provocar un fallo de envío (por ejemplo, con un servidor de correo
apagado) y cuál es el máximo de intentos.
1.4 Reportes con agregación (pieza 5)
ID Requisito Criterio de aceptación
RF-REP-01 Un reporte sobre el Core:
solicitudes de permiso por estado y
por período, o documentos subidos
por usuario.
Devuelve valores agrupados, no el
listado de registros.
RF-REP-02 Un reporte sobre tu módulo de
negocio.
Responde una pregunta real del
dominio, no un conteo de filas.

## Página 3

RF-REP-03 Ambos reportes usan agregación
real: agrupaciones, totales o
promedios.
El resultado tiene menos filas que
los registros que resume.
RF-REP-04 Los reportes respetan el rol de
quien consulta.
Un Estándar no obtiene datos de
otros usuarios a través de un
reporte.
RF-REP-05 Ambos reportes se pueden filtrar
por rango de fechas.
Dos rangos distintos producen
resultados distintos sobre los
mismos datos.

Tu README, en una sección Reportes, dice qué pregunta responde el reporte de tu negocio y cómo
consultar ambos con un rango de fechas. La agregación se hace en el motor de datos o en una función
propia de tu proyecto, no con un listado completo que se resume a mano en la interfaz.
1.5 Revisión acumulada
Se copia tu .env.example a .env, se arranca tu proyecto desde el punto etiquetado con el comando de
tu README, siguiéndolo al pie de la letra (lo que el README no dice, no se busca), y se comprueban
seis frentes:
• Control de acceso: registro con activación por correo, con el correo saliendo por la cola.
• Control de acceso: inicio y cierre de sesión, y rechazo a un usuario Estándar que invoca una operación
de Administrador.
• Control de acceso: recuperación de contraseña.
• Gestión de permisos: las solicitudes se crean, se aprueban y se rechazan, y las transiciones fuera de la
tabla se rechazan.
• Tu módulo de negocio: una operación válida funciona y la transición prohibida de tu máquina de estados
se rechaza.
• Manejador de documentos: un documento subido sobrevive a docker compose down (sin -v) y volver a
levantar.
2. Cómo se entrega
git tag practica-5
git push origin --tags

En Moodle, en la tarea «Práctica 5»: la URL de tu repositorio y el nombre de la etiqueta, practica-5.
Se evalúa el punto etiquetado; puedes seguir trabajando en main después. Esta es la última práctica
del cuatrimestre.
3. Rúbrica
Criterio No logrado (0 %) Parcialmente logrado
(50 %)
Logrado (100 %)
Archivo de contexto y
criterios de aceptación
(1.5 pts)
No hay archivo de
contexto ni criterios
escritos, o los criterios se
escribieron después de
que el agente entregó el
código.
Existen el archivo de
contexto y los criterios,
pero el contexto no dice
cómo correr ni probar el
proyecto ni qué no debe
tocar el agente, o hay
criterios que no se
El archivo de contexto
declara propósito,
lenguaje, cómo correr,
cómo probar,
convenciones y
restricciones (incluida la
regla de no tocar

## Página 4

pueden comprobar ("las
notificaciones funcionan
bien"), o no cubren las
dos funcionalidades
delegadas.
secretos). Los criterios
cubren notificaciones y
reportes, cada uno con
una condición y cómo se
comprueba, citan los RF
y el commit que los
agrega es anterior al
primer commit de
notificaciones.
Notificaciones RF-NOT-
01 a 07 (2.0 pts)
No hay notificaciones, o
se consultan sin sesión
válida.
Se crean y se consultan,
pero falla alguna regla: la
crea o recibe quien no
debe (RF-NOT-01, 02),
un usuario ve o marca la
notificación de otro (RF-
NOT-04, 05), falta el
evento del negocio (RF-
NOT-03) o no se puede
desactivar el canal de
correo (RF-NOT-07).
Crear una solicitud
notifica solo a los
Administradores;
resolverla notifica al
solicitante con el
resultado y el motivo; un
evento del negocio
notifica y se puede
provocar siguiendo el
README; las
notificaciones se
consultan y marcan solo
las propias; cada una
llega por canal interno y
por correo; con el correo
desactivado queda la
interna y no se encola
correo.
Cola de correos RF-NOT-
08 a 13 (1.5 pts)
Los correos se envían
dentro de la operación
que los origina, o el envío
falla si el servidor de
correo no responde.
Los correos se encolan y
se envían aparte, pero
falta una regla: reintentos
limitados con estado
fallido (RF-NOT-10),
consulta del estado por el
Administrador (RF-NOT-
11) o evitar duplicados al
ejecutar el procesador
dos veces (RF-NOT-12).
Los correos se encolan;
el procesador
independiente los envía;
un correo que falla se
reintenta el máximo
declarado en el README
y queda fallido con su
último error; el
Administrador consulta la
cola; ejecutar el
procesador dos veces no
duplica envíos; las
credenciales SMTP salen
de variables de entorno.
Reportes con agregación
RF-REP-01 a 05 (1.5 pts)
No hay reportes, o
devuelven el listado de
registros en lugar de
valores agrupados.
Hay dos reportes con
agregación, pero falla
una regla: el del negocio
solo cuenta filas, un
Estándar obtiene datos
de otros usuarios (RF-
REP-04) o el filtro por
fechas no cambia el
resultado (RF-REP-05).
Un reporte del Core y uno
del negocio devuelven
valores agrupados
(menos filas que los
registros que resumen);
el del negocio responde
una pregunta real del
dominio; un Estándar
solo ve lo suyo; dos
rangos de fechas
distintos dan resultados
distintos sobre los

## Página 5

mismos datos.
Revisión acumulada (1.5
pts)
El proyecto no levanta
con el comando del
README, o funciona uno
de los seis frentes o
ninguno.
Funcionan de dos a cinco
de los seis frentes.
Funcionan los seis
frentes.
Total 8.0

4. Qué se revisa exactamente
• git clone del repositorio y git checkout practica-5.
• El archivo de contexto del agente y docs/criterios-notificaciones-reportes.md, y en git log que
el commit que los agrega es anterior al primer commit del motor de notificaciones.
• .env.example copiado a .env y el comando de la sección «Contenedor» del README: el proyecto
levanta.
• Crear una solicitud de permiso con un Estándar y comprobar la notificación de los Administradores;
resolverla y comprobar la del solicitante; provocar el evento del negocio.
• Consultar y marcar notificaciones propias, y comprobar que las de otro usuario se rechazan. Desactivar
el canal de correo y comprobar que no se encola ninguno.
• Provocar un fallo de envío según el README: reintentos, estado fallido y último error; consulta de la
cola como Administrador y como Estándar; ejecutar el procesador dos veces.
• Los dos reportes con dos rangos de fechas distintos y con un usuario Estándar.
• El historial completo, en busca de credenciales SMTP o de la clave de la API externa.
• Los seis frentes de la revisión acumulada, arrancados desde el README.
Programación III · ITLA · 2026-C-3
