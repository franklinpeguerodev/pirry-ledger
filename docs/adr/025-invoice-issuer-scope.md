# ADR 025 - Invoice issuer scope

- **Estado:** aceptada
- **Fecha:** 2026-11-19
- **Requisitos:** RF-NEG-06, RD-03, RD-06, RD-09
- **Decide:** Franklin. **Redacta:** el agente, con autorización expresa en conversación.

## Contexto

Puede haber varios Estándar por sucursal. Cada uno consulta y concluye sus
facturas; conocer el identificador de otra no concede acceso. Administrador
conserva alcance global. El sistema ya establece alcance por sucursal desde sesión.

## Decisión

Guardar IssuedByUserId en Invoice; la API toma identidad de la sesión, nunca
del cuerpo del cliente. Exponer issuedByUserId en respuesta y persistirlo como
columna obligatoria, sin clave foránea hacia tablas de AccessControl (RD-03).
El contrato de alcance existente incorpora UserId. AccessControl lo establece
desde usuario autenticado. InvoiceRepository combina sucursal y emisor para
Estándar tanto al listar como al buscar por ID. Sin identidad/branch en alcance
restringido no devuelve datos. El filtro adicional de sucursal no amplía alcance.

Los casos de uso existentes obtienen la factura por ese repositorio antes de
mutar. Administrador conserva alcance global; pruebas aisladas pueden construir
repositorio sin alcance HTTP usando el constructor existente de infraestructura.
La API de producción usa siempre el alcance scoped registrado en DI.

La migración requiere tabla de facturas vacía: no reconstruye emisores ni
asigna usuario ficticio a datos anteriores. La limpieza revisada precede a ella.

## Alternativas consideradas

- Comprobar identidad en cada endpoint: repite reglas y permite omisiones en rutas.
- Comprobar solamente sucursal: no distingue compañeros de una misma sucursal.
- Ampliar alcance y consultas del repositorio: reutiliza el punto central existente
  y protege listados, lectura y carga previa a operaciones.

## Consecuencias

Factura ajena responde como no encontrada, sin filtrar datos de su existencia.
Se necesita migración e índice sucursal/emisor. La sucursal de Estándar permite
asignación inicial, pero no traslado posterior; conserva versión de credencial al rechazar.
Las deudas tendrán consulta operativa específica por sucursal en su feature:
no se amplía la consulta ordinaria de facturas ni se expone historial a Estándar.
