# Contrato de reportes del negocio — Práctica 5

Decisiones 1–8 aprobadas por Franklin. ADR 034. RF-REP-02/03/04/05.
Base de implementación: develop 37f0a76, PR 113 integrado.
Rama: feature/business-sales-reports.

## Consulta y acceso

Todas las rutas GET están bajo /api/reports/business y exigen Administrador.
Sin sesión: 401. Estándar: 403 antes de consultar cantidades.

| Ruta | Parámetros propios | Resultado |
|---|---|---|
| /sales | grouping obligatorio Day, Week o Month; productNames opcional | Venta y facturas por período/sucursal, conjunto y totales |
| /collections | ninguno | Efectivo, transferencias confirmadas/pendientes y saldo fiado de facturación |
| /products | productNames opcional | Nombre histórico, unidades, importe y facturas distintas |
| /exclusions | ninguno | Anulación pendiente, anulada y no entregada, con cantidades e importes |
| /employees | productNames opcional | Usuario + nombre histórico; emitidas, vendidas, importes y exclusiones |
| /debts/pending | offset y limit opcionales | Facturas con deuda pendiente |
| /debts/settled | offset y limit opcionales | Facturas que tuvieron deuda y ya están saldadas |
| /closings | offset y limit opcionales | Último cierre guardado, incluida información de reapertura |
| /open-sales | únicamente branchIds opcional | Venta de jornadas actualmente abiertas por sucursal y conjunto |

Excepto /open-sales, from y to son obligatorios yyyy-MM-dd, inclusivos por
BusinessDate. Sucursales: branchIds repetible, omitir o seleccionar Todas equivale
a las sucursales físicas. UUID vacío o desconocido se rechaza. Totales globales
suman exclusivamente las sucursales seleccionadas. Las etiquetas del selector
usan el directorio actual; facturas y empleados conservan nombres emitidos.

productNames es repetible, exacto y sensible a mayúsculas. Omitir significa todos.
Se valida longitud 1–120 y se eliminan duplicados idénticos del filtro. Nombres
distintos por mayúscula permanecen distintos. Se seleccionan líneas históricas,
independientemente del catálogo actual. Solo aplica en ventas, productos y empleado.
Si hay selección de productos, el JSON omite Methods y no asigna pagos a artículos.

## Cálculo

Reportes históricos incluyen únicamente jornadas cerradas. Semana lunes–domingo
y mes calendario agrupan después del filtro de fechas. Una semana parcialmente
seleccionada contiene únicamente sus jornadas filtradas.

Venta: Paid, InDebt o PendingVerification, excluyendo anulación pendiente.
Anulada y no entregada quedan en grupos propios. Se comparte la definición de
estados incluidos con InvoiceSalesCalculation. Las proyecciones SQL se contrastan
con las mismas cifras esperadas del cálculo operativo.

Cobros: solo PaymentKind.Sale; nunca abonos. Cash confirmado, Transfer confirmado
y Registered separados. Fiado conserva el saldo de la facturación tras los pagos
de venta no rechazados; el saldo actual de deuda se consulta en su reporte propio.

Productos suman precio y cantidad emitidos. Una factura con A=100 y B=50 aporta
100 al seleccionar A y 150 al seleccionar ambos, con una factura en el total.
Los conteos por producto no se suman para obtener facturas globales distintas.
Empleados se agrupan por identificador y nombre histórico exacto.

Anulaciones pendientes y efectivas son grupos excluyentes; no entregados se
muestra aparte. Sus porcentajes administrativos son cantidad de cada tipo de
anulación entre todas las facturas emitidas del filtro completo; cero sin facturas.
Se muestran porcentajes globales y cantidades/importes por sucursal.

Deudas: factura individual, deudor, emisión, jornada, importe original, saldo
confirmado pendiente, transferencia pendiente y fecha de registro del pago que
saldó. Se filtra por jornada original y se muestra fecha de pago; confirmar una
transferencia más tarde no mueve esa fecha. Pendientes y saldadas tienen rutas
independientes. Una transferencia pendiente no reduce el saldo.

Cierres: copia guardada, sin recalcular por cambios posteriores en facturas.
Una reabierta conserva copia, fecha y etiqueta Jornada reabierta, pero no suma
a ClosedTotals. TotalCount incluye las filas informativas; ClosedCount y
ReopenedCount permiten distinguirlas. Al cerrar de nuevo se reemplaza la copia.
Recuperaciones y resolución de anulaciones actualizan cifras de la jornada
original, sin reescribir esa copia de cierre.

## Vacíos, páginas y consistencia

Sucursales seleccionadas sin actividad aparecen con totales cero. Filas inexistentes
de productos, facturas o períodos son listas vacías. No se inventan jornadas.

Deudas y cierres usan offset desde cero y limit predeterminado 50, entre 1 y 100.
Filas, cantidad total, importes y totales por sucursal corresponden al filtro
completo de esa consulta, incluso si la página pedida está vacía.

Una transacción RepeatableRead de solo lectura abarca las consultas de cada
respuesta, incluida selección de sucursales y anulaciones consultadas por contrato.
La conexión común incorpora contextos mediante sus participantes, sin leer tablas
ajenas. La lectura termina antes de serializar o enviar el cuerpo HTTP.
Otra página inicia otra lectura y puede reflejar cambios posteriores.

## Archivos y responsabilidades

- Core.Contracts/Reports/IBusinessReportSource.cs: filtros, agrupación, páginas,
  filas y agregados de las consultas. IReportReadSession.cs: contrato de lectura.
- Business.Application/Ports/IBusinessReportRepository.cs: consultas de Billing.
  UseCases/BillingReportSource.cs: contacto Core y agrupación semanal/mensual.
- Business.Infrastructure/Persistence/BusinessReportRepository.cs: filtros y
  agregaciones SQL, proyecciones de líneas/pagos, paginación y cierre guardado.
  DependencyInjection.cs enlaza repositorio y proveedor.
- Business.Domain/Rules/InvoiceSalesCalculation.cs: estados de venta compartidos
  por el cálculo operativo y los reportes.
- Reports.Application/BusinessReportModels.cs y GetBusinessReports.cs: contratos
  visibles, validación, composición por sucursal, porcentajes y totales.
- Reports.Infrastructure/ReportReadSession.cs y DependencyInjection.cs:
  transacción de lectura y enlace de casos de uso.
- Reports.Api/BusinessReportEndpointRegistration.cs y EndpointRegistration.cs:
  rutas y traducción de errores. Operation.cs y RoleRequirements.cs declaran roles.
- PruebasDeReportesDelNegocio.cs de Billing/Reports, soporte de datos y pruebas HTTP:
  cálculos, alcance, vacíos, nombres, métodos omitidos, páginas y autorización.
- README, current-iteration, criterios, reglas, índice y ADR 034 acompañan el PR.
  La bitácora tiene commit propio.
