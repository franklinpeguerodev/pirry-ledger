# ADR 029 - Explicit cross-module transactions

- **Estado:** aceptada; implementación en curso
- **Fecha:** 2026-11-20
- **Requisitos:** RD-01, RD-03, RD-09, RF-NOT-01, RF-NOT-02, RF-NOT-03, RF-NOT-08
- **Decide:** Franklin. **Redacta:** el agente, con autorización expresa.

## Contexto

Operación, avisos internos y correos notificables deben confirmarse juntos.
Los módulos tienen contextos separados y algunos ya abren transacciones propias.
Una escritura fallida no debe dejar operación sin aviso ni aviso sin operación.
SMTP se ejecuta después mediante el procesador independiente existente.

## Decisión

Franklin aprobó la alternativa 1: un coordinador explícito abre una conexión
y una transacción local PostgreSQL para los módulos participantes. El contrato
IApplicationTransaction vive en Core.Contracts; cada módulo incorpora su propio
contexto mediante IModuleTransactionParticipant. Ningún participante ve tablas
o entidades ajenas. La implementación física se ubica en la infraestructura
de Notifications, sin añadir proyecto Shared ni dependencia Core -> Business.

Los participantes se incorporan secuencialmente a la misma transacción.
Una llamada anidada participa en la transacción exterior; un fallo anidado la
marca para reversión aunque el llamador capture la excepción. Solo la llamada
exterior confirma. La liberación restaura los contextos y descarta seguimiento
tras reversión para no conservar cambios que PostgreSQL no confirmó.

Los bloqueos de sucursal y factura permanecen dentro de esa transacción;
los flujos adaptados no abren una segunda transacción incompatible. Se verifica
la reversión real entre módulos antes de declarar la integración terminada.

## Alternativas consideradas

- Coordinador explícito: elegido. Participación visible y una transacción local;
  exige adaptar contextos y transacciones existentes.
- Extender TransactionScope: aprovecha su uso actual en anulaciones, pero requiere
  verificar incorporación de conexiones y convivencia con transacciones explícitas.
  Dependiendo de conexiones y entorno puede requerir coordinación distribuida.
- Guardar avisos después de confirmar: no garantiza la atomicidad aprobada.

## Consecuencias

Se requieren pruebas de éxito, fallo al guardar avisos, fallo de operación,
anidamiento, limpieza y reutilización del ámbito. No se instalan paquetes nuevos.
Activación y recuperación conservan sus reglas de correo. Este ADR no implementa
SMTP dentro de operaciones ni añade un bus distribuido o módulo de auditoría.
