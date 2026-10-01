# ADR 002 - Primer Administrador: semilla desde variables de entorno

- **Estado:** aceptada
- **Fecha:** 2026-10-01
- **Requisitos:** RF-CA-04, RF-CA-08, RF-CA-20, RF-CA-21, RF-CA-14; RD-02, RD-09, RD-10
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

El registro publico siempre crea un `Rol.Estandar`. No es una decision que se
pueda cambiar sin romper el resto: `Usuario.Crear` fija el rol como constante
dentro de la fabrica y el comentario de esa linea explica por que.

```
Rol.Estandar,
activo: false,
```

Un usuario recien registrado no puede gobernarse a si mismo, y RF-CA-15 exige que
nazca inactivo. Las dos cosas juntas producen un bloqueo:

- RF-CA-08, cambiar el rol, lo hace un Administrador.
- RF-CA-20, desactivar a un usuario, lo hace un Administrador.
- RF-CA-21, listar usuarios, lo hace un Administrador.

Sin ningun Administrador, **los tres criterios no se pueden ni demostrar**. Y no
basta con decir "hay que crear uno a mano": una intervencion manual sobre la base
de datos es justamente lo que RD-09 pide evitar, porque el estado tiene que
sobrevivir al proceso, no depender de que alguien se acuerde.

`FechaDeCreacionUtc` en la entidad ya estaba commentada como "para el seed
idempotente del primer Administrador", pero ese seed nunca se escribio. Esta ADR
cierra esa mitad pendiente.

## Decision

El primer Administrador lo crea una **semilla idempotente** que corre en el
arranque del Host, y sus credenciales vienen de variables de entorno (RD-10).

1. **Tres variables**, todas opcionales:
   - `PIRRY_LEDGER_FIRST_ADMIN_EMAIL`
   - `PIRRY_LEDGER_FIRST_ADMIN_PASSWORD`
   - `PIRRY_LEDGER_FIRST_ADMIN_NAME`, con `"Administrador"` por defecto

   Si faltan el correo o la contrasena, la semilla no corre y la aplicacion
   arranca igual. Exigirlas habria estorbedo a quien solo quiere registrarse.

2. **Idempotencia por pregunta, no por conteo.** Si ya existe **cualquier**
   Administrador, la semilla no hace nada y devuelve `false`. No compara correos,
   aunque se le pase otro. Dos arranques no producen dos Administradores, que es
   el mismo criterio que RF-NOT-12 exige al enviador de correo.

3. **Nace activo.** Al contrario que el registro. Este usuario no llega por
   correo, asi que no hay enlace de activacion que nadie pueda abrir, y activar
   la cuenta es una operacion de Administrador: si naciera inactivo no habria
   forma de activarlo, porque no habria ningun Administrador que lo hiciera. El
   circulo se cierra solo de esta forma.

4. **No promueve cuentas existentes.** Si el correo del seed ya existe como
   `Estandar`, la semilla falla con un mensaje controlado y **no toca la cuenta**.
   Promover a alguien por el camino del seed seria el escalamiento de privilegios
   que RF-CA-08 existe para impedir, y lo haria sin que nadie lo pidiera
   explicitamente.

5. **La politica de contrasena se aplica igual** (RF-CA-14). Se reutiliza
   `PasswordPolicy`, sin duplicar reglas: una contrasena debil en un
   Administrador es peor que en un Estándar.

6. **Segunda puerta de entrada en la entidad**, `Usuario.CrearComoAdministrador`,
   que fija `Rol.Administrador` y `activo: true`. `Usuario.Crear` no cambia: sigue
   dando `Estandar` e inactivo.

## Por que una segunda fabrica y no un parametro

La entidad tenia una regla escrita: *"Una sola puerta de entrada: nada crea un
usuario escribiendolo a mano"*. Anyadir el rol como parametro opcional de `Crear`
la habria eliminado de una linea, pero habria roto el motivo de la regla:
`RegisterUser` dejaria de poder fijar el rol por si mismo, y pasaria a depender de
que el llamador recuerde no pasar el parametro.

Tres alternativas, y por que se descartaron:

| Opcion | Que era | Por que no |
|---|---|---|
| `Crear(..., Rol rol = Rol.Estandar)` | Un parametro opcional | El registro publico deja de tener el rol garantizado por la entidad. El default protege solo mientras nadie lo pase. |
| `Crear` + `CambiarRol` + `Activar` | Reutilizar lo que ya existe | El Administrador nace con `CredencialVersion` ya alterada, que no es su estado real, y depende de tres llamadas en orden. El resultado no dice "nacio como Administrador". |
| SQL a mano en el despliegue | Un `INSERT` documentado | Invisible para el codigo, no se puede probar, y es estado fuera del proceso (RD-09). |

Se eligio una fabrica aparte porque deja cada puerta con sus garantias escritas en
un solo sitio: la puerta publica promete `Estandar` e inactivo, y la del seed
promete `Administrador` y activo. Que haya dos metodos es visible; que el rol
dependa de un default es invisible.

## Consecuencias

**A favor**
- RF-CA-08, 20 y 21 se pueden ejercitar en cuanto exista la parte de
  administracion de usuarios, que es el siguiente PR.
- El estado sigue siendo reproducible: clonar el repositorio, definir las tres
  variables y arrancar deja un Administrador. No hay pasos manuales.
- La contrasena nunca se guarda en texto plano ni en el repositorio: pasa por el
  mismo `Argon2idPasswordHasher` con sal por usuario que el registro (RF-CA-02).
- Los mensajes de error no llevan el correo ni la contrasena (RD-08), porque el
  Host los imprime en consola al arrancar.

**En contra, asumido a conciencia**
- **La contrasena vive en el entorno del proceso.** Quien pueda leer las variables
  del sistema puede leerla. Es el mismo riesgo que ya tienen las credenciales de
  SMTP y la cadena de conexion, y es la unica alternativa a escribirla en un
  archivo.
- **Quien tenga las variables puede crear un Administrador** en cualquier base a
  la que apunte. Es deliberado: quien despliega ya puede ejecutar migraciones.
- **No hay recuperacion si se pierde la contrasena.** Es la situacion que
  RF-CA-09 va a resolver con el siguiente PR. Hoy, sin recuperacion construida,
  perderla significa intervention manual.
- **No hay aviso si no se define.** La aplicacion arranca sin Administrador y
  sin decir nada. Se eligio el silencio por el ruido: un mensaje en cada arranque
  de desarrollo-traina de leer la consola. El README dice que hay que definirlas.

## Que se descarta de esta ADR

La creacion de Administradores por HTTP, sea endpoint propio o una bandera en el
registro. Se descarta por una razon concreta: haria que *cualquiera* que se
registre pueda convertirse en Administrador, y RF-CA-04 no lo pide. Si mas adelante
hace falta una recuperacion de Administrador, se decide en su momento.