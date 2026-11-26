# ADR 033 - Query-time report aggregation

- **Estado:** aceptada e implementada, pendiente de revisión del PR.
- **Fecha:** 2026-11-26
- **Requisitos:** RF-REP-01, RF-REP-03, RF-REP-04, RF-REP-05, RD-03.
- **Decide:** Franklin, alternativas 1A, 2A, 3A, 5A y 6A.
  La decisión 4A fue sustituida por sucursal obligatoria en PR 112.

## Contexto

El Administrador consulta solicitudes creadas en un período según su estado
actual. Se requieren cuatro estados con cero, desglose por sucursal y total
general. La fecha calendario de solicitud se interpreta en hora de RD.

## Decisión

Permissions filtra y agrupa sus tablas en PostgreSQL mediante contrato público
de Core.Contracts. Reports valida acceso y filtros y compone la respuesta con
cantidades agregadas y datos de sucursales obtenidos por contrato.
Se exigen fechas inicial y final inclusivas. El límite SQL final es exclusivo
al inicio del día siguiente. Cada solicitud cuenta una vez en su estado actual.
Todas corresponde también al grupo de solicitudes administrativas y no se
distribuye entre sucursales físicas.

## Alternativas consideradas

Se eligió agregación al consultar. Una tabla de resúmenes aceleraría lecturas
repetidas pero requeriría mantener sincronización y reconstrucción.
Reconstruir el estado al terminar el período respondería otra pregunta y exigiría
historial temporal. Franklin eligió estado actual de las solicitudes del período.

## Consecuencias

Reports incorpora capas y pruebas propias. Permissions conserva propiedad de
sus tablas. La respuesta incluye cuatro cantidades y total por sucursal y global.
No se agregan tablas. El acceso exige Administrador y sesión válida.

Los nombres actuales se obtienen del directorio de sucursales. Omitir branchIds
o seleccionar el identificador global equivale a todas. Las solicitudes
administrativas tienen un grupo global propio y se cuentan una vez.
