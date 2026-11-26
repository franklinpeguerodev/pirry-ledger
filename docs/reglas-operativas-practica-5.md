# Reglas operativas — Práctica 5

Decisiones de Franklin acordadas el 18 y 19 de noviembre de 2026.
Estado: reglas aprobadas; avance de implementación en current-iteration.md.
El enunciado original se conserva separado de estas decisiones del negocio.

## Identidad y alcance

- Administrador global; Estándar asignado a una sucursal física.
- Cada persona, incluidos suplentes, tiene cuenta Estándar propia, sin rol especial.
- Puede haber varios Estándar facturando simultáneamente en una sucursal.
- La factura conserva emisor, sucursal y jornada original.
- El Estándar consulta y concluye sus propias facturas; no las de un compañero.
- Excepción: consulta y abona cualquier deuda de su sucursal, incluida deuda saldada.
- No se traslada un Estándar a otra sucursal; se conserva asignación inicial y cambio a Administrador.
- Los reportes y la cola de correos son administrativos; no accesibles al Estándar.

## Jornadas por sucursal

- Entidad propia, abierta con la primera factura.
- Fecha calendario local de apertura; no cambia al cruzar medianoche.
- Instantes en UTC; interpretación y límites en hora de República Dominicana.
- Apertura el 11 a las 18:00: jornada del 11, límite el 12 a las 16:00.
- Apertura el 12 a la 01:00: jornada del 12, límite el 13 a las 16:00.
- Si continúa abierta al límite, se bloquea solamente emitir nuevas facturas.
- Un aviso a Administradores activos por jornada vencida; el Estándar recibe rechazo al emitir.
- Cualquier Estándar de la sucursal puede cerrar; Administrador selecciona sucursal.
- El cierre comprueba todas las facturas de la jornada.
- Bloquea ante facturas inconclusas, identificando factura y emisor sin conceder acceso ajeno.
- Permiten cerrar: pagadas, fiadas, transferencias pendientes, no entregadas,
  anuladas y solicitudes de anulación pendientes.
- Cada emisor resuelve sus propias facturas inconclusas.
- Se guardan valores al cerrar; no se recalculan por cambios posteriores de facturas.
- Se conserva solo el último cierre, sin historial de revisiones en esta iteración.
- El historial de cierres se reserva al futuro módulo de auditoría.
- Mientras está reabierta, conserva fecha y cierre anterior identificado como jornada reabierta.
- Al cerrar otra vez se reemplaza el resumen por el nuevo cierre.

## Reapertura

- Última jornada cerrada, sin otra posterior y antes de las 16:00 del día siguiente.
- Cualquier Estándar de esa sucursal o Administrador puede reabrir.
- Sin motivo obligatorio; requiere acción explícita mediante el contrato de API.
- Emitir con una jornada cerrada elegible devuelve información para ofrecer reapertura.
- No se reabre automáticamente; después del límite corresponde una jornada nueva.
- Reabrir no notifica; cada nuevo cierre sí notifica.

## Cifras del cierre

- Total vendido, efectivo de ventas, transferencias confirmadas y pendientes separadas,
  saldo dejado fiado e importes excluidos por anulación pendiente, aprobada y no entrega.
- Pagos parciales: recibido separado por método y restante como deuda.
- No incluye abonos de deudas, ni siquiera recibidos en efectivo ese día.
- No incluye fondo inicial, conteo físico, diferencias, retiros ni gastos.
- El efectivo mostrado corresponde a ventas de la jornada, no a todo el dinero físico.

## Deudas y abonos

- Facturas individuales con nombre del deudor; sin agrupación por nombre ni módulo de clientes.
- Pendientes y saldadas accesibles al Estándar de la sucursal sin límite de antigüedad.
- Administrador gestiona deudas abiertas e históricas en cualquier sucursal.
- Abono aplicado únicamente a factura seleccionada; monto mayor que cero y no mayor al saldo.
- Cero, negativo o exceso: rechazo completo sin cambios.
- Efectivo: Estándar de la sucursal o Administrador, sin permiso adicional; saldo baja inmediatamente.
- Transferencia: pendiente hasta confirmación administrativa, aunque la registre un Administrador.
- El mismo Administrador puede registrar y confirmar.
- Mientras está pendiente, bloquea nuevos abonos solo en esa factura y no reduce saldo.
- Rechazo exige motivo, no cambia saldo y habilita nuevamente abonos.
- Abonos permitidos con caja cerrada, sin abrir ni reabrir jornada.
- Al saldar se pasa a pagada; transferencia solo después de confirmar.
- Fecha de saldo completo: registro del abono que lo completa, no confirmación posterior.
- Estándar ve saldo, aviso de abono pendiente o deuda saldada; nunca historial de abonos.
- Administrador ve fecha, monto, método, estado y registrador, incluidos rechazados.

## Anulaciones y no entregados

- Solicitar anulación excluye inmediatamente de ventas y efectivo esperado.
- Distinguir pendiente de aprobación y anulación efectiva sin inventar estado de factura aprobado.
- Aprobar mantiene exclusión; rechazar reincorpora en venta y efectivo de la jornada original.
- No elimina factura ni reescribe cierre guardado; reportes reflejan resultado actualizado.
- No se permiten nuevos abonos mientras haya solicitud de anulación pendiente.
- No entregado queda fuera de ventas y efectivo; puede cobrarse después por su emisor.
- Cobro por transferencia requiere verificación antes de considerarse pagado.
- Venta recuperada corresponde a la jornada original, incluso si ocurre después del cierre.
- Solo el Estándar emisor realiza la recuperación de no entregado.
- Sin flujo de devoluciones monetarias en esta iteración.

## Reportes

- Solo pantalla mediante API; sin trabajo móvil ni exportaciones.
- Negocio: únicamente jornadas cerradas; reabrir las retira hasta nuevo cierre.
- Apartado separado de jornadas abiertas: solo total vendido por sucursal y conjunto.
- Sucursales: una, varias o todas; rango inclusivo por fecha de jornada.
- Semana lunes a domingo; mes calendario. Filtrar antes de agregar.
- Productos: selección múltiple o todos, solo en ventas, productos y facturación por empleado.
- Sumar únicamente líneas seleccionadas; contar factura una vez aunque tenga varios productos.
- Con subconjunto de productos, omitir desglose por método: no distribuir pagos entre artículos.
- Nombre e importe históricos conservados al emitir, no valores actuales del catálogo.
- Ventas: total por jornada/sucursal, excluyendo anulaciones pendientes, aprobadas y no entregados.
- Cobros por método: solo facturación; no abonos. Pendientes separados de confirmado.
- Deudas: factura individual, nombre, saldo y fecha de saldo completo; solo jornadas cerradas.
- Productos: unidades e importe, incluye fiadas; mismas exclusiones de ventas.
- Anulaciones y no entregados: grupos separados, cantidades e importes.
- Porcentajes pendientes y aprobados separados sobre todas las facturas emitidas
  del período y sucursales seleccionados; definir cero cuando no haya facturas.
- Cierres: último cierre guardado por jornada, incluida identificación de reapertura.
- Empleado: facturas, vendido, efectivo, transferencias y fiado; exclusiones separadas.
- Permisos: cantidades por estado, fecha calendario real de solicitud y sucursal.
- Fecha de aprobación informativa en consulta ordinaria; sin detalle navegable desde reporte.
- Consulta operativa de deudas no depende del filtro de jornadas cerradas de los reportes.

## Notificaciones

- Únicamente destinatarios activos; se conserva bandeja propia, sin eliminar notificaciones.
- Marcar individualmente o todas las propias mediante acción explícita; abrir no marca.
- Lectura y preferencias persisten en servidor; nunca resuelven operaciones de negocio.

| Evento | Destinatario interno | Correo |
|---|---|---|
| Solicitud de permiso creada | Administradores activos | Sí, según preferencia |
| Permiso resuelto | Solicitante activo | No para Estándar |
| Transferencia registrada, venta o abono | Administradores activos | No |
| Transferencia con más de cuatro días pendiente | Administradores activos, una vez por transferencia | Sí, según preferencia |
| Transferencia de venta resuelta | Emisor activo | No |
| Transferencia de abono resuelta | Registrador activo, también si es Administrador | No |
| Nueva factura fiada | Administradores activos | No |
| Abono en efectivo | Administradores activos | No |
| Cada cierre | Administradores activos, identificando sucursal | No |
| Jornada vencida sin cerrar | Administradores activos, una vez por jornada | Sí, según preferencia |
| Reapertura o no entregado | Ninguno | No |

- Rechazo de transferencias: motivo obligatorio incluido en notificación.
- Tiempo de cuatro días individual desde registro de cada transferencia, no lote global.
- Avisos por CLI: detectados en siguiente ejecución después del vencimiento.
- Un trabajador ejecuta revisión y luego SMTP; espera 30 segundos al terminar.
- Notifications conserva control propio por tipo/origen, también sin destinatarios;
  no hay entrega retroactiva. No son marcas en Billing ni auditoría general.
- Máximo 50 casos nuevos confirmados por pasada. Registrados, resueltos y fallidos
  no consumen cupo; cada fallo revierte su caso y permite intentar los demás.
- Factura ya anulada no genera atraso; solicitud de anulación pendiente no lo excluye.
- Caso resuelto antes del envío cancela correo pendiente sin intento; control y
  aviso interno permanecen. Error de comprobación deja Pending e interrumpe SMTP.
- Administrador: correo habilitado inicialmente y preferencia individual.
- Desactivar cancela correos notificables pendientes; reactivar solo genera por eventos nuevos.
- Estándar: solo notificaciones internas; activación y recuperación conservan flujos actuales.
- Desviaciones de RF-NOT-06 explícitas en ADR 023 y 024; no equivalen a excepción académica.

## Cola y consistencia

- Operación y notificación en transacción común; SMTP siempre fuera de esa operación.
- Cinco intentos totales; sin espera adicional; quinto fallo termina en fallido.
- Administrador consulta; sin reenvío manual ni reintento automático después del límite.
- Revalidar antes de enviar; cancelación de preferencia no retira correo ya enviado a SMTP.
- Envío interrumpido con resultado incierto: no reenviar automáticamente; mostrar incertidumbre.
- No clasificar incierto falsamente como enviado o fallido.
- Contratos del Core entre módulos, transacciones y restricciones de base para concurrencia.
- Solicitudes conservan sucursal al crearse.
- Reinicios no deben duplicar recordatorios ya registrados.

## Limpieza autorizada y límites

- Conservar usuarios, sucursales y asignaciones; eliminar los demás datos operativos,
  catálogo, categorías, imágenes y documentos del entorno que Franklin confirme.
- Inventariar primero tablas y objetos; procedimiento separado de migraciones automáticas.
- Antes de ejecutar, revisar destino y tratamiento de sesiones, tokens y códigos de acceso.
- No borrar volúmenes completos ni objetos de otros proyectos.
- Sin paquetes nuevos sin autorización; sin infraestructura ajena a lo acordado.

## Sucursal de solicitudes — corrección aprobada

Toda solicitud guarda sucursal obligatoria desde su creación. Estándar usa su
sucursal física y Administrador Todas, determinada por rol. No se acepta null,
UUID vacío ni Todas para un Estándar. No se requiere grupo de solicitudes sin
sucursal en el reporte. Una solicitud administrativa cuenta una vez en Todas.
