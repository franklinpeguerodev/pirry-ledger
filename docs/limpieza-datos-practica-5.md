# Inventario y preparación de limpieza — Práctica 5

Estado: procedimiento en preparación. No se ejecutó eliminación.
Rama: `chore/reset-operational-data`, desde develop con el PR de planificación integrado.

## Destino y credenciales

Franklin indicó usar la configuración de .env sin leer ni mostrar sus valores.
La carga debe realizarse mediante el mecanismo de ejecución configurado,
sin volcar variables, cadena de conexión, configuración expandida o credenciales.
El agente no inspecciona .env. El nombre real de base/almacenes se confirma en
ejecución sin publicar secretos. No se deduce el destino por el nombre del repo.

## Inventario comprobado en código

| Acción | Tablas u objetos |
|---|---|
| Conservar íntegramente | ac_users, ac_branches y branch_id de usuarios |
| Pendiente de decisión | ac_sessions, ac_activation_tokens, ac_recovery_codes |
| Retirar | bil_invoices, bil_invoice_items, bil_invoice_payments, bil_invoice_edits |
| Retirar catálogo | bil_products, bil_product_images, bil_categories, bil_subcategories |
| Reinicializar | bil_image_revision: conservar la fila técnica requerida por el repositorio |
| Retirar permisos | per_permission_requests, per_permission_grants |
| Retirar correo | not_email_queue |
| Retirar documentos | doc_documents y bytes correspondientes en SeaweedFS |
| Retirar imágenes | Objetos R2 referidos por image_key, incluidos retirados |
| Conservar infraestructura de esquema | Tablas/historial de migraciones; no son datos del negocio |

Nombres verificados en configuraciones EF. No equivale a inventario de base real.
ImageRevisionCounter es consultado mediante SingleAsync: no se puede borrar
su fila sin reinicializarla. Verificar su valor inicial en migración antes del script final.
Usuarios conservan contraseña, rol, estado y atributos actuales; no se muestran hashes.

## Archivo de lectura preparado

`scripts/maintenance/inventory-operational-data.sql` contiene:

- Transacción de solo lectura con instantánea consistente.
- Identificación de base/esquema y conteos por tabla, sin datos personales.
- Claves distintas de imágenes/documentos para preparar el manifiesto de objetos.
- Inventario de tablas reales y relaciones para detectar diferencias con el código.

No se documenta todavía un comando de ejecución: en esta sesión no se localizaron
psql ni docker mediante Get-Command y no se instalarán sin autorización.
El SQL está revisado contra mapeos, pero no ejecutado contra PostgreSQL.

## Secuencia antes de eliminar

1. Confirmar política de sesiones y códigos; revisar que no interrumpa activaciones.
2. Obtener conexión mediante configuración cargada en ejecución, sin imprimir valores.
3. Ejecutar inventario de lectura y revisar tablas nuevas/desconocidas.
4. Guardar localmente manifiestos de objetos antes de borrar sus referencias.
5. Detener operaciones y envío de correo durante la limpieza para evitar carreras.
6. Preparar y revisar SQL transaccional de borrado explícito, sin CASCADE global.
7. Verificar que filas completas de usuarios y sucursales permanecen idénticas.
8. Retirar solo objetos del manifiesto en los almacenes configurados del proyecto.
9. Comprobar resultados; conservar manifiesto de objetos fallidos para no perder referencias.
10. Comprobar acceso y funcionamiento de catálogo vacío; registrar evidencia real.

No hay transacción distribuida entre PostgreSQL, SeaweedFS y R2. El procedimiento
concreto de borrado y recuperación se presentará antes de ejecutarlo. No eliminar
buckets, volúmenes completos ni objetos ajenos. La ausencia de referencias no
demuestra que no existan objetos huérfanos: inventariarlos requiere revisar almacén.

## Verificación disponible

- Configuraciones de tabla y columna contrastadas con el SQL.
- Script de inventario sin DELETE, TRUNCATE ni lectura de columnas de secretos.
- Pendiente: conexión real, conteos, manifiestos, script de eliminación y ejecución.
- No se ejecutaron build ni pruebas: solo documentación y consulta SQL preparatoria.
