# ADR 034 - Business report read contracts

- **Estado:** aceptada e implementada; revisión del PR pendiente
- **Fecha:** 2026-11-26
- **Requisitos:** RF-REP-02, RF-REP-03, RF-REP-04, RF-REP-05, RD-03, RD-09, RD-11
- **Decide:** Franklin. Autoriza decisiones 1–8 del bloque.

## Contexto

Los reportes combinan jornadas, facturas, pagos y anulaciones pendientes de
módulos diferentes. Necesitan cifras coherentes mientras el negocio opera.

## Decisión

Billing filtra por fecha de jornada y sucursales antes de agregar sus datos.
Reports usa contratos Core y valida rol/filtros. Cada consulta usa una transacción
PostgreSQL RepeatableRead de solo lectura en la conexión común con participantes
dueños de sus contextos. Se libera antes de serializar la respuesta.
La siguiente página obtiene una lectura nueva. No se conserva un reporte temporal.

Franklin pidió separar resultados entre módulos y respuestas HTTP al revisar el
PR 114. Core.Contracts transporta datos sin atributos JSON ni etiquetas de
presentación. Reports construye páginas públicas sin BranchTotals y filas de
ventas/empleados con la omisión condicional de Methods. La etiqueta de jornada
reabierta pertenece a la respuesta. Se descarta reutilizar el resultado interno
ocultando sus propiedades con JsonIgnore: mezcla transporte entre módulos y
presentación. El mapeo explícito añade tipos, pero conserva el JSON aprobado y
permite evolucionar el resultado interno sin exponer campos accidentalmente.

Productos se seleccionan por nombre histórico exacto y se agrupan por ese nombre.
Empleados se agrupan por usuario y nombre histórico. Una factura cuenta una vez
en totales aunque incluya varios artículos. Subconjunto de productos omite métodos.
Las consultas de deudas pendientes y saldadas son independientes.
Deudas y cierres paginan con offset desde cero, limit 50 por defecto y máximo 100.
Sus cantidades y totales corresponden a todo el filtro de esa consulta.
Totales vacíos son cero y las sucursales seleccionadas sin actividad aparecen con cero.

Se conserva el cierre anterior de jornadas reabiertas como información.
Los totales de cierres suman exclusivamente jornadas cerradas. Cada respuesta
de página es coherente por sí misma, con vigencia nueva en la siguiente consulta.

## Alternativas consideradas

Identificador de producto atravesando nombres históricos frente a nombre exacto:
Franklin eligió nombre histórico. Lecturas independientes frente a una lectura
consistente: eligió consistencia por respuesta. Resultado congelado entre páginas
frente a lectura nueva por consulta: eligió lectura nueva.
Sumar cierres anteriores de reabiertas frente a conservarlos informativamente:
eligió excluirlos de los totales de cerradas.

## Consecuencias

No se añaden tablas ni cachés de reportes. Se proyectan datos necesarios y se
agrega en SQL, reutilizando reglas existentes de ventas y verificando equivalencia
de cifras. Semana lunes–domingo y mes calendario agrupan resultados diarios.
El apartado abierto consulta las jornadas actualmente abiertas por sucursal.
Las pruebas separan cálculo, acceso, SQL real y consistencia concurrente.

El proveedor Billing es opcional para la composición del Core. Su ausencia
produce un rechazo 404 controlado al consultar reportes del negocio. Se conserva
la dependencia unidireccional y la ejecución independiente de los módulos Core.
