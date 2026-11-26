# Criterios de aceptación — Notificaciones y reportes

Preparados antes de implementar el motor nuevo. No constituyen evidencia de ejecución.
Fuente: `assignments/practica-5.md`; reglas: `reglas-operativas-practica-5.md`.
Escenarios automatizados con dobles y reloj falso; recepción real y persistencia
se verifican aparte mediante instrucciones ejecutadas del README.

| ID | Dado / cuando / entonces y comprobación |
|---|---|
| RF-NOT-01 | Dados Administradores activos, inactivos y Estándar, al crear solicitud válida solo todos los Administradores activos reciben aviso sin leer. Solicitud inválida no produce avisos. |
| RF-NOT-02 | Dada solicitud pendiente, al resolverla el solicitante activo recibe aprobación o rechazo con motivo. Resolución inválida no notifica ni cambia datos. |
| RF-NOT-03 | Dada factura de la sucursal, al dejarla fiada se notifica a Administradores activos. Repetir una operación rechazada no genera otro evento. Verificar también eventos de la matriz operativa. |
| RF-NOT-04 | Dados avisos de dos usuarios, cada uno obtiene solo los propios y distingue vistos. Sin sesión se rechaza. |
| RF-NOT-05 | Dado aviso propio, marcarlo cambia lectura; aviso ajeno se rechaza sin cambios. Marcar todos afecta solo propios; consultar no marca. |
| RF-NOT-06 | Comparar canales con matriz de eventos. Solo eventos administrativos seleccionados generan correo. Estándar recibe únicamente interno. DESVIACIÓN aprobada por negocio: no satisface entrega universal por dos canales; ADR 023 y 024. |
| RF-NOT-07 | Dado Administrador con correo activado, desactivarlo conserva avisos internos, cancela pendientes notificables y evita nuevas entradas de correo. Reactivar no recupera cancelados. Estándar carece de canal notificable por decisión de negocio. |
| RF-NOT-08 | Dado SMTP apagado, crear evento termina y persiste aviso/correo sin llamada SMTP dentro de la operación. |
| RF-NOT-09 | Dado correo pendiente, ejecutar CLI lo procesa sin repetir operación de origen. |
| RF-NOT-10 | Dado transporte que falla, cinco intentos totales llevan a fallido; posteriores ejecuciones no envían. Sin espera adicional ni reenvío manual. |
| RF-NOT-11 | Dados enviados, pendientes, fallidos, cancelados e inciertos, Administrador consulta estado, intentos, fechas y error seguro. Estándar recibe rechazo sin filtración. |
| RF-NOT-12 | Dado enviado, ejecutar procesador dos veces no reenvía. Dos procesadores no reclaman simultáneamente el mismo mensaje. Resultado incierto tras interrupción no se reenvía automáticamente. |
| RF-NOT-13 | Credenciales provienen de entorno; revisión de código e historial no encuentra valores secretos. Agente no lee .env. |
| RF-REP-01 | Dadas solicitudes de varios estados, fechas y sucursales, el reporte agrupa cantidades por estado filtrando por fecha real de solicitud; no devuelve listado ni detalle navegable. |
| RF-REP-02 | Dadas ventas por sucursal, producto y jornada, reportes responden total vendido, cobros, deudas, productos, anulaciones, cierres y empleados según reglas acordadas. No son simples conteos de filas. |
| RF-REP-03 | Dados varios registros del mismo grupo, resultado tiene menos filas y totales coinciden con valores preparados. Agregación ocurre en backend. |
| RF-REP-04 | Dado Estándar autenticado, todos los reportes administrativos rechazan acceso. Administrador consulta sucursales seleccionadas; consultas operativas de deudas respetan sucursal. |
| RF-REP-05 | Dados datos en dos períodos, rangos producen resultados distintos y correctos. Incluyen extremos; negocio usa jornada y permisos fecha real. Rango inválido se rechaza; vacío produce resultado vacío o totales cero según contrato aprobado. |

## Datos de comprobación

- Dos sucursales, dos Estándar de una misma sucursal y otro de la segunda;
  Administradores activos e inactivo. Ningún dato corresponde a credenciales reales.
- Jornada cerrada con dos facturas válidas de 100 y 150: vendido 250.
- Otra jornada abierta con 80: aparece solo en apartado abierto.
- Facturas de 70 anulada, 60 con anulación pendiente y 50 no entregada:
  no incrementan vendido; aparecen en grupos separados.
- Factura fiada de 120: cuenta en unidades e importe de productos vendidos.
- Factura con productos A=100 y B=50: seleccionar A suma 100, no 150;
  seleccionar ambos cuenta una factura, no dos.
- Venta 1000: efectivo 400, transferencia pendiente 300, saldo fiado 300:
  efectivo 400, transferencia pendiente 300 y deuda 300, sin duplicar importes.
- Deuda 1000 con transferencia de abono 500 pendiente: saldo 1000,
  más abonos rechazados; confirmarla deja 500, rechazarla conserva 1000.
- Abono que salda registrado el 11 y confirmado el 13: fecha de pago el 11.
- Abonos no incrementan ventas ni cifras del cierre.

## Jornadas y cambios posteriores

- Primera factura abre una sola jornada incluso con dos solicitudes concurrentes.
- Apertura 11 a las 18:00; venta 12 a la 01:00 sigue en jornada 11.
- Apertura nueva 12 a la 01:00 pertenece al 12, límite 13 a las 16:00.
- Al límite, emitir falla; no bloquea abonos ni resolución de pendientes.
- Factura inconclusa de otro emisor bloquea cierre y lo identifica, sin permitir edición ajena.
- Transferencia pendiente o solicitud de anulación permiten cerrar.
- Reabrir requiere acción explícita, última jornada, antes del límite y sin posterior.
- Reporte identifica reapertura y mantiene fecha; vuelve a abierto hasta nuevo cierre.
- Nuevo cierre reemplaza anterior, notifica y no conserva revisiones de cierres.
- Cambio posterior de factura actualiza reportes de jornada original, no cierre guardado.
- No entregado solo se recupera por emisor; transferencia no salta verificación.

## Avisos temporales y canales

- Antes y exactamente al cumplir cuatro días no hay aviso de atraso;
  primera ejecución CLI después genera uno por transferencia aún pendiente.
- Repetir CLI o reiniciar no repite recordatorio.
- Dos ejecuciones simultáneas dejan un control por caso, un aviso por destinatario
  y un correo por aviso, incluso tras reintentar un fallo (RF-NOT-12, RD-09).
- Fallo al publicar revierte marca, aviso y correo de ese caso; los demás avanzan.
  Un origen que falla no impide la fase de correo (RF-NOT-08, RF-NOT-09).
- Cada pasada confirma como máximo 50 casos nuevos; antiguos/resueltos/fallidos
  no consumen cupo y la siguiente continúa los restantes (RF-NOT-03).
- Sin Administradores activos se registra el caso sin entregas; habilitarlos
  después no recupera ese aviso (RF-NOT-03, RD-09).
- Una transferencia resuelta antes de ejecutar no genera recordatorio vencido.
- Venta, pago parcial y abono se cuentan desde su propio registro; factura anulada
  se excluye, solicitud pendiente no la excluye (RF-NOT-03, RD-11).
- Jornada vencida genera un aviso administrativo por jornada; cada cierre sí notifica.
- Si el caso se resuelve después de encolar y antes de enviar, el correo queda
  Cancelled con cero intentos; persisten aviso y control. Si falla comprobarlo,
  queda Pending sin SMTP ni intento (RF-NOT-09, RF-NOT-12).
- Preferencia de notificaciones no cancela activación ni recuperación.
- Error o rechazo no deja una notificación de éxito ni operación aplicada parcialmente.

## Revisión acumulada

- Alta por Administrador y activación por correo conservan comportamiento actual.
- Inicio/cierre de sesión, roles y recuperación siguen funcionando.
- Permisos crean, aprueban, rechazan y rechazan transiciones inválidas.
- Anulación automática previa sigue funcionando con verificación transaccional real.
- Operación válida de Billing y transición prohibida demostrables.
- Documento sobrevive a detener composición sin borrar volúmenes y levantar de nuevo.
- Contexto y estos criterios tienen commit anterior al motor nuevo de notificaciones.
- README contiene comandos ejecutados, máximo de intentos, fallos reproducibles y filtros.
- Etiqueta y entrega requieren revisión de Franklin; RF-NOT-06 permanece desviado.
