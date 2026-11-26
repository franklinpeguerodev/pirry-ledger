# Verificación Docker — Práctica 5

Fecha: 2026-11-26, República Dominicana. Versión: develop 7e53a79, merge del
PR 115. El agente ejecutó las comprobaciones en el Codespace autorizado por
Franklin, sobre una composición inicialmente sin contenedores ni volúmenes.
Esta evidencia complementa [la revisión anterior](aceptacion-practica-5.md).
No cambia código de producción ni decisiones funcionales.

## Entorno y arranque

Docker 29.9.2-2, Compose v5.6.1 y SDK .NET 10.0.403. Se usó exclusivamente
la configuración de ejemplo versionada; no se leyó ni modificó .env.
El intento inicial de composición con configuración implícita falló por
variables requeridas ausentes. No se presenta ese intento como arranque exitoso.
La ejecución posterior con --env-file .env.example construyó y arrancó:

- PostgreSQL 17-alpine, saludable; migrador con salida 0.
- Host y trabajador mailer; Mailpit v1.33.1 y SeaweedFS 4.53.
- Volúmenes pirry-ledger-db-data y pirry-ledger-documents.

Comandos ejecutados en la terminal Bash del Codespace (no PowerShell local):

```sh
docker compose --env-file .env.example up --build -d
dotnet test pirry-ledger.slnx --configuration Release -v q
docker compose --env-file .env.example ps -a
```

La suite terminó con código 0: **709 aprobadas, cero fallidas y cero omitidas**.
Por proyecto: AccessControl 172, Permissions 58, Notifications 74, Documents 40,
Reports 23, Business 335 e Integration 7. El registro no contiene NU1900.
Esto no es una certificación de ausencia de vulnerabilidades. Las pruebas
xUnit corrieron en el SDK del Codespace; Host y dependencias sí en Docker.

## Recorridos contra el Host desplegado

Se crearon solo datos sintéticos: sucursal Docker A, cuentas de prueba,
producto de 100, dos facturas y un documento de texto. Las peticiones se hicieron
con curl y jq; tokens y códigos quedaron en archivos temporales privados,
sin imprimirse ni añadirse al repositorio. No se ejecutó Python.

| Frente | Resultado observado |
|---|---|
| Alta y activación | Login de Estándar sin activar: 401. Correo recibido en Mailpit, enlace usado: 200; reutilización: 400. Login posterior correcto. |
| Sesiones y roles | /me anónimo: 401. Administración de usuarios desde Estándar: 403. Logout: 204 y sesión posterior: 401. |
| Recuperación | Solicitud ResetPassword aprobada; correo recibido en Mailpit; código usado: 204. Sesión anterior invalidada: 401; código repetido: 400; nuevo login correcto. |
| Permisos | Aprobación: 204; segunda aprobación: 400 sin modificar Applied. Rechazo: 204. CancelInvoice aprobado anuló automáticamente la factura. |
| Billing y jornadas | Emitir abrió jornada. Cerrar con factura inconclusa: 409 UnfinishedInvoices. Cobrar: Paid; segundo cobro: 400 sin cambiar importe. Cierre y reapertura explícita correctos. |
| Documento persistente | Subida: 201. Descarga: 200 antes y después de retirar/recrear contenedores, conservando volúmenes; mismo identificador y SHA-256. |

El segundo cobro rechazado conservó Paid con paidAmount 100. La segunda factura
pasó de Paid a Cancelled al aprobar el permiso. Cobrarla después devolvió 400 y
conservó Cancelled. El cierre final quedó isOpen false, sold 100, cash 100 y
cancelled 100. La venta histórica devolvió invoiceCount 1 y sold 100.

Se consultaron las nueve rutas de negocio bajo /api/reports/business y el
reporte Core: Administrador obtuvo 200 y Estándar 403. Se verificaron ventas,
cobros, productos, exclusiones, empleados, deudas pendientes/saldadas, cierres y
ventas abiertas. El rango del día anterior devolvió cero. El filtro de producto
no incluyó Methods. El reporte Core agrupó solicitudes por estado; no se
confunde su total de solicitudes con el número de facturas.

Dos intentos finales de consulta usaron por error /api/billing/reports/sales y
devolvieron 404. Se corrigió la ruta conforme al contrato existente:
/api/reports/business/sales con from, to y grouping=Day devolvió 200 y los
totales anteriores. No se atribuye ese error de comando al producto.

## Persistencia tras recrear contenedores

Se ejecutaron estos comandos, sin -v ni eliminación de volúmenes:

```sh
docker compose --env-file .env.example down
docker volume ls
docker compose --env-file .env.example up -d
sha256sum /tmp/pirry-doc.txt /tmp/doc-before.txt /tmp/doc-after.txt
```

Los dos volúmenes permanecieron. Las tres sumas fueron idénticas:

```text
af949a4caabf0e72dd0256c69d80426745835187138a47dc4c89957ff52a6bea
```

La sesión Estándar siguió siendo válida, el reporte conservó la venta de 100 y
el mismo documento pudo descargarse. Mailpit quedó vacío al recrearse porque
su buzón no tiene volumen; la cola persistida no reenvió correos ya enviados.

## Notificaciones y correo

RF-NOT-01/02/04/05/07: crear solicitud incrementó la bandeja administrativa.
Marcar aviso ajeno devolvió 404 y mantuvo readAtUtc null. Marcar todos los propios
respondió marked 3 y no dejó avisos propios sin leer. Al desactivar el correo del
Administrador, una nueva solicitud incrementó avisos de 4 a 5 y mantuvo la cola
en 5. Se restauró emailEnabled true. Estándar recibió 403 al consultar la cola.

Para controlar las pasadas se detuvo temporalmente mailer y se ejecutó el CLI:

```sh
docker compose --env-file .env.example stop mailer
docker compose --env-file .env.example exec -T app dotnet PirryLedger.Host.dll --send-mail
```

Activación y recuperación llegaron a Mailpit y se utilizaron sus enlaces/códigos.
La activación quedó Sent con un intento y lastError null. No hubo entrega a un
proveedor de correo externo. La extracción inicial del enlace incluyó texto
del mensaje y curl la rechazó; se corrigió la extracción sin exponer el código.

RF-NOT-09/10/11/12: se creó otra activación de prueba y se ejecutó cinco veces
el siguiente comando, sustituyendo el puerto solo para ese proceso:

```sh
docker compose --env-file .env.example exec -T -e PIRRY_LEDGER_SMTP_PORT=1 app dotnet PirryLedger.Host.dll --send-mail
```

Quedó Failed con attempts 5 y error seguro de conexión/autenticación SMTP.
Una sexta pasada con la configuración normal tomó cero mensajes; conservó
Failed y cinco intentos. El comando combinado también tomó/envió cero:

```sh
docker compose --env-file .env.example exec -T app dotnet PirryLedger.Host.dll --process-notifications
docker compose --env-file .env.example start mailer
```

La revisión de atrasos registró cero casos: no había fuentes vencidas en estos
datos. No prueba por sí sola el umbral de 96 horas; sus límites y concurrencia
conservan la evidencia automatizada/PostgreSQL de la revisión anterior.
Al terminar, Host, db, mailer, Mailpit y SeaweedFS estaban activos; migrador
Exited (0). git status --short remoto no mostró cambios.

## Advertencias y cierre

El proceso CLI emitió una advertencia por libgssapi_krb5.so.2 ausente.
No impidió SMTP sin autenticación contra Mailpit: hubo entrega y uso real de
activación/recuperación. No se instaló esa biblioteca ni se afirma soporte
Kerberos verificado. No se probó correo externo ni almacenamiento R2 en esta
ejecución. Los datos sintéticos y volúmenes se conservaron en el Codespace.

Los pendientes de arranque y persistencia Docker de la revisión anterior quedan
verificados por esta ejecución. Sigue pendiente la revisión final de Franklin;
RF-NOT-06 conserva su desviación de negocio sin excepción académica registrada
(ADR 023/024). No se fusionó ningún PR ni se creó/publicó el tag practica-5.
