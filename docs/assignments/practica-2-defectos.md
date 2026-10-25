# Defectos publicados — Práctica 2, variante A

Programación III · TDS-007 · ITLA · 2026-C-3
Bloque 3 | Semana 7 | Gestión de permisos | Anexo de la Práctica 2

Un defecto es un cambio pequeño que rompe a propósito el código de Gestión de permisos. No se corrige: se planta, uno por uno, para comprobar que la suite lo detecta. Cómo se planta, se etiqueta y se entrega está en la sección 1.2 de `practica-2.md`.

La variante depende del último dígito de la matrícula (0 a 3, A; 4 a 6, B; 7 a 9, C). La matrícula de este proyecto es 2025-2010: último dígito 0, **variante A**. Este archivo contiene solo esa variante. Cada defecto es un cambio mínimo; si el código expresa la regla de otra forma, se aplica el cambio equivalente y no se toca nada más.

## Variante A

| Defecto | Requisito | Qué plantar en el código | Cómo se comporta la aplicación con el defecto |
|---|---|---|---|
| 1 | RF-GP-06 | En la operación que aprueba una solicitud, sustituye la condición «el estado es Pendiente» por «el estado no es Rechazada». | Una solicitud ya aprobada o ya aplicada puede volver a aprobarse. |
| 2 | RF-GP-06 | En la declaración de transiciones permitidas, agrega la transición Rechazada → Aplicada. | Una solicitud rechazada puede llegar a Aplicada. |
| 3 | RF-GP-07 | Elimina la verificación que impide a un Administrador resolver una solicitud creada por él mismo. | Un Administrador puede aprobar o rechazar su propia solicitud. |
| 4 | RF-GP-02 | En la consulta de solicitudes de un usuario Estándar, sustituye la condición «el solicitante es el usuario que consulta» por «el solicitante no es el usuario que consulta». | Un usuario Estándar ve las solicitudes de otros y no las suyas. |
| 5 | RF-GP-06 | En la declaración de transiciones permitidas, agrega la transición Aprobada → Pendiente. | Una solicitud aprobada puede volver a Pendiente. |
| 6 | RF-GP-04 | En la operación que rechaza, elimina el guardado del motivo. | Una solicitud rechazada queda sin su motivo. |

Estos defectos presuponen que Gestión de permisos implementa RF-GP-01 a 07. Si la regla que toca un defecto no está implementada, el defecto no se puede plantar y ese criterio se califica en 0.
