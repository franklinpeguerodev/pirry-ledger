# ADR 028 - Explicit undelivered invoice recovery

- **Estado:** aceptada
- **Fecha:** 2026-11-20
- **Requisitos:** RF-NEG-02, RF-NEG-04, RF-NEG-05, RF-NEG-06, RD-02, RD-04, RD-06, RD-11
- **Decide:** Franklin. **Redacta:** el agente, con autorización expresa.

## Contexto

Una factura de delivery no entregado se excluye de ventas, pero su emisor
puede cobrarla después. Conserva identificación, líneas, importes, emisión y
jornada originales. La definición anterior de NotDelivered como terminal
impedía ese comportamiento. Cancelled debe conservar su carácter terminal.

## Decisión

Franklin eligió la alternativa 2: caso de uso RecoverUndeliveredInvoice y ruta
POST /api/billing/invoices/{id}/recover. Recibe la misma estructura de pagos
que el cobro ordinario, pero representa una acción distinta. El cobro ordinario
rechaza NotDelivered y no sirve como entrada alternativa de recuperación.

Solo el Estándar emisor de la sucursal puede recuperarla. La tabla central
declara Standard; el caso de uso además rechaza alcance administrativo o sin
identidad y comprueba emisor/sucursal. El repositorio conserva su filtro de
propiedad. No se obtiene identidad ni fecha del cuerpo de la petición.

InvoiceStateMachine permite NotDelivered -> Paid con efectivo que cubre el
total y NotDelivered -> PendingVerification con transferencia. Ambas exigen
delivery. Se reutilizan las reglas de importes y construcción de pagos del
cobro existente; confirmar o rechazar transferencia sigue el flujo actual.
No se añade estado ni se cambia el ordinal de los existentes. La tabla pasa
de 11 a 13 transiciones permitidas y de 38 a 36 prohibidas. Cancelled sigue
sin salidas; no se permite recuperar una factura pagada o anulada.

La recuperación no abre ni reabre jornada y no modifica su cierre guardado.
La venta corresponde a la jornada original. InvoiceSalesCalculation centraliza
las cifras de facturación: una solicitud pendiente excluye venta/efectivo;
aprobar conserva exclusión en anulado y rechazar reincorpora cifras originales.
No entregado se excluye hasta recuperarse. Abonos siguen fuera de estas cifras.
El cierre guarda el resultado al cerrarse, sin recalcularlo después.

## Alternativas consideradas

- Reutilizar /collect: menos superficie de API y un solo contrato de cobro,
  pero no expresa la intención de recuperar y mezcla autorizaciones distintas.
- Operación exclusiva: elegida por Franklin. Expresa intención, permite probar
  su autorización separadamente y facilita identificarla en auditoría futura.
  Añade una ruta y caso de uso; se comparte construcción y validación de pagos
  para evitar que diverjan de cobro ordinario.
- Emitir otra factura: descartado por la regla aprobada de conservar factura
  y jornada originales; duplicaría la identidad de la operación.

## Consecuencias

No se requiere migración ni paquete nuevo. Se actualizan las pruebas que
declaraban NotDelivered terminal: los dos pares ahora permitidos tienen pruebas
positivas y de condiciones; las otras cinco salidas siguen rechazándose.
Se conserva la prueba de Cancelled terminal y no se debilitan rechazos restantes.

La regla común queda disponible para futuros reportes, pero este PR no crea
reportes ni notificaciones, historial de recuperación o auditoría general.
La ruta nueva exige integración futura en el cliente, fuera del alcance backend.
