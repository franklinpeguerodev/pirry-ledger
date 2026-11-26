# ADR 024 - Selective administrator email notifications

- **Estado:** aceptada por Franklin; política y preferencias implementadas, avisos temporales CLI pendientes.
- **Fecha:** 2026-11-19
- **Requisitos:** RF-NOT-01, RF-NOT-03, RF-NOT-06, RF-NOT-07
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

Franklin reserva correo para atención administrativa prioritaria, evitando correo
por operaciones cotidianas. RF-NOT-06 exige ambos canales para toda notificación.

## Decisión

Administradores activos reciben correo por solicitud de permiso creada,
transferencia pendiente más de cuatro días y jornada vencida sin cerrar.
Demás eventos aprobados mantienen solo aviso interno según matriz operativa.
Correo habilitado inicialmente, con preferencia individual. Desactivar cancela
pendientes notificables; reactivar solo afecta nuevos eventos. Bandeja permanece.
Revalidar antes de enviar; no se retira correo ya entregado a SMTP.
Activación y recuperación se conservan independientemente de esa preferencia.

## Alternativas consideradas

- Correo universal: cumple el enunciado, aumenta volumen.
- Preferencias por evento: más configuración que la solicitada.
- Selección fija: corresponde a prioridades del negocio, con desviación explícita.

## Consecuencias

Avisos temporales son únicos por elemento y detectados por CLI tras su límite.
Abono registrado por Administrador y resuelto por otro solo avisa internamente.
Esta selección se aparta de RF-NOT-06 también para Administradores. El ADR no
constituye excepción académica concedida ni evidencia de cumplimiento universal.
