# ADR 030 - Notification event and recipient keys

- **Estado:** aceptada e implementada; ampliada para atrasos por ADR 032
- **Fecha:** 2026-11-20
- **Requisitos:** RF-NOT-01, RF-NOT-02, RF-NOT-03, RF-NOT-08, RF-NOT-12, RD-03, RD-09
- **Decide:** Franklin. **Redacta:** el agente, con autorización expresa.

## Contexto

Reprocesar un mismo evento no debe duplicar aviso ni correo. Cierres sucesivos
de una jornada son eventos diferentes, aunque compartan jornada o instante UTC.
Solo se distribuye a destinatarios activos; activo significa cuenta habilitada,
no presencia ni sesión abierta. Se mantiene la matriz de canales aprobada.

## Decisión

Franklin eligió alternativa 1: clave estable del evento en cada Notification,
con restricción única PostgreSQL por clave/destinatario. La cola notificable
referencia el aviso y tiene restricción única sobre esa referencia. Un aviso
ya existente no vuelve a generar correo. No se añade tabla independiente de eventos.

Los eventos usan tipo e identidad de origen. Solicitud y transferencia mantienen
su identidad entre intentos; cada cierre válido obtiene identidad propia,
conservada solo para el último cierre. No se usan fechas como única identidad.
La operación y su publicación participan en IApplicationTransaction (ADR 029).
No hay llamada SMTP dentro del flujo de negocio.

AccessControl expone destinatarios activos por contrato, sin entregar entidades
ni credenciales a Notifications. La política central de Notifications determina
audiencia y correo según la matriz. La preferencia administrativa no afecta
activación ni recuperación; desactivarla cancela solo correos notificables pendientes.

## Alternativas consideradas

- Clave en aviso: elegida. Restricción directa por destinatario, sin tabla adicional.
- Tabla independiente de eventos: conserva eventos sin destinatarios y permite
  consultar distribución; añade persistencia que no requiere la operación actual.
- Consultar antes de insertar sin restricción: dos procesos pueden duplicar la entrega.

## Consecuencias

Se amplían persistencia de avisos y cola, y contratos públicos entre módulos.
Los avisos existentes conservan identidad y lectura. Cada cierre mantiene solo
su identificador más reciente; no se implementa historial de cierres ni auditoría.
Un evento sin destinatarios activos no crea avisos ni genera entrega retroactiva
por habilitar después una cuenta o el correo. ADR 032 agrega control específico
de atrasos, también sin destinatarios; no modifica las restricciones por entrega
ni introduce un registro general de eventos.

Preferencia y encolado se serializan por destinatario dentro de la transacción
con bloqueo de su fila de preferencia, inicializada con el valor predeterminado
si aún no existe: desactivar no deja un correo pendiente insertado por
una operación que leyó una preferencia anterior. Las entregas múltiples toman
esos bloqueos en orden de identificador para conservar un orden común.
