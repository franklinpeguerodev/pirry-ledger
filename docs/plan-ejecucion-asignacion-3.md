# Plan de ejecución — Asignación 3

Preparado el 2026-11-28 a petición de Franklin. La autorización actual cubre la
preparación del plan. La ejecución sigue su revisión. Fuente académica completa
en `assignments/asignacion-3.md`.
Franklin autorizó publicar esta rama de planificación y abrir su PR el 2026-11-28.

## Objetivo y decisiones resueltas

Auditar Práctica 5, reproducir al menos tres hallazgos y corregirlos con regresiones
demostrables. Debe existir al menos uno de notificaciones y uno de reportes.
Los casos de este plan son investigaciones por ejecutar. Los resultados correctos
también se registran como cobertura de la auditoría.

Franklin confirmó en esta sesión que la política de canales de ADR 023 y ADR 024
está aprobada. Esa política es la referencia funcional en esta iteración. Se
conservan el texto académico de RF-NOT-06 y su diferencia documentada como
antecedente. Se conservan el monolito modular, contratos, PostgreSQL, reloj UTC,
procesador CLI y contratos administrativos de reportes aprobados.

Decisiones abiertas para comenzar esta auditoría una vez revisado el plan,
ninguna. Los hallazgos son resultados pendientes de investigación. Si una causa
exige elegir estructura, persistencia, protocolo o seguridad, se presentarán
alternativas a Franklin antes de implementar conforme a AGENTS.md.

## Features, ramas y dependencias

Base local de Práctica 5, `develop` en
`860d0fd10f887d4a49c0ef4691737883c8d792c2`. `main` en `1fa48d2` tiene el mismo árbol.
Ese SHA conserva comportamiento y criterios originales. Antes de ejecutar se
comprobará el remoto y se registrará cualquier diferencia. Cada rama posterior
parte de `develop` con sus dependencias integradas y registra su SHA inicial.

| Orden | Rama prevista | Trabajo y archivos | Evidencia de cierre |
|---|---|---|---|
| 0 | `docs/assignment-3-planning` | Enunciado, plan, `current-iteration.md` y `bitacora-asignacion-3.md`. | Alcance y decisiones revisados por Franklin. |
| 1 | `chore/assignment-3-audit` | Línea base y matriz ejecutada en `auditoria-asignacion-3.md`. Auditar los tres frentes. | Al menos tres hallazgos reproducibles y cobertura de notificaciones y reportes. |
| 2…N | `fix/h-<numero>-<causa>` | Una rama por hallazgo confirmado, con causa, pruebas y documentación afectada. | Regresión nueva falla antes y pasa después, causa revisada en otros caminos y suite aprobada. |
| N+1 | `chore/assignment-3-acceptance` | Clon limpio, README, auditoría, contexto y bitácora. | Suite y seis frentes aprobados, reproducción anterior y verificación del candidato por hallazgo. |

Los nombres con `<...>` son plantillas. Se concretan archivos y nombres al conocer
la causa. La planificación cubre notificaciones, cola y reportes, con una unidad
de corrección por hallazgo.

## Paso 1 — Línea base

1. Identificar SHA, SDK, configuración y estado de Git. Distinguir fallos de
   entorno de incumplimientos reproducibles del producto.
2. Ejecutar compilación, suite y arranque del README. Guardar comando, salida y
   código de retorno. Las 709 pruebas históricas son una referencia por medir de nuevo.
3. Preparar datos de prueba identificables en un entorno aislado con la composición
   existente, dos Estándar, dos sucursales, administradores e importes conocidos.
   Preparar también el caso sin administradores activos en ese entorno de prueba.
4. Contrastar criterios originales, reglas operativas y contratos con RF-NOT-01…13,
   RF-REP-01…05 y RD-02/03/06/07/08/10/11.
5. Crear `docs/auditoria-asignacion-3.md` durante la ejecución. Por caso registrar
   requisito, preparación, expectativa, petición/comando, resultado, SHA y evidencia.
   Identificar qué casos faltaban en los criterios originales.

## Paso 2 — Matriz de casos

| Frente y requisitos | Casos por ejecutar | Qué comprobar |
|---|---|---|
| Bandeja, RF-NOT-04/05, RD-06/07 | Dos usuarios, identificador ajeno/inexistente al marcar lectura, entradas vacías y paginación inválida. | Propiedad en servidor, primera lectura y rechazo sin cambios. |
| Productores, RF-NOT-01/02/03/07/08 | Crear/resolver permisos, eventos Billing, repetición y concurrencia, sin administradores activos, correo desactivado y SMTP caído. | Destinatarios y canales según ADR 023/024, operación independiente de SMTP y atomicidad de datos, aviso y cola. |
| Cola, RF-NOT-09…13 | Destinatario inválido, quinto fallo, ejecución repetida/concurrente, interrupción, envío incierto y consulta por Estándar. | Límite, estados seguros, exclusión de procesadores, tratamiento del incierto según ADR 031 y cantidad real recibida. |
| Recordatorios, RF-NOT-03/12, RD-11 | Antes/en/después del vencimiento, resolución antes de SMTP, repetición y ausencia de destinatarios activos. | Límites de 96 horas y jornada RD, control durable por origen y cancelación sin intento. |
| Reportes, RF-REP-01/02/03/05 | Dos rangos y totales manuales, sin datos, fechas ausentes/inválidas/invertidas, límite del último día, filtros y páginas vacías. | Agregación real en origen, filtros previos, ceros, calendario RD y UTC, importes y cantidades exactos. |
| Acceso y consistencia, RF-REP-04, RD-03/06 | Todas las rutas con anónimo y Estándar, filtros por sucursal, cambio concurrente de datos durante consulta. | Rechazo antes del proveedor y fotografía consistente por respuesta según ADR 034. |
| Diseño, RD-02/03/07/08/10/11 | Recorrido petición-consulta, referencias de proyectos, errores provocados, configuración e historial versionado. | Reglas fuera de presentación, contratos, errores controlados, secretos externos y reloj inyectable. |

SQL, persistencia, concurrencia y SMTP se verifican también contra componentes
reales. Se guarda salida saneada. RD-10 se revisa sin leer `.env`, usando nombres
de `.env.example`, configuración y revisión segura de archivos e historial.

## Paso 3 — Ciclo por hallazgo

Asignar H-01, H-02 y siguientes después de reproducir cada incumplimiento. Registrar
título, gravedad con motivo, requisito/criterio, SHA anterior, preparación, comando,
esperado y observado. Un criterio incompleto puede constituir un hallazgo si se
demuestra el caso omitido y su consecuencia. Continuar hasta cubrir el mínimo con
evidencia real.

1. Presentar a Franklin causa, archivos, propuesta y verificación antes de corregir.
   Buscar la misma regla defectuosa en otros caminos del alcance.
2. Agregar una regresión xUnit al módulo afectado, nombre español, H-XX en nombre o
   comentario, tres fases identificables, independencia, dobles y reloj falso.
   Los rechazos verifican también estado y datos.
3. Ejecutar la prueba nueva sobre el código del padre del futuro commit. Guardar
   el fallo por comportamiento, corregir y guardar el paso. Después de cerrar el
   commit repetir contra su padre exacto. La prueba debe compilar en ambos puntos.
   Un error de compilación exige ajustar su preparación para demostrar el defecto.
4. Ejecutar pruebas pertinentes y suite completa. Actualizar README, contexto,
   auditoría y criterios afectados en el mismo PR. Revisar el ADR aplicable y
   actualizarlo si el cambio hace incorrecto su contenido. Un ADR nuevo requiere
   autorización según AGENTS.md.
5. Preparar un commit de corrección por hallazgo con su prueba. Formato de asunto,
   `Fix <cause> (H-01, RF-NOT-05)`. Registrar el SHA exacto en la auditoría. El
   commit contiene arreglo y regresión juntos, y se conserva la evidencia fallar/pasar.
6. Completar la bitácora en un commit separado del mismo PR. Franklin revisa
   borrador, evidencias y su propia comprobación antes de publicar.

Se justifica un cambio mayor de 200 líneas por su unidad lógica. Cada hallazgo
declarado corregido se resuelve por completo. Un hallazgo bajo diferido queda
identificado y justificado para revisión de Franklin.

## Paso 4 — Delegación y criterios

La bitácora se organiza por hallazgo y conserva los seis campos de la consigna.
Citar instrucciones reales de Práctica 5 y evidencia de la sesión de corrección.
Franklin confirma por qué pasó inadvertido y qué verificó personalmente. Se
atribuyen al agente sus comprobaciones. Cada mejora de «Qué cambias» debe verse
en criterios o contexto del candidato, con referencia al criterio original por SHA.
Las mejoras concretas se redactan al conocer la causa.

## Paso 5 — Aceptación y entrega

- Desde clon limpio ejecutar el comando único de la sección Pruebas del README,
  `dotnet test pirry-ledger.slnx --configuration Release`, y compilar la solución.
- Ejecutar el arranque del README, `docker compose up --build`, y los seis frentes
  de `verificacion-docker-practica-5.md`, alta/activación, sesiones/roles, recuperación,
  permisos, Billing/jornadas y documento persistente tras recrear contenedores
  conservando volúmenes.
- Reproducir cada defecto en el padre de su arreglo y verificar el candidato final.
  Aplicar también la prueba nueva al código anterior y guardar fallo/paso. Usar
  copias aisladas para conservar el trabajo actual.
- Verificar mínimo de hallazgos, requisitos, gravedad, evidencias, commits propios,
  pruebas nuevas, bitácora completa y cambios preventivos visibles.
- Revisar ignorados, estado y diff preparado antes de proponer commits. PR con
  las cuatro secciones existentes. Franklin revisa commits y bitácora. Publicación
  de ramas/PR requiere autorización para esta asignación.
- La entrega se evalúa en `asignacion-3`. Merge, creación/publicación de etiqueta
  y entrega en Moodle requieren instrucción de Franklin. Consultar la fecha de la
  plataforma cuando corresponda.

## Límites y estado de preparación

El alcance comprende auditoría y correcciones de Práctica 5 con regresión acumulada.
Quedan fuera nuevas funciones de negocio, móvil, módulo general de auditoría,
cambios de infraestructura y limpieza de datos operativos reales. Se usa
infraestructura existente con datos aislados. Una limitación de entorno deja el
criterio afectado pendiente de verificar hasta resolverla.

Se leyeron las cuatro páginas de la consigna, criterios, requisitos y estado de
Práctica 5. Se comprobaron ramas locales, igualdad de árboles main/develop e
ignorados de los nuevos documentos. Suite, arranque y auditoría funcional se
ejecutarán en el bloque 1.
