# ADR 026 - Branch business day transactions

- **Estado:** aceptada
- **Fecha:** 2026-11-19
- **Requisitos:** RF-NEG-06, RD-03, RD-04, RD-06, RD-09, RD-11, RD-12
- **Decide:** Franklin. **Redacta:** el agente, con autorización expresa.

## Contexto

La jornada pertenece a sucursal; varios Estándar pueden emitir y cualquiera
de esa sucursal puede cerrar o reabrir. Emisión concurrente no debe crear
dos jornadas abiertas ni persistir una factura fuera de su jornada.
Franklin aprobó entidad propia, cierre guardado, último resumen sin historial
y bloqueo transaccional por sucursal tras comparar alternativas.

## Decisión

BusinessDay pertenece a Billing y conserva fecha local de apertura, instantes
UTC, apertura/cierre y último resumen. El resumen se guarda en la misma fila
como valor propio; reabrir lo conserva y cerrar de nuevo lo reemplaza.
No se crea historial de revisiones: auditoría tendrá un módulo posterior.

Apertura, emisión, cierre y reapertura comparten transacción PostgreSQL y
pg_advisory_xact_lock con clave derivada de sucursal y espacio fijo 530026.
La clave usa hashtextextended del texto UUID; una colisión solo añade espera
entre sucursales, no permite concurrencia insegura. El bloqueo se libera al
terminar transacción. Índice único parcial impide dos jornadas abiertas.
Se usa aislamiento predeterminado READ COMMITTED: consultar la última jornada
después de obtener el bloqueo observa el commit del operador anterior.

Una factura nueva guarda BusinessDayId y nombre histórico del emisor tomado
de sesión. La relación es interna de Billing; no hay FK hacia AccessControl.
BusinessDayId es nullable para compatibilidad con pruebas aisladas:
producción registrada por AddBusiness usa el coordinador al emitir.
Las sobrecargas sin coordinador son para pruebas y consumidores aislados;
no prueban transacciones reales. No se reconstruyen jornadas históricas;
la migración rechaza una instalación que todavía contenga facturas anteriores.

El cierre obtiene todas las facturas de su jornada desde un puerto específico,
sin ampliar el repositorio ordinario restringido por emisor. Solo expone datos
mínimos de pendientes (factura y emisor) y resumen operativo de la sucursal.
Una anulación pendiente se consulta mediante IPendingInvoiceCancellations:
Permisos consulta sus propias tablas y devuelve únicamente IDs solicitados.
El cierre guarda esa situación al calcular; resolución posterior no lo reescribe.

La fecha comercial se calcula con America/Santo_Domingo; límite a las 16:00
locales del siguiente día. El reloj único inyectado entrega UTC. Al límite se
bloquea emisión, pero se permite resolver pendientes y cerrar.

## Alternativas consideradas

- Tabla de control Billing por sucursal y bloqueo de fila: válida, requiere
  datos adicionales aun antes de la primera jornada.
- Solo índice único y reintentos: evita duplicar apertura, pero no coordina
  por sí solo emisión, cierre y reapertura.
- Bloqueo transaccional por sucursal: elegido, sin tocar tablas de otro módulo
  y sin bloquear todo el negocio por una sola sucursal.
- Recalcular el cierre: pierde sus cifras originales ante ajustes posteriores.
- Historial de cierres: queda fuera de esta iteración por decisión de Franklin.

## Consecuencias

Se requiere PostgreSQL para verificar bloqueo real; la suite unitaria usa
dobles y reloj falso. SaveChanges de jornada y factura comparte el mismo
contexto/transacción; fallo revierte ambas y limpia entidades rastreadas.
Operaciones posteriores sobre facturas pueden cambiar ventas actuales sin
reescribir la instantánea cerrada. Abonos se excluyen: fiado del cierre refleja
el importe dejado en deuda por la venta, no cobros de deuda posteriores.

No incluye notificaciones de cierre o vencimiento todavía: llegarán con sus
features y transacción común. Tampoco implementa reportes ni estado nuevo de factura.
