-- Inventario de lectura: no elimina datos ni muestra credenciales.
-- Ejecutar en la base configurada, tras confirmar el mecanismo de conexión.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;

SELECT current_database() AS base_actual, current_schema() AS esquema_actual;

SELECT 'usuarios a conservar' AS grupo, count(*) AS cantidad FROM ac_users
UNION ALL SELECT 'sucursales a conservar', count(*) FROM ac_branches
UNION ALL SELECT 'asignaciones a conservar', count(*) FROM ac_users WHERE branch_id IS NOT NULL
UNION ALL SELECT 'sesiones: decisión pendiente', count(*) FROM ac_sessions
UNION ALL SELECT 'activaciones: decisión pendiente', count(*) FROM ac_activation_tokens
UNION ALL SELECT 'recuperaciones: decisión pendiente', count(*) FROM ac_recovery_codes
UNION ALL SELECT 'facturas', count(*) FROM bil_invoices
UNION ALL SELECT 'líneas de factura', count(*) FROM bil_invoice_items
UNION ALL SELECT 'pagos', count(*) FROM bil_invoice_payments
UNION ALL SELECT 'ediciones', count(*) FROM bil_invoice_edits
UNION ALL SELECT 'productos', count(*) FROM bil_products
UNION ALL SELECT 'imágenes registradas', count(*) FROM bil_product_images
UNION ALL SELECT 'categorías', count(*) FROM bil_categories
UNION ALL SELECT 'subcategorías', count(*) FROM bil_subcategories
UNION ALL SELECT 'contador de imágenes: reinicializar', count(*) FROM bil_image_revision
UNION ALL SELECT 'solicitudes', count(*) FROM per_permission_requests
UNION ALL SELECT 'concesiones', count(*) FROM per_permission_grants
UNION ALL SELECT 'cola de correo', count(*) FROM not_email_queue
UNION ALL SELECT 'documentos registrados', count(*) FROM doc_documents;

-- Manifiestos para revisar objetos antes de eliminar registros de PostgreSQL.
-- Incluye imágenes retiradas y documentos eliminados lógicamente.
SELECT DISTINCT image_key AS clave_imagen FROM bil_product_images ORDER BY image_key;
SELECT DISTINCT storage_key AS clave_documento FROM doc_documents ORDER BY storage_key;

-- Revisar esquema real y relaciones, sin asumir que coincide con las migraciones.
SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
  AND table_type = 'BASE TABLE'
ORDER BY table_schema, table_name;

SELECT conrelid::regclass::text AS tabla,
       confrelid::regclass::text AS tabla_referida,
       conname AS restriccion
FROM pg_constraint
WHERE contype = 'f'
ORDER BY tabla, restriccion;

COMMIT;
