# ADR 001 - Session credential: opaque token with a `Sesion` table

- **Estado:** aceptada
- **Fecha:** 2026-09-30
- **Requisitos:** RF-CA-03, RF-CA-07, RF-CA-18, RF-CA-19; RD-06, RD-08, RD-09, RD-11
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

`current-iteration.md` pone una condicion dura: la credencial de sesion debe poder
invalidarse **en el servidor**. Cerrar sesion (RF-CA-18), cambiar la contrasena
(RF-CA-12), restablecerla (RF-CA-09) o desactivar al usuario (RF-CA-20) tienen que
anular las credenciales ya emitidas. Una credencial autocontenida que se valida
solo, sin consultar nada en el servidor, no cumple.

Piramid ERP v3, un solo despliegue, sin microservicios. La pregunta es como se
valida cada peticion sin repetir logica por todos lados.

## Decision

Credencial de sesion **opaca**, guardada en la base:

1. Al iniciar sesion se generan **32 bytes aleatorios** de `RandomNumberGenerator`,
   se codifican en **base64url** y se devuelven al cliente en
   `Authorization: Bearer <token>`.
2. En la base **solo se guarda el SHA-256** del token. El valor en claro no se
   persiste: leer la tabla no permite suplantar a nadie.
3. Tabla `Sesion` con: hash del token, `UsuarioId`, `EmitidaUtc`, `ExpiraUtc`,
   `CerradaUtc` y `CredencialVersion`, el valor que tenia el usuario al emitirla.
4. `Usuario` lleva `CredencialVersion`. Se incrementa al cambiar la contrasena, al
   restablecerla y al desactivar la cuenta.
5. Una sesion es valida **solo si** se cumplen las cinco condiciones:
   - la fila existe;
   - `CerradaUtc` es nulo;
   - `ExpiraUtc` es posterior al momento actual;
   - la `CredencialVersion` de la sesion coincide con la actual del usuario;
   - el usuario esta activo.
6. **Un solo punto de validacion.** Una unica pieza resuelve las cinco condiciones
   y distingue `401` (no hay sesion valida) de `403` (sesion valida, rol
   insuficiente, RF-CA-06). Nada de esto se repite en los endpoints.
7. El reloj siempre es el `IClock` inyectado (RD-11), tambien para vencimientos y
   para el bloqueo, para que las pruebas no esperen 15 minutos.

Sin paquetes nuevos: `RandomNumberGenerator`, `SHA256` y `Convert.ToBase64String`
vienen en la biblioteca estandar. `WebEncoders` de `Microsoft.AspNetCore` no hizo
falta.

### Vencimiento absoluto, no deslizante

**Decision: absoluto.** La sesion vence 8 horas despues de emitirse y no se renueva
aunque el usuario este activo.

El deslizante (renovar el vencimiento en cada uso) es comodo y se ve en muchos
sistemas, pero aqui se descarto por dos razones concretas:

- **Sesiones eternas.** Con caducidad deslizante, un token robado que se use cada
  30 minutos nunca caduca y sigue valiendo mientras haya trafico. El robo se
  vuelve permanente. Con caducidad absoluta, el dano esta acotado a 8 horas.
- **Costo de auditar cuando se robo.** Con caducidad absoluta, la hora de emision
  es un dato fiable: la credencial es valida entre `EmitidaUtc` y `ExpiraUtc`. Si
  fuera deslizante, `EmitidaUtc` dejaria de ser la referencia y habria que
  reconstruir la historia de uso fila por fila.

El precio es que el usuario tiene que volver a entrar cada jornada de 8 horas. Con
2 o 3 empleados por sucursal y un turno de trabajo mucho mas corto, es aceptable,
y un `401` seguido de volver a entrar es preferible a una credencial que nunca
caduca.

### Por que no JWT con lista de revocacion

JWT se descarto, y la razon es la que hace innecesaria la discusion:

**Una lista de revocacion server-side es una tabla de sesiones con mas piezas.**

Para que JWT sirva en este sistema, hay que poder anularlo: cambiar la contrasena,
desactivar el usuario o cerrar sesion tienen que matar tokens **ya emitidos**. La
unica forma es que el servidor consulte algo en cada peticion. Ese "algo" es una
tabla de revocaciones con identificador del token, fecha y motivo. Mirandola de
cerca es una tabla de sesiones: fila por credencial, con su fecha y su estado.

La diferencia real entre las dos opciones:

| | Token opaco + tabla `Sesion` | JWT + lista de revocacion |
|---|---|---|
| Donde vive la credencial | Fila en la base | Fila en la lista de revocacion |
| Consultar la base por peticion | Necesario, obligatorio | Necesario, por la revocacion |
| Quien decide si la fila aplica | El estado de la fila | Una clave mas una entrada en la lista |

La segunda fila de la tabla es la misma. El JWT **no evita** la consulta a la
base; solo cambia quien guarda la informacion. Se paga ademas el coste de
verificar la firma y de emitir un token con los campos `exp`, `iat` y `jti`, mas
una clave secreta que hay que proteger y rotar. Ninguna de esas piezas existe en
la decision tomada.

Ademas, JWT resuelve **autenticacion sin estado**, que es un problema que este
sistema no tiene: hay una sola base de datos y una sola app que valida. El
beneficio del JWT, que es escalar validando sin tocar la base, no se usa.

**El argumento decisivo:** no hay varios servicios validando el mismo token. Es un
monolito modular con un Host. La caracteristica sin estado del JWT no aporta nada
aqui, y su unico motivo de ser, validar sin consultar estado, es justo lo que el
requisito prohibe.

## Consecuencias

**A favor**
- Cerrar sesion, cambiar o restablecer la contrasena y desactivar anulan todo lo
  emitido, con una sola fila por sesion y sin logica repetida.
- `CredencialVersion` invalida **todas** las sesiones de golpe con un entero, sin
  recorrer la tabla. Es lo que hace que desactivar a un usuario con una sesion
  abierta sea inmediato.
- El token no viaja interpretable. Robar la base de datos no sirve para
  autenticarse.
- Revocar una sesion concreta es borrar su fila, y el estado se ve en la propia
  fila.

**En contra, asumido a conciencia**
- Cada peticion autenticada lee la base. Con 2 o 3 usuarios por sucursal es
  irrelevante; con decenas de miles seria el punto a cambiar, y el dia que pase se
  cambiaria por una cache de sesiones, no por JWT.
- Hay que limpiar las sesiones vencidas, ver la seccion siguiente.
- Un `SELECT` por peticion acopla la disponibilidad a la base. Aceptado: sin base
  no hay sesion, y la app tampoco puede facturar.

## Limpieza de sesiones vencidas

**Propuesta, fuera de este PR:** las filas vencidas y cerradas se conservan. Una
consulta de limpieza es trivial y un `DELETE` masivo en un sistema con pocos
usuarios no aporta nada medible.

Cuando haga falta, y si el profesor lo pide, hay dos vias y la segunda es la
recomendada:

1. Al arrancar, borrar lo que ya vencio. Simple, pero en un despliegue con mas de
   una instancia cada una borraria lo mismo.
2. Un proceso de mantenimiento aparte, como el que ya existe para el envio de
   correo, que corre cada cierto tiempo y limpia por fecha.

**Lo que si se decide ahora:** la tabla lleva indice sobre `ExpiraUtc` para que la
consulta de limpieza, cuando exista, no sea un recorrido completo. Sin ese indice,
la limpieza seria un `WHERE` sobre la tabla entera.

## Que se descarta de esta ADR

Nada. La decision es firme: token opaco, tabla `Sesion`, `CredencialVersion`.
Este documento existe para que Franklin pueda defender **por que no JWT**, que es
la pregunta que seguro aparece en la revision.