# Reports

Source: Práctica 5, section 1.4. Core permission report implemented; business reports pending.
The assignment text below is kept in the professor's original Spanish.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-REP-01 | Un reporte sobre el Core: solicitudes de permiso por estado y por período, o documentos subidos por usuario. | Devuelve valores agrupados, no el listado de registros. |
| RF-REP-02 | Un reporte sobre tu módulo de negocio. | Responde una pregunta real del dominio, no un conteo de filas. |
| RF-REP-03 | Ambos reportes usan agregación real: agrupaciones, totales o promedios. | El resultado tiene menos filas que los registros que resume. |
| RF-REP-04 | Los reportes respetan el rol de quien consulta. | Un Estándar no obtiene datos de otros usuarios a través de un reporte. |
| RF-REP-05 | Ambos reportes se pueden filtrar por rango de fechas. | Dos rangos distintos producen resultados distintos sobre los mismos datos. |

Business-specific definitions live in `../reglas-operativas-practica-5.md`.
All report endpoints are Administrator-only by Franklin's decision.
