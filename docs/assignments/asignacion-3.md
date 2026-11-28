# Asignación 3 — Auditas lo que el agente entregó y lo corriges

Transcripción textual del PDF facilitado por Franklin el 2026-11-28.
Los saltos y las tablas siguen el orden de extracción del original.

## Página 1

PROGRAMACIÓN III · TDS-007 · ITLA · 2026-C-3
Asignación 3 — Auditas lo que el agente entregó y lo
corriges
Bloque 5 | Semana 11 | 5 puntos | Individual

Plazo Entrega tardía
El que indica la plataforma virtual. Hasta 24 horas después, con 50 % de penalidad.
Luego no se acepta.

Tercera asignación. El agente te entregó notificaciones, cola de correos y reportes en la Práctica 5, y
que el código corra no significa que esté bien. Esta semana lo revisas como revisarías el trabajo de
otra persona: contra los criterios que tú mismo escribiste, ejecutándolo, y corriges lo que encuentres.
En resumen: entregas en el tag asignacion-3 tu auditoría de lo que el agente construyó, las correcciones
en el repositorio (cada una con su prueba) y la bitácora de cómo delegaste, qué falló y qué cambias para la
próxima vez.

1. Qué entregas
1.1 La auditoría
Partes del código que hoy está en tu repositorio, que incluye lo que el agente escribió en la Práctica 5.
Auditas notificaciones, cola de correos y reportes. No hace falta leer cada línea; sí ejecutar y
provocar casos. Compara contra tres referencias:
• Tus criterios de aceptación (docs/criterios-notificaciones-reportes.md) y los requisitos RF-NOT
y RF-REP.
• Los requisitos de diseño RD-02, RD-03, RD-06, RD-07, RD-08, RD-10 y RD-11 aplicados a lo que el
agente escribió.
• Los casos que tus criterios no cubrían: entradas vacías o fuera de rango, un usuario sin ningún
Administrador activo, dos solicitudes simultáneas, un rango de fechas sin datos, un correo con
destinatario inválido.
Hallazgos con evidencia. Un hallazgo es algo que se puede reproducir, no una opinión de estilo.
Entregas al menos tres, de los cuales al menos uno es de notificaciones y uno de reportes. Si tu
criterio fue incompleto y por eso el defecto pasó, también es un hallazgo (sobre tus criterios).
Algunos lugares donde suele haber problemas, para que sepas dónde mirar (no es una lista de lo que
debes encontrar):
• Un usuario consulta o marca como leída la notificación de otro cambiando un identificador (RF-NOT-04,
05; RD-06).
• El correo se envía dentro de la operación que lo origina, o la operación falla si el servidor de correo no
responde (RF-NOT-08).
• Un correo fallido se reintenta sin límite, o el procesador envía dos veces el mismo correo al ejecutarse
dos veces (RF-NOT-10, 12).
• Con el correo desactivado se encola igual, o se desactiva también el canal interno (RF-NOT-07).
• El reporte trae todos los registros y los suma en memoria, o el filtro de fechas incluye o excluye mal el
último día (RF-REP-03, 05).

## Página 2

• Un Estándar obtiene datos de otros usuarios por el reporte (RF-REP-04).
• Un mensaje de error muestra una traza, una ruta o una consulta (RD-08); una credencial quedó escrita
en el código o en un archivo versionado (RD-10); fechas con distinto criterio de zona horaria entre
piezas (RD-11).
• Lógica de negocio dentro del manejador de la petición (RD-02), o el Core que ahora depende de tu
módulo de negocio (RD-03).
1.2 Las correcciones
• Corriges todos los hallazgos que declares como corregidos; puedes dejar sin corregir alguno de
gravedad baja si lo declaras y explicas por qué.
• Un commit por hallazgo, cuyo mensaje cita el número del hallazgo. Un commit que arregla tres cosas
a la vez no se puede ligar a ninguna.
• Corregir la causa, no el síntoma: si el agente hizo mal una regla en un lugar, revisa si la repitió en otro.
• Puedes pedirle la corrección al agente o hacerla tú. Si se la pides, tú verificas el resultado antes de
aceptarlo, igual que la primera vez.
• Lo que ya funcionaba sigue funcionando: el proyecto levanta con el comando del README y los seis
frentes de la revisión acumulada de la Práctica 5 siguen pasando.
1.3 Una prueba por hallazgo
• Cada hallazgo corregido tiene una prueba nueva que falla sobre el código anterior a la corrección y
pasa sobre el corregido. Una prueba que pasa en ambos no detecta nada.
• Pruebas con el framework del lenguaje, sin levantar la aplicación completa (RD-12), con tres fases
identificables (preparar, actuar, verificar), independientes entre sí y con nombre que dice el
comportamiento.
• El nombre o un comentario de la prueba cita el hallazgo que cubre.
• Un único comando, en la sección Pruebas de tu README, ejecuta toda la suite desde un clon limpio. La
suite pasa.
1.4 Bitácora de delegación
Archivo docs/bitacora-asignacion-3.md, incluido en el tag. Se organiza por hallazgo. Para cada uno:
Campo Qué debe decir
Hallazgo Número, título corto y gravedad (alta, media o baja).
Qué se pidió La instrucción o el criterio con que delegaste esa parte
en la Práctica 5.
Qué entregó el agente Qué hacía realmente, con la evidencia: salida, captura
o commit donde se ve.
Por qué pasó Por qué no lo detectaste antes: criterio ausente,
criterio ambiguo, instrucción incompleta o revisión
insuficiente.
Corrección Quién la hizo (tú o el agente), la instrucción que diste
si fue el agente, y el commit.
Qué cambias Qué agregaste al archivo de contexto o a tus criterios
para que esta clase de defecto no se repita.

Una bitácora donde el agente acertó en todo, o donde no se explica por qué el defecto pasó, está
incompleta y se devuelve. Los cambios que declaras en "Qué cambias" deben verse en el archivo de
contexto o en los criterios del tag.
2. Cómo se entrega

## Página 3

git tag asignacion-3
git push origin --tags

En Moodle, en la tarea «Asignación 3»: la URL de tu repositorio y el nombre de la etiqueta,
asignacion-3. Se evalúa el punto etiquetado; puedes seguir trabajando en main después.
3. Rúbrica
Criterio No logrado (0 %) Parcialmente logrado
(50 %)
Logrado (100 %)
Auditoría contra tus
criterios (1.5 pts)
No hay auditoría, o los
hallazgos no se pueden
comprobar en el
repositorio (no dicen qué
se ejecutó ni qué se vio).
Hay hallazgos reales,
pero son menos de tres,
o no hay ninguno sobre
notificaciones o ninguno
sobre reportes, o un
hallazgo no cita el criterio
o requisito que incumple,
o no dice cómo se
detectó.
Al menos tres hallazgos
que se confirman al
ejecutar el proyecto; al
menos uno de
notificaciones y uno de
reportes; cada uno cita el
criterio o requisito
incumplido, la evidencia
(qué se ejecutó y qué se
obtuvo) y su gravedad.
Correcciones en el
repositorio (1.5 pts)
Ninguno de los hallazgos
está corregido en el tag,
o las correcciones
rompen el proyecto o lo
anterior (no levanta, o
falla algún frente de la
revisión acumulada).
Se corrigen algunos
hallazgos, o una
corrección arregla el
síntoma sin resolver la
causa (se repite en otro
lugar), o un commit
mezcla varias
correcciones y no se
puede ligar a su hallazgo.
Cada hallazgo declarado
como corregido se
reproduce en el punto
anterior a su commit y no
se reproduce en el tag;
cada corrección es un
commit propio que cita el
hallazgo; el proyecto
sigue levantando y los
seis frentes siguen
funcionando.
Prueba de regresión por
hallazgo (1.0 pt)
No hay ninguna prueba
nueva asociada a los
hallazgos.
Hay pruebas para
algunos hallazgos, o una
prueba pasa también
sobre el código sin
corregir (no detecta el
defecto), o depende del
orden de ejecución.
Cada hallazgo corregido
tiene una prueba que
falla sobre el código
anterior a la corrección y
pasa sobre el corregido.
Un solo comando
documentado en el
README las ejecuta y
toda la suite pasa.
Bitácora de delegación
(1.0 pt)
No hay bitácora, o no
corresponde a esta
asignación, o no muestra
evidencia de la sesión
real.
Documenta qué pediste y
qué devolvió el agente,
pero no explica por qué el
defecto pasó inadvertido
ni qué cambiaste en tu
forma de delegar, o no
hay ninguna instrucción
de corrección que le diste
al agente.
Para cada hallazgo: qué
se le había pedido, qué
entregó, por qué el
criterio no lo detectó
antes, cómo corregiste
(con el agente o sin él) y
qué cambiaste en el
archivo de contexto o en
los criterios para que no
se repita, con evidencia
de la sesión.

## Página 4

Total 5.0

4. Qué se revisa exactamente
• git clone del repositorio y git checkout asignacion-3.
• docs/bitacora-asignacion-3.md: los hallazgos, su evidencia y los campos de cada uno.
• Reproducir cada hallazgo en el commit anterior a su corrección y comprobar que ya no se reproduce en
el tag.
• git log: un commit por hallazgo, con el hallazgo citado en el mensaje.
• Cada prueba de regresión ejecutada sobre el código anterior a la corrección (debe fallar) y sobre el tag
(debe pasar).
• El comando de la sección «Pruebas» del README, ejecutado tal como está escrito.
• El archivo de contexto y los criterios: que reflejan los cambios declarados en la bitácora.
• El proyecto arrancado desde el README y los seis frentes de la revisión acumulada de la Práctica 5.
Programación III · ITLA · 2026-C-3
