# ADR 022 - Cancel invoices on permission approval

- **Estado:** aceptada
- **Fecha:** 2026-11-13
- **Requisitos:** RF-NEG-06, RF-GP-05, RD-03, RD-04, RD-06
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

El ADR 011 exigía que el estándar volviera a anular la factura después de la
aprobación. La solicitud ya identifica la factura con `CancelInvoice:<id>`.

## Decisión

La aprobación de esa solicitud ejecuta la anulación de la factura pagada o
en deuda a través de un contrato del Core implementado por Facturación.
Facturación valida la transición y guarda sus propios datos. Permisos registra
al aprobador y deja la solicitud en `Applied`. Ambas escrituras participan
en la misma transacción de PostgreSQL. La aprobación aplica el permiso en el
acto, por lo que no se emite una concesión para usar después. Las concesiones
previas siguen siendo consumibles por el flujo existente.

## Alternativas consideradas

- Conservar el segundo paso exigía que el estándar volviera a la factura.
- Guardar por separado podía dejar una aprobación aplicada y la factura vigente.
- Ejecutar en segundo plano introducía una espera entre aprobar y anular.

## Consecuencias

El estándar solicita una vez. La API de aprobación conserva el código `204`
si la operación se completa. Una factura inexistente o en un estado inválido
produce un rechazo y la solicitud sigue pendiente. No requiere migraciones.
