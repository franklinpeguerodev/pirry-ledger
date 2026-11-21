# ADR 023 - Standard in-app notifications

- **Estado:** aceptada por Franklin; bandeja y productores de eventos implementados.
- **Fecha:** 2026-11-19
- **Requisitos:** RF-NOT-02, RF-NOT-04, RF-NOT-06, RF-NOT-07
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

Franklin indicó que el correo operativo corresponde a la empresa. Los Estándar
consultan avisos en la aplicación. RF-NOT-06 exige dos canales para todo aviso.

## Decisión

Notificaciones de negocio y permisos para Estándar solo dentro del sistema,
incluidas confirmaciones y rechazos de transferencias. Se mantienen sin cambio
los correos de activación y recuperación de contraseña.

## Alternativas consideradas

- Ambos canales para todos: cumple el enunciado, contradice política elegida.
- Preferencia de correo para Estándar: configura ambos canales, pero no reserva
  correo operativo a la empresa como decidió Franklin.

## Consecuencias

Se conservan bandeja propia y lectura explícita. Esta decisión se aparta de
RF-NOT-06 y limita RF-NOT-07 al Administrador. No equivale a excepción aprobada
por el profesor ni a cumplimiento íntegro de esos criterios académicos.

## Implementación de bandeja — 2026-11-20

Notification conserva destinatario, contenido y fechas UTC. Consulta y marcado
se restringen al usuario autenticado en servidor. Consultar no marca; las
acciones individual/general conservan primera fecha de lectura. No se expone
eliminación ni creación arbitraria por API. La persistencia de esta bandeja
se complementa con productores y selección de cuentas activas mediante contratos
propios de AccessControl (ADR 030). Las resoluciones al Estándar son internas;
la política no modifica los correos de activación o recuperación.
