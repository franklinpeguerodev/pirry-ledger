# Bitácora — Práctica 5

## 2026-11-26 — Separación de resultados internos y respuestas de reportes

- Franklin cuestionó JsonIgnore en los contratos entre módulos y pidió cerrar
  PR 114 y dividir responsabilidades. Se cerró el PR antes de corregirlo.
- Core.Contracts deja de declarar reglas JSON y la etiqueta de cierre.
  Reports construye páginas públicas sin BranchTotals y mapea filas de ventas,
  empleados y cierres. Methods opcional y Status pertenecen a la respuesta.
  Se conserva el contrato HTTP, cálculos, SQL y paginación aprobados.
- Las dos pruebas nuevas fallaron con la implementación anterior: atributos de
  serialización en contratos y reutilización de páginas internas. Después pasan.
- dotnet test pirry-ledger.slnx --configuration Release --no-restore -v q:
  709 aprobadas, cero fallidas/omitidas; Reports 23 y los demás módulos sin cambios.
- dotnet test Tests/PirryLedger.Business.Tests --configuration Release --no-restore --filter FullyQualifiedName~PruebasDeReportesDelNegocioPorHttp -v q:
  10 aprobadas. Verifican omisión de Methods en filas, totales públicos de deuda,
  ausencia de BranchTotals y etiqueta de jornada reabierta.
- dotnet build Src/Host/PirryLedger.Host --no-restore -v q:
  cero errores y advertencias. No se cambió persistencia ni se repitió la prueba
  PostgreSQL del bloque anterior. Docker y aceptación acumulada siguen pendientes.
- El agente ejecutó estas verificaciones; Franklin revisará el resultado en el PR.
  Corrección y documentación en un commit; bitácora en otro. Sin merge ni tag.

## 2026-11-26 — Reportes del negocio y lectura consistente

- Franklin aprobó PR 113. Se verificó integración 37f0a76 y se creó
  feature/business-sales-reports desde develop.
- Aprobó decisiones 1–8. Aclaró que pendientes y saldadas tienen consultas
  separadas. Reabiertas conservan copia sin sumar a cerradas. Cada página
  obtiene lectura consistente propia. Paginación 50, máximo 100.
- ADR 034 autorizado. Contrato y manifiesto en business-report-contract.md.
  Billing agrega sus datos; Reports compone mediante Core.Contracts.
- Se comparten estados incluidos con el cálculo operativo. Productos usan
  nombre exacto y empleado ID + nombre histórico. Facturas distintas no se
  calculan sumando conteos por artículo. Subconjunto de productos omite métodos.
- Se implementó RepeatableRead de solo lectura con participantes de contextos
  en conexión común. Se libera antes de responder. El proveedor Billing puede
  faltar sin impedir composición Core; sus reportes devuelven 404 controlado.
- Pruebas de Billing/Reports verifican cifras, abonos, nombres, grupos, vacíos,
  fecha de pago, páginas, reapertura y recuperación en jornada original.
- Nueve rutas verificadas con 401 y 403; Administrador consultó todas con filtros
  y paginación. Se amplió enumeración de rutas sin debilitar verificaciones previas.
- dotnet test pirry-ledger.slnx --configuration Release --no-restore -v q:
  707 aprobadas, cero fallidas/omitidas. Billing 335, Reports 21, AccessControl 172,
  Permissions 58, Notifications 74, Documents 40 e Integration 7.
- Mutación sumando el cierre reabierto hizo fallar la prueba 7A. Se restauró el
  archivo original y se verificó nuevamente el bloque afectado.
- PostgreSQL real confirmó agregaciones, filtros y páginas. Un escritor confirmó
  anulación y resolución de permiso durante lectura: la respuesta conservó 560,
  siguiente consulta mostró 470. Se comprobó repeatable read:on y rechazo de
  escritura 25006. Solicitud y facturas/jornadas temporales retiradas.
- El verificador externo necesitó regenerar dependencias al incorporar Reports;
  el primer intento no cargó su ensamblado. Se corrigió fuera del repositorio.
  Configuración cargada como Host sin inspeccionar .env. Sin correos externos.
- dotnet build Src/Host/PirryLedger.Host --no-restore -v q: cero errores y
  advertencias. Docker no disponible; contenedores permanecen sin verificar.
- Documentación acompaña implementación. Bitácora en commit separado.
  Unidades de proveedor SQL e integración conservan pruebas aunque superen
  orientación de 200 líneas, conforme autorización previa.
- Verificaciones del agente. Franklin revisa PR. Aceptación acumulada pendiente.

## 2026-11-26 — Reporte Core con agregación al consultar

- Franklin confirmó PR 112 integrado. Se verificó develop aaa4de7 y se actualizó
  feature/core-permission-reports, aún sin commits propios, a esa base.
- Aprobó 1A, 2A, 3A, 5A y 6A, con ADR 033. El caso 4A fue sustituido por
  sucursal obligatoria o Todas para Administrador mediante el fix integrado.
- Se creó Reports con cuatro capas y proyecto de pruebas propio. Permissions
  agrega sus tablas mediante Core.Contracts. AccessControl expone listado de
  sucursales por su directorio. No se agregaron tablas ni bibliotecas distintas
  de las usadas ya en la solución. Restore reutilizó dependencias existentes.
- GET /api/reports/permission-requests exige Administrador. Fechas obligatorias
  RD inclusivas, estado actual, cuatro cantidades con ceros por sucursal y total.
  Todas incluye grupo administrativo una vez. Sucursales inválidas rechazan.
- Una mutación eliminó el último día: falló la prueba temporal. Se restauró.
  La primera revisión completa detectó que el escenario de enumeración de rutas
  aún no mapeaba Reports. Se incorporó ruta y caso de rol, sin debilitar aserciones.
- Pruebas HTTP verificaron 401, 403, Administrador 200, fechas incompletas e
  invertidas, selección válida y sucursal inexistente sin consultar origen.
- dotnet test pirry-ledger.slnx --configuration Release --no-restore -v q:
  672 aprobadas, cero fallidas/omitidas. Reports 7, Billing 314, AccessControl 172,
  Permissions 58, Notifications 74, Documents 40 e Integration 7.
- dotnet build Src/Host/PirryLedger.Host --no-restore -v q:
  cero errores y advertencias.
- PostgreSQL real: GroupBy, extremos, distintos rangos, sucursal y estado actual
  verificados con solicitudes temporales dentro de transacción revertida.
  Herramienta externa con configuración del Host, sin inspeccionar .env.
- README, criterios, contexto, plan, índice y ADR acompañan el PR. Se documenta
  el contrato de parámetros y respuesta. El agente ejecutó las verificaciones.
  Franklin revisa el PR. Reportes de negocio y aceptación acumulada pendientes.
- Inicialización del módulo con capas, integración y pruebas en una unidad lógica
  mayor de 200 líneas, conforme a autorización previa.
- No se ejecutó Docker en esta entrega por ausencia del comando en la máquina.

## 2026-11-26 — Sucursal obligatoria en solicitudes

- Franklin autorizó fix separado desde develop d0a342d antes de reportes.
- Ambos caminos guardan sucursal física del Estándar o Todas para Administrador,
  incluso ascendido desde sucursal. Se rechaza identidad o sucursal ausente,
  vacía y global para Estándar antes de persistir. Fábrica exige Guid no vacío.
- PostgreSQL exige NOT NULL y restricción contra UUID vacío, sin default.
  Inventario previo del entorno: cero solicitudes, nulas y vacías. Migración
  RequirePermissionRequestBranch aplicada y esquema verificado sin inspeccionar .env.
- Pruebas existentes asignan sucursal física y directorio al gateway conservando
  aserciones originales. Se agregaron cuatro casos. Mutación detectada para
  Administrador ascendido; regla restaurada y suite Release verde.
- dotnet test pirry-ledger.slnx --configuration Release --no-restore -v q:
  663 aprobadas, cero fallidas/omitidas. Permisos 58, Integration 7, Billing 313,
  AccessControl 171, Notifications 74 y Documents 40.
- Documentación y ADR 030 actualizado acompañan el fix. Migración/diseñador
  constituyen unidad lógica mayor de 200 líneas. Bitácora en commit propio.
- Verificaciones realizadas por el agente. Revisión de Franklin pendiente.
  Reportes continúan como siguiente bloque. Docker pendiente de verificación.

## 2026-11-26 — Avisos únicos de atraso y trabajador CLI

- Franklin aprobó PR 110; se verificó base develop 5457422 y se creó
  feature/overdue-notification-reminders. Decisiones acordadas el 25 de noviembre:
  control propio de Notifications, trabajador 1A, 2B con espera de 30 s,
  revalidación 3A, continuidad 4A sin duplicados, exclusión 5A y cupo 6B.
- ADR 032 autorizado: una tabla adicional, claves únicas por caso y entrega,
  sin marcas en Billing ni módulo general de auditoría. Máximo 50 casos nuevos
  confirmados; registrados, resueltos y fallidos no consumen cupo.
- Billing expone reglas, candidatos y revalidación por Core.Contracts. Consulta
  sus propias tablas; bloquea factura/pago o sucursal mientras se publica.
  Incluye venta, pago parcial y abono; excluye anulada, no solicitud pendiente.
- Marca, avisos y correos confirman o revierten juntos por caso. Un fallo permite
  continuar los siguientes y la fase SMTP, sin consumir marca ni duplicar al reintentar.
  Sin destinatarios también se registra; no hay entrega retroactiva.
- SMTP revalida origen antes de reclamar: resuelto cancela sin intento; fallo de
  comprobación conserva Pending y aborta esa fase. Aviso y control permanecen.
- Se conservan --send-mail y cinco intentos; se agregan --notify-overdue y
  --process-notifications. Docker espera 30 s tras terminar el combinado, incluido
  SMTP. También cambia la cadencia de activación y recuperación; no implementa push.
- Se adaptaron constructores en pruebas existentes y se amplió la comprobación
  de destinatarios actuales entre casos, sin debilitar aserciones previas.
- Rojo por mutación: >=96 h hizo fallar el caso de límite exacto; se restauró >.
  Otra mutación retiró dos defensas de unicidad: Assert.Single obtuvo dos eventos.
  Se restauró el archivo original y se verificó nuevamente Notifications en verde.
- El agente ejecutó dotnet build pirry-ledger.slnx --no-restore -v q: 0 errores,
  1 NU1900 por consulta de vulnerabilidades de NuGet sin red. Ejecutó dotnet test
  pirry-ledger.slnx --configuration Release --no-restore -v q: 659 aprobadas,
  0 fallidas/omitidas (74 Notifications, 313 Billing, 171 AccessControl,
  54 Permissions, 40 Documents y 7 Integration).
- Prueba externa temporal con configuración de entorno equivalente al Host,
  sin inspeccionar .env: el sandbox impidió SSL; se repitió con autorización
  de red. Se aplicó AddOverdueNotificationRecords en PostgreSQL y verificaron
  consultas, límite, bloqueos frente a otra conexión, simultaneidad sin duplicados,
  rollback después de insertar entrega, reintento y caso sin destinatarios.
  Se retiraron exclusivamente los datos temporales; no hubo correos externos.
- Los tres CLI reales se comprobaron sin atrasos existentes y con bloqueo SMTP
  ocupado: revisión registró cero; combinado y enviador no tomaron mensajes.
- docker compose up --build no pudo ejecutarse: docker no está en PATH. No se
  afirma validación de contenedores ni de una entrega SMTP externa en este bloque.
- Documentación, criterios, reglas, índice y ADR acompañan el cambio. Se mantiene
  desviación RF-NOT-06, reportes y revisión acumulada pendientes. Franklin revisa
  el PR; no se le atribuyen comandos ni se hace merge o etiqueta.
- La migración con diseñador y algunas unidades de origen/procesamiento exceden
  200 líneas para conservar cambios coherentes, conforme a autorización previa.

## 2026-11-21 — Cola acotada y consulta administrativa

- Franklin confirmó aprobado PR 109; el agente verificó integración e6f4f08 en
  develop y creó feature/email-queue-retries desde esa base limpia.
- Se presentaron enviador único y coordinación por mensaje. Franklin eligió
  alternativa 1 y autorizó ADR 031; el alcance es SMTP, no avisos internos.
- Prueba real inicial falló: dos conexiones sin pooling Npgsql usaban el mismo
  pg_backend_pid y ambas obtenían bloqueo de sesión. Se retiró el bloqueo de
  diagnóstico y se comprobó cero residuales. No se ocultó el fallo ni se declaró
  exclusión verificada. Franklin autorizó adaptar a bloqueo transaccional en
  conexión exclusiva, sin escrituras de negocio, tabla o variables nuevas.
- Adaptación real aprobada: primer enviador entra, segundo no entra, finalizar
  libera para siguiente. Se verifica coordinación antes de nuevos reclamos.
  La transacción de coordinación permanece durante SMTP, separada de las
  escrituras de cola y sin bloquear facturación/sucursales.
- Rojo de dominio: quinto fallo esperaba Failed y obtuvo Pending. Se implementó
  límite cinco, Failed/Uncertain conservando ordinales, fechas UTC y error seguro.
  Cada correo tiene un intento por ejecución, sin espera adicional. Inciertos
  nunca se reenvían automáticamente; pérdida de coordinación aborta.
- Reclamo individual durable y resultado guardado por mensaje; una interrupción
  no reclama el resto del lote. Fallo al guardar aceptación conserva Processing
  en PostgreSQL y la siguiente ejecución lo considera incierto.
- Transporte distingue conexión/autenticación fallida o rechazo explícito de
  pérdida de confirmación durante envío. Cerrar sesión fallando tras aceptación
  no cambia el éxito. Textos controlados sin respuestas SMTP crudas ni trazas.
- GET /api/notifications/email-queue: solo Administrador; filtros por estado,
  creación UTC y paginación; metadata sin cuerpos/enlaces de acceso. Consulta
  no modifica datos y filtra errores históricos desconocidos a mensaje genérico.
- Se adaptaron dobles existentes al reclamo individual. La prueba previa de
  fallo ahora usa EmailDeliveryException conocido; se conserva rechazo sin
  cambios y se añade error desconocido -> Uncertain. SavedCount pasa de dos a
  uno porque una ejecución sin mensajes ya no guarda un lote vacío.
- HTTP usa consulta real EF en memoria; se agregó referencia de proyecto a
  Notifications.Infrastructure en pruebas, sin paquetes nuevos. Primer build
  detectó esa referencia faltante y luego setters faltantes del doble de alcance;
  se corrigieron antes de verificar suite completa.
- Migración 20261121055232_AddEmailQueueOutcomes generada con herramienta existente
  y aplicada en PostgreSQL: fechas opcionales sin reconstruir historia; Pending
  históricos con >=5 intentos quedan Failed. No se inspeccionó .env ni se
  imprimieron valores; herramientas cargan configuración igual que el Host.
- Agente ejecutó dotnet build pirry-ledger.slnx --no-restore -v q: 0 errores,
  una advertencia NU1900 por consulta de vulnerabilidades restringida.
  dotnet test pirry-ledger.slnx --configuration Release --no-restore -v q:
  632 aprobadas, 0 fallidas/omitidas: Notifications 62, AccessControl 171,
  Billing 298, Permissions 54, Documents 40, Integration 7.
- PostgreSQL con procesador/repositorio reales y esquema temporal: cinco fallos,
  sexto reclamo rechazado, enviados/inciertos sin repetición, recuperación real
  y fallo al guardar aceptación. Esquema/tablas/datos revertidos, sin procesar
  mensajes existentes. SMTP local en loopback probó aceptación seguida de cierre,
  rechazo explícito y pérdida de respuesta; sin entregas externas.
- Comando real dotnet run --project Src/Host/PirryLedger.Host -- --send-mail:
  ejecutado con bloqueo ocupado, informó enviador activo y terminó sin tomar
  correos. El seed administrativo se deshabilitó en el proceso de prueba.
  No se verificó recepción en buzón externo ni Docker (no disponible en PATH).
- Revisión final detectó seguimiento de destinatario entre mensajes del mismo
  comando. Se añadió lectura actual sin seguimiento al repositorio propio de
  AccessControl y la consulta del directorio la utiliza. Prueba con dos contextos
  desactiva al Administrador después de cargarlo. Mutación a lectura anterior
  produjo Assert.Null fallido; se restauró y verificó. Referencia adicional a
  AccessControl.Infrastructure solo en pruebas, sin paquetes ni consultas ajenas.
- README, iteración, plan, máquina de estados y ADR reflejan comportamiento.
  Recordatorios CLI y reportes siguen pendientes; desviación RF-NOT-06 se conserva.
  Commits lógicos con migración/diseñador juntos; bitácora separada. Evidencia
  ejecutada por agente, sin atribuir comandos a Franklin. Sin merge ni etiqueta.

## 2026-11-20 — Eventos, destinatarios activos y preferencias

- Franklin autorizó alternativa 1 para ADR 030: clave estable en cada aviso,
  índice único evento/destinatario y correo vinculado único, sin tabla de eventos.
  Se continúa feature/notification-center desde develop con PR 108 integrado.
- Activo significa cuenta habilitada, sin exigir sesión abierta. AccessControl
  publica destinatarios por contrato; Notifications centraliza audiencia/canal.
  Estándar recibe interno; Administrador configura correo propio, habilitado
  inicialmente. Desactivar cancela pendientes notificables y no activación/recuperación.
  Se revalidan cuenta, rol y preferencia antes de SMTP.
- Se conectaron solicitudes/resoluciones de permisos, transferencias, fiado,
  abonos y cada cierre. Resolución de abono avisa al registrador, incluso si es
  Administrador, solo internamente. Sucursal queda conservada en solicitud.
- Operación, avisos y correo comparten coordinador explícito. Jornadas/abonos
  conservan bloqueos aprobados; resolver permiso lee bajo bloqueo de fila y
  recarga estado aunque el contexto hubiera consultado antes. Recuperación de
  contraseña conserva emisión posterior a aprobación según ADR 014.
- Rechazo de transferencia de venta exige motivo en API, aplicación y dominio;
  tres llamadas existentes de pruebas incluyen ahora el motivo, sin debilitar
  sus aserciones. Se verifica rechazo sin motivo sin cambiar estado/pagos.
- Migraciones generadas con herramientas existentes: 20261121021247
  AddNotificationDeliveryPolicy, 20261121021256 AddPermissionRequestBranch y
  20261121021304 AddBusinessDayClosingEvent. Fecha UTC corresponde a madrugada
  del 21; fecha local de trabajo es 20. Aplicadas en PostgreSQL sin reconstruir
  datos históricos ni agregar FK entre módulos o paquetes.
- Mutación de clave estable por Guid aleatorio produjo rojo esperado: dos
  destinatarios esperados, cuatro obtenidos. Código restaurado antes de suite.
- Agente ejecutó dotnet build pirry-ledger.slnx --no-restore -v q:
  0 errores, una advertencia NU1900 por consulta de vulnerabilidades bloqueada.
  dotnet test pirry-ledger.slnx --configuration Release --no-restore -v minimal:
  612 aprobadas, 0 fallidas/omitidas (Billing 296, AccessControl 170,
  Notifications 45, Permissions 54, Documents 40, Integration 7).
- Prueba PostgreSQL con DI de producción: matriz, preferencias, cancelación,
  branch de solicitud, cierres distintos, reversión de cobro/pagos por fallo de
  aviso, dos publicaciones simultáneas sin duplicados y bloqueo de resolución.
  Datos temporales revertidos o retirados; sin SMTP, sin inspección de .env
  ni impresión de secretos. La herramienta carga configuración igual que el Host.
- No se comprobó recepción SMTP real ni composición Docker en esta entrega;
  docker no está disponible en PATH. Cola ampliada, cinco intentos, consulta
  administrativa, envío incierto, recordatorios CLI y reportes siguen pendientes.
  RF-NOT-06 conserva desviación explícita ADR 023/024, sin excepción académica.
- Commits por contratos, persistencia, política, productores, API y documentos;
  migraciones con diseñador/snapshot y pruebas completas son unidades coherentes
  que pueden superar 200 líneas según autorización de Franklin. Bitácora separada.
- Franklin revisó alternativas y autorizó ejecución; no se le atribuye correr
  los comandos anteriores. PR sujeto a su revisión; sin merge ni etiqueta.

## 2026-11-20 — Bandeja propia de notificaciones

- Franklin pidió continuar. Se verificó merge 0d183d2 del PR 107 y se inició
  siguiente entrega desde develop limpio. Bandeja separada de eventos/preferencias
  para revisar RF-NOT-04/05 de forma independiente, sin declarar bloque 6 completo.
- Notification y tabla propia; consulta por sesión sin lectura automática.
  Marcado individual y general solo de propios, persistido y con primera fecha
  conservada; aviso ajeno/inexistente no se distingue y no puede modificarse.
- Tres rutas protegidas AnySession, incorporadas también a la prueba de metadatos
  del punto único de acceso. Dos casos HTTP recorren Administrador y Estándar.
- Se generó AddInternalNotifications mediante factory del módulo. El primer
  intento con Host como startup no tenía EF Design; se utilizó Infrastructure,
  que ya tiene ese paquete. No se instaló ningún paquete nuevo.
- Se restauraron referencias existentes tras agregar dependencia de API a pruebas.
  NuGet dejó NU1900 al consultar vulnerabilidades por restricción de red;
  se reintentó fuera del sandbox, sin eliminar la comprobación ni ocultar aviso.
- Build: 0 errores, 1 advertencia NU1900. Suite completa: 575 aprobadas,
  0 fallidas/omitidas (Billing 288, AccessControl 166, Permissions 52,
  Documents 40, Notifications 22 e Integration 7).
- Seis pruebas de bandeja y dos HTTP aprobadas. Mutación del guard de identidad
  produjo fallo esperado por ausencia de NotificationSessionRequiredException;
  código restaurado antes de publicar y suite de bandeja nuevamente en verde.
- Migración aplicada en PostgreSQL. Verificación real: listados propios sin
  marcarlos, rechazo ajeno, marcado individual/general, repetición sin cambiar
  fecha, persistencia en nuevo contexto y aviso ajeno intacto.
  Se retiraron exclusivamente avisos temporales propios. Configuración cargada
  por aplicación sin inspección de .env ni impresión de valores; sin SMTP.
- README, iteración, plan y ADR 023 reflejan alcance parcial. Productores,
  destinatarios activos, preferencias y adaptación de transacciones pendientes.
  Evidencia del agente; Franklin revisa PR. Sin merge ni etiqueta.

## 2026-11-20 — Base transaccional del centro de notificaciones

- Franklin confirmó integración del PR 106 y autorizó continuar. Se verificó
  merge b53babd y se inició desde develop limpio.
- Se presentaron coordinador explícito y extensión de TransactionScope, con
  ventajas/costos. Franklin aprobó alternativa 1 y ADR 029. Solicitó esperar;
  se detuvo sin modificar código y luego indicó continuar.
- Se implementaron contratos IApplicationTransaction, IModuleDatabaseConnection
  e IModuleTransactionParticipant; coordinador PostgreSQL y conexión por ámbito.
  Cuatro contextos incorporan exclusivamente su propia persistencia.
- Cuatro pruebas aisladas pasaron: conexión y transacción únicas, reversión,
  anidamiento exitoso y fallo interno capturado que impide confirmar afuera.
- Rojo comprobado retirando temporalmente CommitAsync: la prueba esperaba una
  confirmación y recibió cero. Se restauró el código y las cuatro pasaron.
- Compilación de solución: 0 advertencias, 0 errores. Suite completa: 564
  aprobadas (Billing 286, AccessControl 163, Permissions 52, Documents 40,
  Notifications 16, Integration 7), 0 fallidas y 0 omitidas.
- Helper temporal con configuración cargada por aplicación, sin inspeccionar
  .env ni imprimir valores: PostgreSQL revirtió escrituras reales de Billing
  y Permisos, vació seguimiento y permitió reutilizar coordinador con éxito.
  Se retiraron exclusivamente sus facturas y solicitudes temporales.
- Entrega como PR previo del centro para revisar persistencia por separado.
  No se conectaron eventos, bandeja o preferencias; tampoco se adaptaron aún
  transacciones explícitas de jornadas, abonos o permisos. No se declara RF-NOT
  completado por esta infraestructura. Sin migración, paquetes ni SMTP.
- Comprobaciones del agente; Franklin revisa PR. Sin merge ni etiqueta.

## 2026-11-20 — Recuperación explícita y ajustes de ventas

- Franklin confirmó integración del PR 105 y pidió continuar el plan. Se
  comprobó merge a26a1cf y se creó feature/invoice-sales-adjustments desde develop.
- Se presentaron dos alternativas: reutilizar cobro o crear operación exclusiva.
  Franklin aprobó alternativa 2 y ADR 028: ruta /recover y caso de uso propio.
- NotDelivered deja de ser terminal y admite Paid o PendingVerification;
  Cancelled sigue terminal. Solo el Estándar emisor recupera su sucursal.
  La ruta /collect sigue rechazando no entregadas.
- Se conserva factura, líneas, precios, emisión y jornada; no abre/reabre caja
  ni modifica cierre guardado. Se comparte construcción y validación de pagos
  con el cobro ordinario. Transferencia conserva verificación administrativa.
- InvoiceSalesCalculation centraliza cifras y exclusiones usadas en cierres:
  pendiente excluye, rechazo reincorpora, aprobación clasifica anulado;
  no entregado se excluye hasta recuperación. Abonos permanecen fuera del cierre.
- Rojo inicial: las dos salidas desde NotDelivered fallaron con Assert.True
  porque la tabla aún las prohibía. Se actualizaron pruebas antiguas de terminal
  y conteo: 13 permitidas, 36 prohibidas y cinco salidas no entregadas rechazadas.
  Se conservó prueba de Cancelled sin salidas; no se debilitó ese requisito.
- Mutación adicional: retirar filtro de anulación pendiente hizo fallar
  SolicitudExcluyeVentaYEfectivoYRechazoLosReincorpora: esperaba 0 y recibió 500.
  Regla restaurada antes de verificación final y publicación.
- Pruebas HTTP reales con EF en memoria: 401 sin sesión, 403 Administrador,
  404 compañero/otra sucursal; rechazos no agregan pagos ni cambian estado.
  Emisor recupera y repetición se rechaza; cierre permanece sin modificación.
- Integración Billing/Permisos: solicitudes reales excluyen; aprobación y
  rechazo actualizan cifras mediante contrato público, sin leer tablas ajenas.
- PostgreSQL real con helper temporal y bibliotecas existentes: recuperación
  en efectivo y transferencia persistidas, confirmación posterior, identidad,
  emisión/jornada y cierre original comprobados desde contextos nuevos.
  Primer intento restringido falló en TLS; ejecución autorizada terminó bien.
  Se retiraron solo facturas/jornada temporales propias; no se leyó .env como
  archivo ni se imprimieron valores. Se usó carga de configuración de aplicación.
- Compilación sin advertencias ni errores. Suite completa: 560 aprobadas,
  0 fallidas y 0 omitidas (Billing 286, AccessControl 163, Permissions 52,
  Documents 40, Notifications 12, Integration 7). Filtro de recuperación/ajustes:
  25 aprobadas; integración: 7 aprobadas. Comandos en README ejecutados.
- Sin migración, paquetes, móvil, notificaciones, reportes o auditoría general.
  Evidencia ejecutada por el agente; Franklin revisa. Sin merge ni etiqueta.

## 2026-11-20 — Abonos por sucursal

- Franklin pidió continuar y consultó progreso: se estimó 30 % del esfuerzo,
  con preparación, limpieza, emisor y jornadas integrados; abonos en ejecución.
- Aprobó bloqueo de fila de factura frente a bloqueo de sucursal y ADR 027.
- Rama feature/debt-payments desde develop con jornadas integradas.
- Se añadieron consulta operativa por sucursal, abonos Cash/Transfer y resolución
  administrativa; no abre jornadas ni incluye abonos en efectivo/ventas del cierre.
- Se oculta historial al Estándar también en respuestas ordinarias de factura.
  Administrador conserva pagos rechazados, motivo y fecha/usuario de resolución.
- La fecha de saldo completo usa registro del pago, no confirmación posterior.
- Rojo demostrado quitando deliberadamente filtro de abono confirmado: prueba
  esperaba PaidAmount 0 y obtenía 500 con transferencia pendiente. Código correcto
  restaurado; no se debilitó prueba para pasar.
- Otra prueba roja detectó pago parcial original visible al Estándar en factura
  fiada. Se corrigió la respuesta ordinaria para ocultar toda su colección de
  pagos; Administrador conserva historial completo y el saldo sigue visible.
- Se actualizó prueba anterior de abono sin método: la regla aprobada ahora exige
  Cash/Transfer, manteniendo comprobaciones de recibido/cambio ausentes.
- Se actualizaron expectativas de autorización antiguas: abonar admite sesión,
  historial/confirmación/rechazo siguen administrativos y se verifican aparte.
  Tres nuevas rutas /api/admin llevan el conteo comprobado de 20 a 23.
- Verificación final: dotnet test pirry-ledger.slnx --no-build --no-restore -v q:
  535 aprobadas, 0 fallidas y 0 omitidas (Business 264, AccessControl 162,
  Permissions 52, Documents 40, Notifications 12 e Integration 5).
- dotnet build pirry-ledger.slnx --no-restore -v q: 0 advertencias, 0 errores.
- Se generó y aplicó AddVerifiedDebtPayments: metadatos e índice parcial, sin
  reclasificar registros históricos ni cambiar ordinales de métodos.
- PostgreSQL con helper temporal y bibliotecas existentes: dos abonos de 600
  sobre saldo 1000 aceptan solo uno, quedando saldo 400 y un pago.
- Otra transacción no toma la misma factura, pero sí otra de la sucursal.
- Reutilizar contexto no resucita pago rechazado; confirmación salda con fecha
  de registro y conserva rechazo anterior; otra sucursal y anulación pendiente rechazan.
- Índice impide dos abonos pendientes sin impedir transferencias múltiples de venta.
- Datos temporales propios retirados. No se inspeccionó .env ni se imprimió
  configuración. No se instalaron paquetes ni se hicieron cambios móviles.
- Notificaciones y recordatorios siguen pendientes de sus features; no se afirma
  entrega de avisos por estos nuevos eventos. Franklin revisa el PR; evidencia del agente.

## 2026-11-20 — Jornadas por sucursal — RD-04, RD-06, RD-09, RD-11

- Franklin pidió continuar y aprobó bloqueo transaccional PostgreSQL por sucursal
  frente a tabla de control, con documentación en ADR 026. Rama desde develop
  con propiedad integrada: feature/branch-business-days.
- Se implementaron entidad propia, apertura por primera factura, fecha local,
  límite UTC equivalente a 16:00 RD, cierre y reapertura explícita.
- Solo último resumen, conservado al reabrir y reemplazado al cerrar; sin auditoría.
- Se consulta anulación pendiente mediante contrato del Core y datos propios de
  Permisos; no hay lectura directa de tablas ajenas. No se adelantaron reportes.
- Rojo real: cierre incorporaba efecto de un abono sobre fiado; prueba esperaba
  60 y devolvía 40. Se corrigió para reflejar deuda originada por la venta,
  excluyendo abonos posteriores del resumen, conforme a reglas acordadas.
- Pruebas de dominio comenzaron antes de entidad (fallo de compilación por ausencia).
- dotnet build pirry-ledger.slnx --no-restore -v q: 0 advertencias, 0 errores.
- dotnet test pirry-ledger.slnx --no-restore -v q: 511 aprobadas, 0 fallidas:
  Business 245, AccessControl 157, Permissions 52, Documents 40, Notifications 12,
  Integration 5. Grupo Jornada: 15 aprobadas.
- HTTP con rutas/filtro reales y dobles/persistencia en memoria: sin sesión 401,
  otra sucursal 403, cierre con factura ajena inconclusa 409 e identificación de
  emisor; cobro permite cerrar y otro Estándar de la sucursal puede reabrir.
- Se generó y aplicó AddBranchBusinessDays. Herramienta temporal con bibliotecas
  ya compiladas del Host, sin paquetes nuevos ni inspección de .env.
- PostgreSQL: dos emisiones simultáneas comparten jornada; pg_try_advisory_xact_lock
  rechaza misma sucursal y acepta otra; fallo tras SaveChanges revierte jornada
  y factura; cierre se recupera en nuevo contexto y admite reapertura/reemplazo.
- Carrera emisión/cierre conserva resultado consistente; contrato de anulaciones
  devuelve solo IDs pendientes solicitados y excluye solicitud rechazada.
- Datos de prueba por sucursales UUID propias retirados; solicitud de prueba
  revertida. No se modificaron usuarios/sucursales reales ni credenciales.
- Quedan para siguientes features notificaciones, CLI, abonos nuevos, ajustes
  generales de ventas y reportes. No se afirma implementada toda Práctica 5.
- La migración y diseñador completos se mantienen juntos por unidad lógica.
- Franklin decide/revisa; comandos y comprobaciones de esta entrada son del agente.

## Propiedad de facturas — RF-NEG-06, RD-06

- Franklin pidió continuar tras integrar la evidencia de limpieza. Rama desde
  develop: feature/invoice-ownership. Autorizó expresamente ADR 025.
- Se persistió IssuedByUserId desde sesión y se amplió alcance sucursal/emisor
  en repositorio. Estándar no lee ni concluye factura de compañero por ID.
- Se conserva alcance global administrativo y se rechaza traslado de Estándar
  sin cambiar su sucursal o versión de credencial.
- Rojo: tras agregar soporte de identidad y antes de filtro, 3 de 4 pruebas
  de propiedad fallaron por exposición de datos y modificación ajena; administrador pasó.
- Rojo de traslado: 1 prueba falló antes de agregar la regla de rechazo.
- Verde: 5 pruebas de propiedad, incluida HTTP con sesión/filtro/repositorio reales
  y persistencia en memoria; prueba comprueba rechazo sin modificar estado/pagos.
- dotnet build pirry-ledger.slnx --no-restore -v q: 0 advertencias, 0 errores.
- dotnet test pirry-ledger.slnx --no-restore -v q: 496 aprobadas, 0 fallidas:
  Business 230, AccessControl 157, Permissions 52, Documents 40, Notifications 12,
  Integration 5. Grupo PruebasDeSucursales: 3 aprobadas.
- Se generó AddInvoiceIssuer con fábrica de Infrastructure. Intentos iniciales
  corrigieron una opción no válida y startup Host sin paquete Design; no se instalaron paquetes.
- Migración aplicada en base configurada con comprobación previa de cero facturas:
  salida 0 y columna obligatoria verificada. No se imprimió configuración ni se inspeccionó .env.
- Se rechaza migrar con facturas previas sin emisor; no se inventa atribución histórica.
- La migración con diseñador se mantiene en unidad lógica aunque supere guía de líneas.
- No se ejecutó Flutter ni flujo manual con credenciales reales. Deudas compartidas,
  jornadas y siguientes funcionalidades permanecen en sus ramas planificadas.
- Verificaciones descritas son del agente; revisión del PR corresponde a Franklin.

## Continuación — Limpieza ejecutada

- Franklin pidió continuar tras integrar el PR de vista previa; el agente comprobó develop.
- Se inspeccionaron base y R2: 2 imágenes registradas, 7 objetos en prefijo de productos.
- Herramienta temporal con Npgsql/DotNetEnv/AWSSDK compilados del Host, sin paquetes nuevos.
  Carga en ejecución sin inspeccionar .env ni imprimir configuración.
- Se ejecutó borrado transaccional revisado: salida 0, comparación de filas completas
  de usuarios, sucursales, sesiones y códigos correcta; contador reinicializado.
- Se eliminaron 2 imágenes del manifiesto y se verificó ausencia por sus claves.
- Inventario posterior: 4 usuarios, 2 sucursales, 38 sesiones y 3 activaciones;
  operativos vacíos y una fila técnica del contador de imágenes.
- Se conservaron 5 objetos R2 sin registro para revisar pertenencia; no se inspeccionaron
  huérfanos SeaweedFS. No se afirma almacenamiento externo vacío.
- No hubo login real, cambios de código de producción, migraciones ni ejecución de suite completa.
- Franklin no ejecutó comandos atribuidos en esta entrada; resultados son del agente.

## Verificación de vista previa

- Franklin decidió conservar sesiones y códigos de activación/recuperación.
- El agente ejecutó la vista previa mediante herramienta temporal .NET con
  --preview: salida 0, comprobaciones de filas completas de identidad/acceso
  correctas y ROLLBACK final. No se confirmó ningún borrado.
- Se repitió inventario: mismos conteos de usuarios, sucursales y operativos;
  permanecen 38 sesiones, 3 activaciones y 6 facturas.
- Esta evidencia verifica SQL y reversión en PostgreSQL, no eliminación final
  ni limpieza de imágenes R2/objetos SeaweedFS.

## Continuación — Inventario real

- Franklin indicó continuar; PR de inventario comprobado integrado en develop.
- El agente encontró dotnet y bibliotecas Npgsql/DotNetEnv del Host ya compiladas.
  Preparó herramienta temporal fuera del repo, sin paquetes nuevos, para ejecutar
  el SQL de lectura con carga de configuración equivalente al Host.
- Primer intento restringido falló al conectar; ejecución con permiso de red
  terminó correctamente. No se imprimieron valores secretos ni se inspeccionó .env.
- Resultado: 4 usuarios, 2 sucursales, 6 facturas, 2 productos, 2 imágenes,
  38 sesiones y 3 activaciones. Conteos y relaciones documentados en limpieza.
- Preparado borrador de eliminación PostgreSQL con ROLLBACK y preservación de
  identidad/acceso; no ejecutado. No se borraron filas ni objetos externos.
- Pendientes: decisión de sesiones/códigos y limpieza de almacenes con manifiesto revisado.

## Continuación — Inventario de limpieza

- Franklin aprobó el PR de planificación; el agente comprobó su integración en develop.
- Aclaró que 100–200 líneas es orientativo y puede superarse con unidad lógica.
  Se corrigieron AGENTS.md, iteración y plan para evitar cortes artificiales.
- Se creó chore/reset-operational-data y se contrastaron mapeos EF y almacenes:
  imágenes en R2, documentos en SeaweedFS, datos en PostgreSQL.
- Se preparó inventario SQL de solo lectura y procedimiento de revisión.
- Franklin indicó usar propiedades de .env sin leerlas; no se inspeccionó el archivo.
- No se encontraron psql ni docker en PATH. Inventario real y eliminación pendientes.
- Sesiones y códigos conservados provisionalmente hasta decisión explícita.
- Contador de imágenes requiere reinicialización, no desaparición de su fila técnica.
- No se borraron datos ni se ejecutaron migraciones. No hay pruebas de limpieza exitosa.

## 2026-11-19 — Preparación autorizada

- Franklin pidió cerrar la iteración anterior, leer la Práctica 5 y acordar
  reglas antes de modificar archivos. La conversación resolvió jornadas,
  emisor, deudas, transferencias, anulaciones, reportes y canales de avisos.
- Corrigió exclusividad de cajero: varios Estándar pueden facturar a la vez;
  suplentes tienen cuentas normales propias, sin rol especial.
- Aprobó caja por sucursal, fecha calendario de apertura, último cierre sin
  revisiones, cinco intentos SMTP y resultado incierto sin reenvío automático.
- Autorizó dos ADR para desviaciones de correo; no son excepción académica.
- Autorizó iniciar documentación, ramas por feature desde develop, commits
  atómicos de máximo 200 líneas, push y creación de PR por el agente.
- El agente verificó develop limpio e historial de integración de anulación;
  leyó código actual y PDF de cinco páginas sin leer .env.
- Se redactaron fuentes, criterios previos a código, reglas, plan y ADR.
- Documentación distingue implementación pendiente y desviación de RF-NOT-06.
- La limpieza de datos está autorizada en alcance, pero requiere destino e
  inventario revisados antes de ejecutarse. No se ejecutó limpieza ni migración.
- No se ejecutaron build, pruebas o Docker: esta fase solo modifica documentos.
- Franklin revisó reglas en conversación; no se le atribuye ejecución de comandos.
- Se comprobarán diff, vínculos locales y tamaño de cada commit antes de publicar.
- La aprobación final de este PR corresponde a Franklin; sin merge ni tag del agente.
