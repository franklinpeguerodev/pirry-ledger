# Bitácora — Práctica 5

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
