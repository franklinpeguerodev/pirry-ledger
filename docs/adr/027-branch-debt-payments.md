# ADR 027 - Branch debt payments

- **Estado:** aceptada
- **Fecha:** 2026-11-20
- **Requisitos:** RF-NEG-06, RD-03, RD-04, RD-06, RD-09, RD-11, RD-12
- **Decide:** Franklin. **Redacta:** el agente, con autorización expresa.

## Contexto

Estándar consulta y abona cualquier deuda de su sucursal, aunque otro emita
la factura. Esto no permite concluir facturas ordinarias de compañeros ni
consultar su historial de abonos. Administrador conserva alcance global.
Los abonos no requieren jornada abierta ni se suman al cierre de ventas.

## Decisión

IDebtRepository ofrece consultas por sucursal separadas del repositorio
ordinario limitado por emisor. ManageDebtPayments obtiene identidad y alcance
del contrato de sesión del Core; nunca del cuerpo de la petición.
Los abonos nuevos exigen Cash o Transfer. Los registros históricos None no
se reclasifican ni cambian ordinales de enums.

Registro, confirmación y rechazo se ejecutan en transacción con SELECT FOR
UPDATE sobre la factura seleccionada y autorizada. Se carga estado fresco
incluso al reutilizar contexto: no se aceptan cambios pendientes de otra
operación y se descartan referencias rastreadas antes de leer bajo bloqueo.
No se utiliza la exclusividad de toda la sucursal para abonos independientes.

Un índice único parcial permite un abono de deuda por transferencia pendiente
por factura. No limita las transferencias del cobro original de venta; la
regla de dominio impide registrar abonos mientras exista cualquier transferencia
pendiente en esa deuda. El bloqueo de fila protege el saldo frente a concurrencia.

Efectivo se confirma al registrar. Transferencia no reduce saldo hasta que
Administrador confirme, aunque él mismo la haya registrado. Rechazo requiere
motivo, conserva saldo y libera nuevos abonos. No se borra el pago rechazado.
Se guardan fecha/usuario de resolución y motivo; no es un módulo de auditoría.
Una deuda pendiente de anulación no admite nuevos abonos; se consulta esa
condición por contrato del Core, sin leer tablas de Permisos desde Billing.

Confirmar el pago que completa el saldo pasa por InvoiceStateMachine a Paid.
La fecha de pago completo se deriva del registro del pago confirmado final,
no del instante posterior de verificación. No cambia emisión ni jornada.
Transferencias parciales originales de una factura fiada también se pueden
resolver individualmente desde el flujo administrativo de deudas.

## Contratos y privacidad

Deuda operativa devuelve factura, sucursal, deudor, fecha de emisión, total,
saldo, indicador de transferencia pendiente e indicador de deuda saldada.
No devuelve colección de pagos ni fechas/actores de los abonos.
El historial administrativo sí incluye pagos y rechazados con su motivo.
La respuesta ordinaria de factura fiada también oculta su colección de pagos
al Estándar, incluidos pagos parciales originales;
la protección no depende de que el cliente oculte campos.
La API existente de registro exige ahora method y devuelve resumen de deuda;
los clientes deberán usar el nuevo contrato cuando se trabaje su integración.
La sobrecarga aislada de RegisterDebtPayment conserva pruebas sin servidor;
producción registrada por AddBusiness utiliza ManageDebtPayments y su transacción.

## Alternativas consideradas

- Reutilizar bloqueo de sucursal: coordina, pero impide abonos simultáneos de
  facturas distintas y acopla innecesariamente a la exclusividad de jornada.
- Bloquear la fila: elegido por Franklin; serializa solo la factura afectada.
- Solo comprobar saldo en memoria: dos procesos pueden aceptar sobreabonos.
- Ampliar el repositorio ordinario a toda la sucursal: expone facturas ajenas.
- Ocultar historial en cliente: el servidor seguiría entregando datos prohibidos.

## Consecuencias

Se requiere migración de metadatos de resolución e índice parcial, sin nuevas
tablas de clientes ni FK hacia usuarios del Core. La suite usa EF en memoria
y dobles de bloqueo; PostgreSQL se comprueba aparte con datos temporales.
Notificaciones y recordatorios se conectarán en sus features; este cambio
todavía no envía avisos ni correo por registrar o resolver un abono.
