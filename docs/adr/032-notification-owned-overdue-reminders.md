# ADR 032 - Notification-owned overdue reminders

- **Estado:** aceptada e implementada; pendiente de revisión del PR
- **Fecha:** 2026-11-25
- **Requisitos:** RF-NOT-03, RF-NOT-07, RF-NOT-08, RF-NOT-09, RF-NOT-12, RD-03, RD-09, RD-11
- **Decide:** Franklin. **Redacta:** el agente, con autorización expresa.

## Contexto

Transferencias pendientes durante más de cuatro días y jornadas abiertas al
vencer su límite requieren un aviso único. Un reinicio, repetición o ejecución
simultánea no debe duplicar el aviso ni su correo. Billing conoce el estado y
las reglas de vencimiento, pero no debe guardar marcas de notificación.

## Decisión

Franklin confirmó una tabla específica de control en Notifications, frente a
marcas en las entidades de Billing o depender solo de avisos por destinatario.
No es un registro general de eventos ni un módulo de auditoría. Complementa
ADR 030: mantiene claves únicas evento/destinatario y correo vinculado único.
El control de atraso tiene clave única por tipo/origen e instante UTC registrado.
También se consume el evento sin destinatarios activos, sin entrega retroactiva.

Billing expone candidatos y revalidación mediante Core.Contracts. Notifications
no consulta sus tablas. El registro, publicación y encolado comparten la
transacción explícita ADR 029, individual por caso. Billing revalida bajo los
bloqueos de factura o sucursal aprobados. Un conflicto de clave se omite; un
fallo revierte el caso completo y permite continuar con los demás.

Franklin aprobó 1A, 2B con 30 segundos, 3A, 4A, 5A y 6B. Una invocación del
backend revisa atrasos y después procesa correos. Docker espera 30 segundos
tras finalizar antes de repetir. Se conservan CLI independientes y el límite
existente de 50 correos. No se introduce Worker Service ni servicio hospedado.

Cada pasada registra como máximo 50 casos nuevos. Los ya registrados, los que
dejaron de aplicar y los fallidos no consumen ese cupo. Se toman candidatos de
la revisión en orden de vencimiento y se continúa tras fallos individuales.
Un fallo global de un origen se informa y no impide intentar los demás orígenes
ni la cola SMTP. Cancelación de ejecución sí se propaga.

Transferencias: estrictamente más de 96 horas desde registro, tanto venta como
abono, todavía pendientes y factura no anulada. Una solicitud de anulación
pendiente no las excluye. Jornadas: abiertas desde el límite de las 16:00 RD
del día siguiente a su fecha inicial. El reloj se captura por revisión en UTC.
No se modifican estados, importes, emisión, jornada, ni cierre del negocio.

Antes de reclamar un correo notificable de atraso se revalida su caso por el
contrato de origen. Si se resolvió, se cancela el pendiente sin consumir intento;
aviso interno y control permanecen. Un fallo de comprobación no equivale a caso
resuelto: deja correo pendiente y aborta esa ejecución SMTP sin exponer detalles.
No se retira correo aceptado por SMTP. Push al dispositivo queda fuera.

## Alternativas consideradas

- Control propio de Notifications: elegida; una tabla adicional, sin marcas en
  entidades de negocio y con procesamiento independiente de destinatarios.
- Columnas en InvoicePayment/BusinessDay: menos tablas, pero seguimiento de
  notificaciones dentro del modelo del negocio. Descartada por Franklin.
- Solo not_notifications: protege entregas por destinatario, pero no registra
  evaluación sin destinatarios y vincula seguimiento a la existencia del aviso.
- Dos trabajadores: independencia de cadencias y SMTP lento, a costa de otro
  proceso. Franklin eligió uno combinado y aceptó 30 segundos entre ejecuciones.
- Detener al primer fallo: impide avanzar otros casos. Se eligió transacción
  por caso y continuidad, conservando unicidad en base y en entrega.

## Consecuencias

El modelo suma una tabla de Notifications: 22 tablas sin contar historial EF.
La cadencia SMTP cambia de 10 a 30 segundos de espera; afecta también activación
y recuperación, conservando cinco intentos y resultado incierto sin reenvío.
Las lecturas de candidatos no son el límite de generación: se consultan sus
identidades y vencimientos y solo se registran 50 casos nuevos por pasada.
Se documenta el coste sin afirmar una mejora de CPU no medida.
Las pruebas separan límites temporales, resolución concurrente, fallo/reversión,
ausencia de duplicados, pagadas/anuladas, cupo y cancelación previa a SMTP.
