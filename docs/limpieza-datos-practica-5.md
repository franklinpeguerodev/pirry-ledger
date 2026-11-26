# Inventario y preparación de limpieza — Práctica 5

Estado: limpieza PostgreSQL y de imágenes referidas ejecutada; objetos sin registro pendientes.
Rama de evidencia: `chore/complete-operational-reset`, desde develop tras integrar vista previa.

## Ejecución final y resultado

Franklin pidió continuar tras integrar la vista previa y decidió conservar sesiones
y códigos. Se usaron configuración y dependencias existentes sin inspeccionar .env
ni imprimir sus valores. Herramienta temporal fuera del repositorio, sin paquetes nuevos.

1. Inspección de lectura: 2 claves de imagen en base y 7 objetos en prefijo products/ de R2.
2. Se guardó manifiesto local fuera de Git, antes del borrado.
3. Se ejecutó el SQL ya verificado dentro de una transacción, con los bloqueos de
   tablas de la vista previa y comprobación de filas completas de identidad/acceso.
4. En esta ejecución se confirmó la transacción en lugar de revertirla.
5. Se retiraron solo las dos imágenes referidas; consultas posteriores devolvieron
   ausencia para ambas claves. No se borraron buckets ni volúmenes.
6. Inventario posterior: 4 usuarios, 2 sucursales, 4 asignaciones, 38 sesiones,
   3 activaciones y 0 recuperaciones, conservados. Cero facturas, líneas, pagos,
   ediciones, productos, imágenes registradas, categorías, subcategorías, solicitudes,
   concesiones, correos y documentos registrados. Contador técnico: una fila.

Salida de ejecución: 0. La conexión y credenciales no se publicaron.
Los bloqueos protegieron la transacción; no se afirma haber detenido servicios
externos de API/SMTP ni impedido operaciones después del commit.
La comparación completa dentro de transacción es evidencia de preservación de
identidad/acceso; no se realizó login con credenciales de usuarios reales.

### Límites pendientes

- Cinco objetos R2 sin registro no se eliminaron: no se deduce su pertenencia
  solamente por compartir prefijo. Su revisión no bloquea implementar propiedad de facturas.
- Cero documentos registrados no demuestra almacén SeaweedFS vacío; objetos
  huérfanos no se inspeccionaron ni borraron.
- Manifiestos permiten rastrear claves; no son copia de seguridad de los bytes.
- No hay prueba de eliminación de versiones históricas en almacenamiento externo.
- No se ejecutaron migraciones ni suite completa: cambió información operativa,
  no código de producción. Las secciones siguientes conservan evidencia preparatoria.

## Inventario real ejecutado

El inventario de solo lectura ya se ejecutó contra la conexión configurada,
cargada mediante DotNetEnv.NoClobber, igual que el Host. No se imprimieron
valores de configuración ni se inspeccionó .env. La herramienta temporal usa
Npgsql y DotNetEnv ya compilados con el Host, sin instalar paquetes.

| Grupo | Filas observadas |
|---|---:|
| Usuarios / sucursales / asignaciones | 4 / 2 / 4 |
| Sesiones / activaciones / recuperaciones | 38 / 3 / 0 |
| Facturas / líneas / pagos / ediciones | 6 / 6 / 4 / 0 |
| Productos / imágenes | 2 / 2 |
| Categorías / subcategorías | 1 / 2 |
| Contador técnico de imágenes | 1 |
| Solicitudes / concesiones | 4 / 1 |
| Cola / documentos | 3 / 0 |

Se contrastaron 18 tablas de negocio/acceso y la tabla de migraciones.
Las relaciones reales incluyen ítems hacia productos: borrar ítems antes de catálogo.
Manifiestos de claves se guardaron localmente fuera del repositorio.
Estos conteos son una instantánea; deben repetirse antes de cualquier borrado.
No prueba ausencia de objetos huérfanos en almacenes externos.

`scripts/maintenance/reset-operational-data.preview.sql` prepara un borrado
explícito con comparación de filas completas de identidad/acceso y ROLLBACK
obligatorio al final. Se ejecutó correctamente con ROLLBACK. No es una limpieza de archivos.
Contador técnico se recrea con id 1 y valor 0, como migración AddProductImages.

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
| Conservar por decisión de Franklin | ac_sessions, ac_activation_tokens, ac_recovery_codes |
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

No se documenta todavía un comando permanente de ejecución: no se localizaron
psql ni docker mediante Get-Command y no se instalarán sin autorización.
El inventario sí fue ejecutado mediante herramienta temporal .NET; el borrado
preparatorio fue verificado con ROLLBACK, sin persistir eliminación. No depende de instalar psql o Docker.

## Secuencia antes de eliminar

1. Conservar sesiones y códigos, según respuesta explícita de Franklin.
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
- Completado: conexión real, conteos, relaciones y manifiestos de registros.
- Vista previa ejecutada: comprobación de filas de identidad/acceso y ROLLBACK correctos.
- Inventario repetido después: mismos conteos, incluidas 6 facturas, 38 sesiones y 3 activaciones.
- Completado: limpieza final PostgreSQL y retiro verificado de imágenes registradas.
- Pendiente: revisión de cinco objetos R2 sin registro y posibles huérfanos SeaweedFS.
- No se ejecutaron build ni pruebas: solo documentación y consulta SQL preparatoria.
