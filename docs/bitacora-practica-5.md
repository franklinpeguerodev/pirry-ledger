# Bitácora — Práctica 5

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
