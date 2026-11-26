# ADR 031 - Single email queue processor

- **Estado:** aceptada e implementada
- **Fecha:** 2026-11-21
- **Requisitos:** RF-NOT-09, RF-NOT-10, RF-NOT-11, RF-NOT-12, RD-08, RD-09
- **Decide:** Franklin. **Redacta:** el agente, con autorización expresa.

## Contexto

La cola necesita cinco intentos totales y distinguir un fallo conocido de un
resultado incierto. Un proceso puede interrumpirse después de que SMTP acepte
el correo y antes de guardar Sent. Reenviar ese mensaje podría duplicarlo.
Los avisos internos no dependen del enviador de correo.

La prueba real detectó que dos conexiones distintas, incluso sin pooling de
Npgsql, ejecutaron en el mismo pg_backend_pid y ambas obtuvieron el bloqueo
de sesión. Esto evidencia reutilización de sesión en el entorno y hace
insuficiente ese bloqueo para excluir al segundo enviador. No se declara
verificada la exclusión. Se presentó a Franklin adaptar la coordinación a un
bloqueo transaccional en conexión exclusiva o configurar acceso directo que
conserve sesión. Franklin autorizó explícitamente el bloqueo transaccional en
conexión exclusiva tras conocer el resultado.

## Decisión

Franklin eligió alternativa 1: un solo enviador activo. Tras la prueba del entorno,
autorizó un bloqueo transaccional PostgreSQL en una conexión exclusiva sin pooling
de Npgsql. Una segunda ejecución termina informando que hay otra activa.
La transacción de coordinación permanece abierta durante el comando, incluido SMTP,
pero no escribe datos de negocio ni mantiene bloqueadas facturas o sucursales.
No se agrega tabla de coordinación ni variable de entorno.
La sesión se verifica antes de recuperar o reclamar mensajes; perderla aborta el
procesador antes de iniciar otros envíos. Finalizar la transacción libera el bloqueo.
Solo el proceso que obtiene el bloqueo recupera Processing anteriores como
Uncertain, sin reenviarlos ni afirmar que fallaron o se enviaron.

Cada ejecución selecciona un lote de pendientes y reclama individualmente cada
mensaje antes de procesarlo. Guarda el resultado de cada mensaje por separado.
Cada correo tiene como máximo un intento por ejecución, sin espera adicional
entre ejecuciones ni backoff; el quinto fallo conocido termina en Failed.
Un resultado incierto nunca vuelve automáticamente a Pending.

El transporte distingue fallo anterior al envío, rechazo SMTP explícito y pérdida
de confirmación durante el envío. Un fallo al desconectar después de aceptación
no invalida esa aceptación. Si falla guardar el resultado, se interrumpe el
procesador: la próxima ejecución trata el Processing persistido como incierto.
Errores públicos/persistidos son mensajes controlados, sin respuestas SMTP crudas,
credenciales, trazas, consultas ni enlaces de activación/recuperación.

La consulta administrativa muestra metadatos de cola, estados, intentos, fechas
y último error seguro; no expone cuerpos de mensajes ni ofrece reenvío.
Las preferencias y elegibilidad se revalidan antes del intento de envío.
La consulta del destinatario obtiene estado actual sin reutilizar una entidad
seguida en una consulta previa del mismo comando.

## Alternativas consideradas

- Un enviador con bloqueo transaccional exclusivo: elegida y comprobada en el
  entorno actual. Funciona con reutilización transaccional
  de sesiones; serializa envíos y mantiene una transacción de coordinación.
- Bloqueo de sesión: propuesta inicial descartada tras prueba real. Una conexión
  directa podría conservar ese diseño, pero exigiría otra configuración.
- Bloqueo por mensaje: permite varios enviadores con mensajes diferentes;
  exige coordinar posesión y recuperación individual. No aporta capacidad
  necesaria para el volumen actual del negocio.
- Vencimiento fijo de Processing: puede confundir un envío lento con un proceso
  interrumpido. No se usa un temporizador para decidir que el correo se perdió.

## Consecuencias

Notifications incorpora contrato de exclusión del procesador y nuevos metadatos
de cola, con migración que conserva ordinales y fechas históricas desconocidas.
Processing antiguos se consideran inciertos únicamente al obtener el bloqueo.
El bloqueo coordina los enviadores nuevos; no deben coexistir ejecutables de la
versión anterior que desconocen este protocolo durante el despliegue.
La API sigue encolando y guardando avisos internos mientras se envían correos.
No se añaden paquetes, temporizadores, reenvío manual ni módulo de auditoría.

ADR 032 amplía revalidación con el estado actual del caso de atraso antes del
reclamo. Resuelto cancela Pending sin intento; error conserva Pending y aborta
esa fase. El trabajador CLI combinado espera 30 segundos entre ejecuciones;
conserva el bloqueo, cinco intentos y resultado incierto sin reenvío de este ADR.
