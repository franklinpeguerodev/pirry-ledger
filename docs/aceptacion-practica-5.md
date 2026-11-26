# Aceptación acumulada — Práctica 5

Fecha: 2026-11-26, República Dominicana. Versión comprobada: develop 2aa76d6,
merge del PR 114, incluida la separación de contratos internos y respuestas.
Rama de evidencia: chore/practice-5-acceptance. Esta revisión no modifica código
de producción, datos de negocio existentes ni decisiones funcionales.

## Resultado y límites

La suite completa tiene 709 pruebas aprobadas, cero fallidas y cero omitidas.
El Host compila con cero errores y siete advertencias NU1900: no se pudo consultar
el servicio de vulnerabilidades de NuGet. Se repitió restore con acceso de red;
persistió la advertencia. No se desactivó la comprobación ni se afirma que los
paquetes estén libres de vulnerabilidades.

Reportes y cola se comprobaron también en PostgreSQL real con datos temporales.
SMTP se comprobó contra un servidor local de prueba. Estos resultados no prueban
la ejecución en contenedores, la persistencia de documentos tras reiniciarlos
ni la recepción de un enlace de activación/recuperación en un buzón externo.

**La aceptación integral permanece abierta.** Docker no está disponible en esta
máquina; Franklin confirmó esa limitación. RF-NOT-06 conserva la desviación de
negocio de los ADR 023 y 024, sin aprobación académica registrada.
No se creó ni publicó el tag practica-5.

## Comandos ejecutados sobre la versión integrada

```powershell
dotnet test pirry-ledger.slnx --configuration Release
dotnet restore pirry-ledger.slnx -v q
dotnet build Src/Host/PirryLedger.Host --no-restore -v q
dotnet build Src/Core/PirryLedger.Core.Reports/PirryLedger.Core.Reports.Api --no-restore -v q
docker compose up --build
```

| Comprobación | Resultado observado |
|---|---|
| Suite completa | 709 aprobadas; restore emitió NU1900; proceso terminó con código 0 |
| Restore con red | Código 0; persistió NU1900 al consultar api.nuget.org |
| Host | Cero errores, siete advertencias NU1900 |
| Reports.Api por separado, sin construir Host ni Business | Cero errores y advertencias |
| Docker | Comando no reconocido; no se construyeron ni arrancaron contenedores |

| Proyecto de pruebas | Aprobadas |
|---|---:|
| PirryLedger.Core.AccessControl.Tests | 172 |
| PirryLedger.Core.Permissions.Tests | 58 |
| PirryLedger.Core.Notifications.Tests | 74 |
| PirryLedger.Core.Documents.Tests | 40 |
| PirryLedger.Core.Reports.Tests | 23 |
| PirryLedger.Business.Tests | 335 |
| PirryLedger.Integration.Tests | 7 |
| **Total** | **709** |

## Matriz de notificaciones y reportes

Las rutas bajo Tests/ son relativas a la raíz del repositorio. Las pruebas HTTP
usan servidor de pruebas y dobles o EF en memoria; no equivalen a desplegar Host
contra servicios externos. Los criterios originales están en
[criterios-notificaciones-reportes.md](criterios-notificaciones-reportes.md).

| Requisito | Evidencia automatizada ejecutada en la suite | Alcance o límite |
|---|---|---|
| RF-NOT-01 | Notifications.Tests/PruebasDePublicacionDeAvisos.cs; Permissions.Tests/CasosDeUso/PruebasDeSolicitudYNotificacionAtomicas.cs | Audiencia de Administradores activos y operación/aviso atómicos |
| RF-NOT-02 | Notifications.Tests/PruebasDePublicacionDeAvisos.cs; Permissions.Tests/CasosDeUso/PruebasDeRejectPermissionRequest.cs | Resolución y motivo; rechazo sin cambios |
| RF-NOT-03 | Business.Tests/Operaciones/PruebasDeEventosDeFacturacion.cs; Notifications.Tests/PruebasDeAvisosDeAtraso.cs | Eventos del negocio, límites temporales, unicidad y fallos |
| RF-NOT-04 | Notifications.Tests/PruebasDeBandejaDeNotificaciones.cs; Business.Tests/Persistencia/PruebasDeBandejaPorHttp.cs | Solo bandeja propia; consultar no marca |
| RF-NOT-05 | Las mismas suites de bandeja | Marca individual/general y rechazo de aviso ajeno |
| RF-NOT-06 | Política probada en PruebasDePublicacionDeAvisos.cs | **Desviado:** Estándar solo interno y correo administrativo selectivo |
| RF-NOT-07 | Business.Tests/Persistencia/PruebasDePreferenciasPorHttp.cs; Notifications.Tests/PruebasDeRevalidacionDeCorreo.cs | Preferencia administrativa y cancelación; Estándar sin canal por ADR 023 |
| RF-NOT-08 | Notifications.Tests/PruebasDeTransaccionEntreModulos.cs; Permissions.Tests/CasosDeUso/PruebasDeSolicitudYNotificacionAtomicas.cs | Operación encola; no llama SMTP |
| RF-NOT-09 | Notifications.Tests/PruebasDelProcesadorDeCorreos.cs | Proceso separado; SMTP local verificado aparte |
| RF-NOT-10 | Notifications.Tests/PruebasDeLimiteDeCorreos.cs | Cinco intentos; además verificado con repositorio PostgreSQL |
| RF-NOT-11 | Notifications.Tests/PruebasDeConsultaDeCola.cs; Business.Tests/Persistencia/PruebasDeColaPorHttp.cs | Estado/error seguro y acceso administrativo |
| RF-NOT-12 | PruebasDeLimiteDeCorreos.cs; PruebasDeAvisosDeAtraso.cs | No duplicar enviados; inciertos sin reenvío; evidencia PostgreSQL adicional |
| RF-NOT-13 | Notifications.Tests/PruebasDeLaConfiguracionSmtp.cs | Configuración por entorno; revisión de patrones del historial limitada, descrita abajo |
| RF-REP-01 | Reports.Tests/PruebasDelReporteDePermisos.cs; Business.Tests/Persistencia/PruebasDelReporteCorePorHttp.cs | Agregación Core y filtros; PostgreSQL adicional |
| RF-REP-02 | Reports.Tests/PruebasDeReportesDelNegocio.cs; Business.Tests/Persistencia/PruebasDeReportesDelNegocio.cs | Siete familias y ventas abiertas; PostgreSQL adicional |
| RF-REP-03 | Las suites anteriores | Totales, agrupaciones y facturas únicas; agregación SQL verificada |
| RF-REP-04 | PruebasDelReporteCorePorHttp.cs; PruebasDeReportesDelNegocioPorHttp.cs | Nueve rutas de negocio y Core con 401/403; Administrador autorizado |
| RF-REP-05 | Suites de Reports y SQL real | Extremos, rangos, jornadas, vacíos y filtros antes de agregar |

En esta tabla, Notifications.Tests, Permissions.Tests y Reports.Tests corresponden
a Tests/PirryLedger.Core.Notifications.Tests, Tests/PirryLedger.Core.Permissions.Tests
y Tests/PirryLedger.Core.Reports.Tests. Business.Tests corresponde a
Tests/PirryLedger.Business.Tests. Nombres abreviados no son comandos.

## Revisión acumulada de las funcionalidades anteriores

| Frente exigido por la práctica | Evidencia de esta revisión | Pendiente de despliegue |
|---|---|---|
| Alta y activación por cola | AccessControl: PruebasDeAltaPorAdministrador, PruebasDeRolYEstado; Notifications: procesador | Recepción y apertura del enlace con servicios reales |
| Sesiones y rechazo Estándar/Administrador | AccessControl: PruebasDeInicioDeSesion, PruebasDeAccesoHttp, PruebasDeInvalidacionDeCredencial, PruebasDelPuntoUnicoDeAcceso | Repetir recorrido contra Host desplegado |
| Recuperación de contraseña | AccessControl: PruebasDeRecuperacionDeContrasena, PruebasDeAprobacionDeCambioDeContrasena | Recepción/uso del enlace con servicios reales |
| Permisos y transiciones | Permissions: CasosDeUso y MaquinaDeEstados; siete pruebas Integration | Recorrido completo contra Host desplegado |
| Operación de Billing y transición prohibida | Business: Operaciones y MaquinaDeEstados; pruebas HTTP | Recorrido completo contra Host desplegado |
| Documento persistente tras reinicio | Documents: 40 pruebas con dobles/adaptador | **No ejecutado:** subir, detener composición sin borrar volúmenes, levantar y descargar el mismo documento |

Las pruebas con dobles no se presentan como cumplimiento de persistencia tras
reinicio ni recepción externa. La bitácora distingue las comprobaciones reales
anteriores de estas ejecuciones nuevas.

## Verificaciones adicionales ejecutadas

- PostgreSQL, reporte Core: agrupación, filtros por sucursal, extremos temporales
  y estado actual; las solicitudes temporales se revirtieron.
- PostgreSQL, reportes de negocio: ventas, productos, empleados, cobros sin abonos,
  deudas, exclusiones, cierres y abiertas. Lectura RepeatableRead de solo lectura;
  escritor concurrente confirmó sin bloquearse. La misma respuesta conservó 560 y
  la siguiente mostró 470. Escribir desde la lectura fue rechazado con 25006.
  Se retiraron exclusivamente la solicitud, facturas y jornadas de prueba.
- PostgreSQL, cola en esquema temporal: cinco intentos, enviados sin repetir,
  recuperación de Processing a Uncertain y fallo al guardar aceptación sin
  reenvío posterior. Esquema y datos revertidos; transporte simulado.
- SMTP local: aceptación seguida de desconexión, rechazo explícito y pérdida
  de respuesta final. No se enviaron mensajes a destinatarios externos.
- Las comprobaciones externas usan herramientas temporales fuera del repositorio;
  no forman parte de la suite entregable ni sustituyen el recorrido del README.
  Cargaron configuración igual que Host, sin inspeccionar ni mostrar .env.
- Grafo de referencias de los 21 proyectos Core: ninguna referencia directa o
  transitiva a Business. La búsqueda en Core no encontró namespaces Business ni
  prefijos de tablas bil_. Reports.Api compiló independientemente.
- Contexto y criterios: 57d4feb es ancestro de 0788bc7, primer commit de Notification.
  La cola mínima anterior se conserva como antecedente de Práctica 1.
- Sin archivos .env locales ni bin/obj versionados. Revisión de cambios añadidos
  del historial develop sin coincidencias de tokens GitHub, identificadores AKIA
  ni cabeceras de claves privadas. Es una búsqueda de patrones conocidos, no una
  certificación exhaustiva de ausencia de secretos; no se leyeron archivos .env.
- Búsqueda de reloj: las llamadas ejecutables a DateTime.UtcNow de producción
  están en SystemClock; las demás coincidencias encontradas son comentarios.

## Condiciones para cerrar

1. Ejecutar la composición y los seis recorridos de despliegue del README en un
   equipo con Docker, incluida persistencia del documento tras reiniciar.
2. Completar la comprobación de recepción/uso de enlaces de acceso y revisar
   las advertencias de vulnerabilidades cuando NuGet sea accesible.
3. Franklin revisa los resultados y la desviación RF-NOT-06 ya documentada.
   La aprobación de negocio no equivale a excepción concedida por el profesor.
4. Corregir cualquier fallo hallado y autorizar expresamente el tag practica-5.

La fusión del PR de evidencia no convierte los pendientes en pruebas aprobadas.
