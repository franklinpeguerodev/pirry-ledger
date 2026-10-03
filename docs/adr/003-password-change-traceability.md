# ADR 003 - Password change traceability with a nullable UTC timestamp

- **Estado:** aceptada
- **Fecha:** 2026-10-02
- **Requisitos:** RF-CA-12, RF-CA-09, RF-CA-13; RD-09, RD-10, RD-11
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

La Práctica 1 invalida las sesiones existentes cuando una contraseña se
reemplaza. Esa invalidación protege la cuenta, pero no deja un dato sencillo
para responder una pregunta operativa importante: **¿cuándo se cambió por última
vez la contraseña de este usuario?**

Los tres flujos que pueden reemplazarla son distintos:

- el usuario completa una recuperación con un código;
- el usuario autenticado cambia su propia contraseña;
- un Administrador fuerza el restablecimiento de otro usuario.

Si cada flujo registrara la fecha por separado, con el tiempo podrían divergir:
uno podría olvidar el campo, usar la hora local o actualizar la fecha antes de
una validación que después falla. La regla debe vivir en la misma operación de
dominio que ya concentra el cambio de la credencial.

La auditoría completa de RF-CA-08, RF-CA-13 y RF-CA-20 está fuera del alcance de
esta práctica y se reserva para la semana 14. Por tanto, esta decisión no debe
convertirse accidentalmente en un historial de eventos ni en una auditoría de
actores.

## Decisión

`Usuario` conserva una propiedad nullable `ContrasenaCambiadaUtc`:

1. `null` significa que la cuenta todavía conserva la contraseña inicial
   creada durante el registro o el seed del primer Administrador.
2. Cuando `CambiarContrasena` completa el reemplazo de una contraseña, asigna el
   instante UTC recibido del `IClock` inyectado y después incrementa
   `CredencialVersion`.
3. Todos los flujos de reemplazo llaman a esa operación compartida:
   recuperación por código, cambio autenticado y restablecimiento forzado.
4. EF Core persiste el campo como `timestamp with time zone` nullable mediante
   una migración aditiva. La migración no elimina ni reescribe las contraseñas
   existentes.
5. El campo sirve para consultar el último reemplazo, pero no identifica quién
   lo hizo, por qué flujo ocurrió, qué valor tenía la contraseña anterior ni
   cuántos cambios hubo.

El valor nunca contiene una contraseña, un hash, un token ni un secreto. La
consulta queda restringida a la persistencia interna; no se añade un endpoint
que exponga credenciales o detalles sensibles.

## Por qué se registra desde `CambiarContrasena`

La operación de dominio es el punto común de los tres casos de uso. Registrar
la fecha allí proporciona una única garantía:

- si el cambio no termina, no se registra una sustitución exitosa;
- recuperación, cambio propio y restablecimiento forzado mantienen la misma
  semántica;
- `IClock` permite usar UTC en producción y un reloj controlado en pruebas;
- el incremento de `CredencialVersion` y la fecha quedan ligados al mismo
  cambio de credencial.

El endpoint no es el lugar correcto para esta regla: traducir HTTP no debe
decidir cuándo una contraseña cambió realmente.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| No guardar ninguna fecha | La invalidación seguiría funcionando, pero no habría trazabilidad mínima para diagnosticar cuándo se reemplazó una credencial. |
| Guardar la fecha en cada caso de uso | Duplica una regla crítica y permite que los tres flujos terminen con comportamientos distintos. |
| Crear una fila por cada cambio | Sería un historial de auditoría. Requiere actor, motivo, evento, retención y consultas adicionales que pertenecen a la semana 14. |
| Crear una tabla de auditoría completa ahora | Adelanta alcance fuera de la práctica y mezcla trazabilidad mínima con RF-CA-08, RF-CA-13 y RF-CA-20. |
| Usar la hora local del servidor | Rompe RD-11 y vuelve ambiguas las consultas entre máquinas o zonas horarias. |
| Hacer el campo obligatorio | Las cuentas existentes no tienen un instante conocido de cambio inicial; `NULL` representa honestamente esa ausencia. |

## Persistencia y compatibilidad

La migración añade una columna nullable a `ac_usuarios`. Las filas existentes
permanecen válidas porque reciben `NULL`, y las cuentas nuevas también empiezan
con `NULL` hasta que su contraseña sea reemplazada.

El tipo PostgreSQL es `timestamp with time zone`, compatible con el valor UTC
producido por el reloj del dominio. La configuración EF Core declara la
propiedad para que futuras migraciones mantengan el modelo y la base alineados.

## Verificación

La decisión se verifica con:

- la prueba de dominio que comprueba que la fecha coincide con el instante UTC
  entregado por el reloj inyectado;
- el build Release de la solución;
- las pruebas de AccessControl;
- la aplicación de la migración;
- la inspección del esquema, confirmando `nullable = YES` y
  `timestamp with time zone`.

## Consecuencias

**A favor**

- Se puede conocer el último reemplazo sin guardar material secreto.
- Los tres flujos usan una única regla de dominio.
- Las cuentas antiguas no reciben una fecha inventada.
- La solución sigue siendo pequeña y compatible con la auditoría futura.
- El timestamp UTC es determinista y testeable.

**En contra, asumido a conciencia**

- Solo existe el último instante; se pierde la historia de cambios anteriores.
- No se sabe quién inició el cambio ni si fue recuperación, cambio propio o
  restablecimiento forzado.
- `NULL` requiere que las consultas distingan entre una cuenta sin reemplazos y
  una cuenta cuyo dato aún no existía antes de esta migración.
- Para investigar una secuencia completa será necesario implementar la auditoría
  de la semana 14.

## Qué queda fuera

Esta ADR no crea registros de auditoría, historial de eventos, actor del cambio,
motivo, IP, dispositivo, valores anteriores, notificaciones ni un endpoint
administrativo para consultar credenciales. Tampoco cambia la duración de las
sesiones, la política de contraseñas, los códigos de recuperación ni la
invalidación por `CredencialVersion`.
