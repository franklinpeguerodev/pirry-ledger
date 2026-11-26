-- Vista previa transaccional. ROLLBACK final: no persiste la limpieza.
-- No ejecutar en producción ni convertir a COMMIT sin revisar destino/manifiestos.
-- Sesiones/códigos se conservan por decisión explícita de Franklin.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SET LOCAL search_path = public, pg_catalog;

LOCK TABLE ac_users, ac_branches, ac_sessions, ac_activation_tokens,
    ac_recovery_codes IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE bil_invoice_edits, bil_invoice_items, bil_invoice_payments,
    bil_invoices, bil_product_images, bil_products, bil_subcategories,
    bil_categories, bil_image_revision, per_permission_grants,
    per_permission_requests, not_email_queue, doc_documents IN ACCESS EXCLUSIVE MODE;

-- Instantáneas privadas a la transacción; no imprimir su contenido.
CREATE TEMP TABLE preserved_users ON COMMIT DROP AS TABLE ac_users;
CREATE TEMP TABLE preserved_branches ON COMMIT DROP AS TABLE ac_branches;
CREATE TEMP TABLE preserved_sessions ON COMMIT DROP AS TABLE ac_sessions;
CREATE TEMP TABLE preserved_activation ON COMMIT DROP AS TABLE ac_activation_tokens;
CREATE TEMP TABLE preserved_recovery ON COMMIT DROP AS TABLE ac_recovery_codes;

-- Orden explícito comprobado contra relaciones del inventario real.
DELETE FROM bil_invoice_edits;
DELETE FROM bil_invoice_items;
DELETE FROM bil_invoice_payments;
DELETE FROM bil_invoices;
DELETE FROM bil_product_images;
DELETE FROM bil_products;
DELETE FROM bil_subcategories;
DELETE FROM bil_categories;
DELETE FROM per_permission_grants;
DELETE FROM per_permission_requests;
DELETE FROM not_email_queue;
DELETE FROM doc_documents;
DELETE FROM bil_image_revision;
INSERT INTO bil_image_revision (id, value) VALUES (1, 0);

DO $$
BEGIN
    IF EXISTS ((TABLE ac_users EXCEPT ALL TABLE preserved_users)
        UNION ALL (TABLE preserved_users EXCEPT ALL TABLE ac_users))
        OR EXISTS ((TABLE ac_branches EXCEPT ALL TABLE preserved_branches)
        UNION ALL (TABLE preserved_branches EXCEPT ALL TABLE ac_branches)) THEN
        RAISE EXCEPTION 'Cambió información que debe conservarse.';
    END IF;
    IF EXISTS ((TABLE ac_sessions EXCEPT ALL TABLE preserved_sessions)
        UNION ALL (TABLE preserved_sessions EXCEPT ALL TABLE ac_sessions))
        OR EXISTS ((TABLE ac_activation_tokens EXCEPT ALL TABLE preserved_activation)
        UNION ALL (TABLE preserved_activation EXCEPT ALL TABLE ac_activation_tokens))
        OR EXISTS ((TABLE ac_recovery_codes EXCEPT ALL TABLE preserved_recovery)
        UNION ALL (TABLE preserved_recovery EXCEPT ALL TABLE ac_recovery_codes)) THEN
        RAISE EXCEPTION 'Cambió información de acceso fuera del alcance aprobado.';
    END IF;
END $$;

SELECT 'Vista previa: los cambios se descartan al terminar.' AS resultado;
ROLLBACK;
