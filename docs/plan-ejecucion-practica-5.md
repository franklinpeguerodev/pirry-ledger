# Plan de ejecución — Práctica 5

Autorizado comenzar el 2026-11-19. Reglas completas en
`reglas-operativas-practica-5.md`; criterios en `criterios-notificaciones-reportes.md`.
Estado: preparación, limpieza, propiedad, jornadas y abonos integrados.
Ajustes de ventas y recuperación explícita integrados mediante PR 106.
El bloque 6 se entrega por responsabilidades: base transaccional integrada
`feature/cross-module-transactions` (ADR 029) y `feature/notification-inbox`
integradas; eventos y preferencias integrados mediante PR 109 (ADR 030).
Cola ampliada integrada mediante PR 110 (`feature/email-queue-retries`):
cinco intentos, consulta y tratamiento incierto (ADR 031). Tras fallo de exclusión
por sesión, Franklin autorizó bloqueo transaccional exclusivo, verificado.
Recordatorios implementados en `feature/overdue-notification-reminders`, pendientes
de revisión del PR: ADR 032 aprobado, tabla propia de Notifications y CLI combinado.
Los bloques posteriores conservan el orden y alcance de este plan.

## Git y dependencias

Base/destino: develop. Cada rama nace después de integrar sus dependencias.
Agente publica PR; Franklin revisa y aprueba. Sin merge ni tag sin autorización.
Commits atómicos: 100–200 líneas es una orientación, sin mínimo artificial.
Franklin permite superarla cuando el cambio constituye una unidad lógica;
se explica el motivo en vez de partir documentos solo para cumplir el tamaño.
Cada commit funcional incluye pruebas pertinentes; documentación acompaña cambios.
Bitácora en commit separado por PR. Documentos extensos completos cuando forman
una unidad lógica. Contexto/criterios primero.

| Orden / rama | Entrega | Commits principales propuestos |
|---|---|---|
| 0 docs/practice-5-planning | Cierre previo, contexto, fuentes, reglas, criterios y ADR | Record cancellation iteration closure; Define Practice 5 criteria (RF-NOT-01, RF-REP-01); Document email policies (RF-NOT-06); Record planning work |
| 1 chore/reset-operational-data | Inventario, destino, script revisado | Add reviewed operational reset procedure (RD-09); Record reset evidence |
| 2 feature/invoice-ownership | Emisor y alcance | Restrict invoices to their issuer (RF-NEG-06); Preserve standard branch assignment (RD-06); Record ownership work |
| 3 feature/branch-business-days | Apertura, cierre y reapertura | Open branch business days (RD-11); Close with pending invoice checks (RD-04); Reopen eligible business days (RD-06); Record business day work |
| 4 feature/debt-payments | Abonos y visibilidad | Allow branch cash payments (RF-NEG-06); Verify debt transfers (RD-04); Restrict debt history (RD-06); Record debt work |
| 5 feature/invoice-sales-adjustments | Anulaciones y no entrega | Exclude pending cancellations (RF-GP-05); Recover undelivered invoices (RD-04); Record sales work |
| 6 feature/notification-center | Bandeja, eventos y preferencias | Add internal notifications (RF-NOT-04); Notify permissions and billing (RF-NOT-01, RF-NOT-03); Apply preferences (RF-NOT-07); Record notification work |
| 7 feature/email-queue-retries | Cinco intentos y consulta | Limit email attempts (RF-NOT-10); Expose queue status (RF-NOT-11); Handle uncertain delivery (RF-NOT-12); Record queue work |
| 8 feature/overdue-notification-reminders | CLI, tiempos individuales | Notify overdue items once (RF-NOT-03, RD-11); Record reminder work |
| 9 feature/core-permission-reports | Módulo y reporte Core | Aggregate permission requests (RF-REP-01, RF-REP-03); Record Core report work |
| 10 feature/business-sales-reports | Siete reportes y ventas abiertas | Aggregate sales and products (RF-REP-02, RF-REP-05); Add debt and closing reports (RF-REP-02); Show open-day sales; Record report work |
| 11 chore/practice-5-acceptance | Revisión acumulada | Document acceptance evidence; Record final review |

Asuntos definitivos reflejan contenido real e IDs relacionados. Dividir por
responsabilidades comprobables cuando corresponda, sin commits rotos ni cortes artificiales.

## Archivos y contenido por bloque

Rutas relativas a raíz. Nombres nuevos son propuestas de contrato, que se
concretan antes de cada feature. Migraciones se nombran al generarse, no se
inventan identificadores. Revisión de ignorados antes de agregar cada archivo.

### Preparación

- AGENTS.md: no leer .env, comandos de entrada exigidos, autorización de PR.
- docs/current-iteration.md: alcance, fuentes, criterios, secuencia y desviaciones.
- docs/assignments/practica-5.md: enunciado original completo.
- docs/requirements/notifications.md y reports.md: RF originales y procedencia.
- docs/requirements/README.md: índice y estado planificado.
- docs/criterios-notificaciones-reportes.md: dado/cuando/entonces y evidencia esperada.
- docs/reglas-operativas-practica-5.md: todas las reglas de negocio aprobadas.
- docs/plan-ejecucion-practica-5.md: ramas, contenido, pruebas y entrega.
- docs/bitacora-anulacion-automatica.md: cierre y limitación histórica de verificación.
- docs/bitacora-practica-5.md: peticiones, correcciones, verificaciones y resultados.
- docs/adr/023-standard-in-app-notifications.md y 024-selective-administrator-email-notifications.md:
  contexto, alternativas, decisión, desviación y consecuencias.
- docs/adr/README.md: índice; otros ADR solo ante decisión importante autorizada.
- README.md: preparación y luego instrucciones verificadas por feature.
- docs/maquina-de-estados.md: actualizar junto a cambios de estados, no anticiparlos.

### Propiedad y jornadas

Base: Src/Business/PirryLedger.Business/ (capas con prefijo PirryLedger.Business.).
- Domain/Entities/Invoice.cs: emisor, sucursal y jornada histórica.
- Application/UseCases/IssueInvoice.cs, ListInvoices.cs, GetInvoice.cs: identidad
  desde sesión y alcance; edición/cobro/no entrega verifican emisor.
- Application/Ports/IInvoiceRepository.cs e Infrastructure/Persistence/InvoiceRepository.cs:
  consultas filtradas antes de exponer datos.
- Infrastructure/EntityConfigurations/InvoiceConfiguration.cs: persistir emisor.
- Nuevos BusinessDay.cs y BusinessDayClosing.cs: jornada y último resumen.
- Nuevos CloseBusinessDay.cs, ReopenBusinessDay.cs, GetCurrentBusinessDay.cs:
  pendientes, acción explícita, límite local y ausencia de jornada posterior.
- Repositorio/configuraciones: una abierta por sucursal y cierre consistente.
- BusinessDbContext.cs, DependencyInjection.cs, modelos y endpoints: integrar.
- AccessControl/Application/UseCases/AssignUserBranch.cs: impedir traslado posterior.

### Deudas, pagos y ventas

- Invoice.cs e InvoicePayment.cs: método, bloqueo pendiente, saldo confirmado,
  motivo de rechazo y fecha de saldo completo.
- RegisterDebtPayment.cs, ConfirmInvoiceTransfer.cs y RejectInvoiceTransfer.cs:
  permisos y resolución del pago concreto, sin confirmar otros accidentalmente.
- Nuevos ListDebts.cs e historial administrativo: contratos distintos por rol.
- Core.Contracts/Auth/Operation.cs y RoleRequirements.cs en AccessControl:
  declaración centralizada de acceso.
- InvoicePaymentConfiguration.cs y modelos/endpoints: persistencia y visibilidad.
- InvoiceStateMachine.cs y contexto: no entregado recuperable por emisor,
  transferencia verificada; Cancelled sigue terminal.
- CancelInvoice.cs, casos de Permisos y contrato Core: seguimiento de solicitud
  sin tocar tablas ajenas; no abonar durante solicitud pendiente.
- Regla común de ventas: exclusiones y jornada original, sin fórmulas repetidas.

### Notificaciones, cola y recordatorios

Base: Src/Core/PirryLedger.Core.Notifications/.
- Nuevas entidades Notification y NotificationPreference; configuraciones/repositorios.
- ListOwnNotifications, MarkNotificationAsRead, MarkAllNotificationsAsRead,
  UpdateEmailPreference: propiedad, acción explícita y cancelación de pendientes.
- Core.Contracts: publicación y consulta de destinatarios activos. AccessControl
  implementa consulta; Billing/Permissions originan eventos tras éxito transaccional.
- EmailQueueItem.cs, EmailStatus.cs, ProcessEmailQueue.cs y repositorio:
  cinco intentos, fallido/cancelado/incierto y revalidación antes del envío.
- Consulta administrativa segura de cola, sin reenvío.
- Casos de vencimiento individual con marca durable para no repetir recordatorio.
- Host/Program.cs: solo despacho CLI/composición; lógica dentro de módulos.
- docker-compose.yml: adaptar ejecución CLI solo tras revisar contrato concreto.

### Contrato concreto del bloque 8 — ADR 032

- Core.Contracts/Notifications/IOverdueNotificationSource.cs: candidato con tipo,
  origen y vencimiento; consulta, revalidación transaccional y vigencia para SMTP.
- Billing Domain/Rules/OverdueBillingRules.cs: más de 96 horas y límite de jornada.
  Application/Ports/IOverdueBillingRepository.cs y UseCases/BillingOverdueNotificationSource.cs:
  selección y aviso, incluidos abonos; excluye facturas ya anuladas.
- Billing Infrastructure/Persistence/OverdueBillingRepository.cs: consultas solo
  propias; bloqueo de factura/pago y sucursal durante publicación, lecturas actuales.
- Notifications Domain/Entities/OverdueNotificationRecord.cs: identidad, clave
  única por tipo/origen y fecha UTC. No se agregan marcas a entidades de Billing.
- Notifications Application/Ports/IOverdueNotificationStore.cs e INotificationEmailEligibility.cs:
  control durable y comprobación de correo por contrato.
- ProcessOverdueNotifications.cs: máximo 50 casos nuevos confirmados por pasada;
  transacción por caso, omisión de registrados, reversión y continuidad tras fallo.
- OverdueNotificationEmailEligibility.cs y ProcessEmailQueue.cs: comprobar vigencia
  antes del reclamo; resuelto cancela sin intento, error conserva Pending y aborta SMTP.
- ProcessNotifications.cs: revisar atrasos y después procesar cola en una invocación.
- Notifications Infrastructure/Persistence/OverdueNotificationStore.cs y
  OverdueNotificationRecordConfiguration.cs: inserción con conflicto omitido,
  DbSet y migración AddOverdueNotificationRecords con su diseñador/snapshot.
- AccessControl/IUserRepository.cs, UserRepository.cs y NotificationRecipientDirectory.cs:
  consultar destinatarios activos sin reutilizar seguimiento de casos anteriores.
- DependencyInjection.cs de Billing/Notifications: enlazar contratos y casos;
  Host/Program.cs: despachar --notify-overdue y --process-notifications, conservando
  --send-mail. docker-compose.yml: mismo proceso CLI, espera de 30 s tras terminar.
- PruebasDeVencimientos.cs, PruebasDeOrigenDeAtrasos.cs, PruebasDeAvisosDeAtraso.cs
  y PruebasDeRevalidacionDeCorreo.cs: tiempos, estados, cap, rollback, correo y errores.
  Pruebas existentes adaptan constructor y verifican lectura actual de destinatarios.
- README, contexto, reglas, criterios, ADR/índice y bitácora se actualizan en el PR.
  La bitácora va en commit propio. Migración/diseñador son una unidad generada;
  se conserva esa unidad aunque exceda la orientación de 200 líneas.

### Reportes

- Nuevo Src/Core/PirryLedger.Core.Reports con Domain/Application/Infrastructure/Api.
- Nuevo Tests/PirryLedger.Core.Reports.Tests desde inicio.
- Core.Contracts/Reports: filtros y contratos de agregación.
- Permissions agrega solicitudes; Billing agrega sus facturas y jornadas.
- Core nunca referencia Business ni lee sus tablas.
- pirry-ledger.slnx, proyectos y composición: dependencias unidireccionales.
- Siete reportes de negocio más apartado abierto; reglas completas en documento operativo.

## Decisiones técnicas aprobadas

Entidad propia; valores guardados al cerrar; solo último cierre y sin revisiones
(auditoría futura); transacción común operación/notificación; CLI para avisos
con tiempos individuales; contratos Core; transacciones/restricciones de base;
revalidación de envío; resultado incierto sin reenvío; sucursal en solicitud;
no entregado por transferencia se verifica; anulación pendiente bloquea abonos.
No implican un módulo general de auditoría; historial de abonos sigue incluido.
Cada manifiesto y contrato concreto se presenta antes de implementación.

## Limpieza

Conservar usuarios, sucursales y asignaciones; retirar operativos, catálogo,
clasificación, imágenes, documentos, cola y permisos del entorno confirmado.
Inventariar tablas y objetos y presentar destino/script antes de ejecutar.
Aclarar tratamiento de sesiones, tokens y códigos sin asumir que son usuarios.
Sin lectura de .env, borrado global de volúmenes ni objetos de otros proyectos.
Procedimiento independiente de migraciones automáticas; verificar preservación.

## Pruebas, documentación y entrega

- xUnit, nombres españoles, reloj falso y dobles; sin SMTP/base en pruebas unitarias.
- Demostrar fallo antes de pasar; rechazo comprueba estado y datos intactos.
- Ampliar suites por módulo y endpoints; cambios de pruebas terminales se justifican.
- Probar límites de madrugada/16:00, reapertura, emisor, deudas, visibilidad y cálculos.
- PostgreSQL aparte: apertura concurrente, emisión/cierre y abonos sin exceso.
- SMTP aparte: cinco fallos, recepción, preferencia/cancelación e incierto sin reenvío.
- CLI repetida/reinicio: avisos únicos y tiempos individuales.
- Reportes: dos rangos, agregado real, filtros antes de suma y facturas únicas.
- Revisar persistencia, roles, activación, sesiones, recuperación, permisos,
  anulación automática previa y documentos tras reinicio sin borrar volúmenes.
- Build y suite completa con resultados reales; README solo comandos ejecutados.
- Antes de commit: status, diff staged, ignorados, secretos y cohesión del cambio.
- PR: exactamente Qué cambia, Por qué, Cómo probarlo y Qué NO incluye.
- Bitácora separada por PR, distinguiendo agente y comprobación de Franklin.
- Tag practica-5 solo con autorización; RF-NOT-06 declarado desviado.

## Bloque 9 concretado — reporte Core

Rama feature/core-permission-reports desde develop aaa4de7. ADR 033 autorizado.
Core.Contracts/Reports/IPermissionRequestReportSource.cs declara cantidades agrupadas.
Permissions.Infrastructure/Persistence/PermissionRequestReportSource.cs filtra y agrupa
sus propias tablas. IBranchDirectory y BranchDirectory exponen listado por contrato.
Reports agrega sus cuatro proyectos: ReportPeriod, GetPermissionRequestReport y
modelos, AddReports y MapReports. Host compone el módulo y la solución lo incluye.
Operation y RoleRequirements declaran acceso administrativo. Pruebas propias de
Reports y HTTP/enumeración real de rutas verifican comportamiento y autorización.
Documentación acompaña el PR y bitácora tiene commit separado.

## Bloque 10 concretado — reportes del negocio

feature/business-sales-reports nace de develop 37f0a76. Decisiones 1–8 aprobadas,
ADR 034 y manifiesto completo en business-report-contract.md. Contratos/sesión
de lectura, proveedor Billing con proyección SQL y pruebas, composición/API y
pruebas de Reports, documentación y bitácora se separan por responsabilidad.
El proveedor de consultas y la integración API pueden superar 200 líneas como
unidades coherentes con sus pruebas, conforme a autorización previa.

## Bloque 11 — Aceptación acumulada

Rama chore/practice-5-acceptance desde develop 2aa76d6 (PR 114 integrado).
Sin nuevas funcionalidades. docs/aceptacion-practica-5.md registra matriz por
requisito, 709 pruebas de regresión, compilación, SQL real y SMTP local.
README, current-iteration, índices y estado del ADR 034 reflejan integración y
límites actuales. Bitácora en commit separado.

En la revisión local Docker no estaba disponible. Continuación desde develop
7e53a79 (PR 115), rama chore/docker-acceptance: composición real en Codespace,
seis recorridos desplegados, enlaces/códigos recibidos en Mailpit, documento
persistente tras recrear contenedores y suite Linux de 709 aprobadas.
Evidencia: docs/verificacion-docker-practica-5.md. Solo documentación;
bitácora en commit separado. Sin correo externo ni Kerberos verificados.
RF-NOT-06 continúa desviado; faltan revisión final y autorización del tag.
