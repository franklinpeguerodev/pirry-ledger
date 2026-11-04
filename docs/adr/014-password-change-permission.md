# ADR 014 - Password change approval

- **Estado:** aceptada
- **Fecha:** 2026-11-04
- **Requisitos:** RF-CA-09, RF-CA-10, RF-CA-11, RF-CA-13, RF-CA-22; RF-GP-01, RF-GP-03, RF-GP-05, RF-GP-07; RD-03, RD-11; toca ADR 008 y ADR 011
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

Hoy un `Standard` con sesión cambia su contraseña con
`POST /api/auth/change-password` sin pedir permiso a nadie: verifica la contraseña
actual y la reemplaza. La recuperación (`POST /api/auth/recovery` y
`POST /api/auth/reset-password`) también es libre: cualquiera con acceso al correo
del usuario define una contraseña nueva.

Franklin decidió que **todo cambio de contraseña de un `Standard` requiere
aprobación de un Administrador**. La razón es la misma que motiva ADR 013: el
Administrador conoce la contraseña inicial de cada cuenta, y sin este control
podría cambiarla a su antojo sin que nadie se entere.

La pieza de Permissions ya tiene el mecanismo: solicitud, aprobación, concesión y
consumo de un solo uso (ADR 008, ADR 011, ADR 012). Lo que falta es usarlo para
las contraseñas, que hoy no pasan por ahí.

### El choque con RF-CA-11

RF-CA-11 dice, literal: "Con un código válido el usuario define una contraseña
nueva". Si se exige una concesión en `reset-password` sin cambiar nada más, el
usuario pide su código, lo recibe, lo introduce y recibe un `403` porque no tiene
concesión. Eso **incumple** el requisito tal como está escrito.

La aprobación tiene que existir **antes** de que el código llegue al usuario. Así,
"código válido" implica "aprobación concedida" y el criterio se cumple.

## Decisión

### 1. Dos nombres nuevos en el catálogo

| Nombre | Quién lo pide | Quién lo aprueba | Qué autoriza | Cuándo se consume |
|---|---|---|---|---|
| `ChangePassword` | El `Standard` con sesión | Un Administrador | Cambiar la contraseña propia | Al cambiarla |
| `ResetPassword` | El sistema, al recibir `recovery` | Un Administrador | Emitir el código y restablecer | Al usar el código |

Los dos entran en la lista cerrada de `PermissionCatalog` y en `PermissionNames`.
Son nombres fijos, no llevan `:<guid>`: la concesión es por usuario y el índice
único `(user_id, permission_name)` (ADR 008) ya garantiza una sola fila por par.

### 2. La aprobación se mueve antes del código

```
1. POST /api/auth/recovery { email }                  (público)
   Standard → solicitud ResetPassword Pending. No se genera código, no se envía correo.
   Admin    → código emitido y encolado (ver punto 6).
   → 202 idéntico exista o no el correo, y en los dos casos anteriores.

2. POST /api/permissions/requests/{id}/approve         (Administrator)
   → Pending → Approved → Applied + concesión ResetPassword
   → recién aquí se emite el código y se encola el correo.

3. POST /api/auth/reset-password { code, password }    (público)
   Standard → valida el código, consume ResetPassword, cambia la contraseña.
   Admin    → valida el código, cambia la contraseña.
```

`PasswordRecovery.RequestAsync` deja de generar el código y de encolar el correo.
Eso cambia un comportamiento que hoy está probado
(`SolicitudDesconocidaYConocidaRespondenIgual`): la respuesta sigue siendo
idéntica, pero el correo ya no sale en ese momento. La prueba se reescribe para
fijar el comportamiento nuevo, no para tapar el viejo.

### 3. Cambio con sesión

`POST /api/auth/change-password`, en este orden:

1. autentica la sesión;
2. valida la política de la contraseña nueva (RF-CA-14);
3. carga el usuario y **verifica la contraseña actual**;
4. si el rol es `Standard`: `ConsumeAsync(userId, ChangePassword)`;
5. si no había concesión: `RequestAsync(userId, ChangePassword)` y rechaza;
6. cambia la contraseña y guarda.

El paso 3 va antes del 4 a propósito: con la contraseña actual equivocada no se
consume nada. Si se consumiera primero, un error de tecleo del usuario gastaría
una concesión que un Administrador tuvo que aprobar.

El rechazo distingue dos casos, igual que ADR 012:

| Resultado | Código | Mensaje |
|---|---|---|
| `Created` | `403` | `Se envió una solicitud para cambiar su contraseña. Un Administrador debe aprobarla.` |
| `AlreadyPending` | `409` | `Ya hay una solicitud pendiente para cambiar su contraseña.` |

### 4. `force-reset` crea la concesión y emite el código

`POST /api/admin/users/{id}/force-reset` es el atajo del Administrador cuando el
usuario olvidó la contraseña: invalida la contraseña anterior, concede
`ResetPassword` con `IPermissionAuthority.GrantAsync` y emite el código. El
Administrador es la aprobación, así que no pasa por una solicitud.

`GrantAsync` hace upsert por `(user_id, permission_name)`: repetir `force-reset`
no acumula filas (ADR 008).

### 5. Los dos contratos nuevos viven en Core.Contracts

```csharp
// Core.Contracts/Auth/IPasswordResetIssuer.cs
public interface IPasswordResetIssuer
{
    Task IssueAsync(Guid userId, CancellationToken cancellationToken = default);
}

// Core.Contracts/Permissions/IPermissionAuthority.cs
Task GrantAsync(
    Guid userId,
    string permissionName,
    Guid grantedByUserId,
    CancellationToken cancellationToken = default);
```

`IPasswordResetIssuer` lo necesita Permissions para emitir el código al aprobar,
pero el código de recuperación es de AccessControl. Sin el contrato, Permissions
tendría que escribir en las tablas de AccessControl o AccessControl tendría que
depender de Permissions.Application; las dos cosas rompen RD-03.

`GrantAsync` lo necesita `ForcePasswordReset` para conceder sin solicitud previa.
Se añade al mismo contrato que ya usan `Authenticate` y `CancelInvoice`.

La implementación de `IPasswordResetIssuer` es `PasswordResetIssuer`, en
`AccessControl.Application`: solo usa puertos (`IUserRepository`,
`IRecoveryCodeRepository`, `IEmailQueue`) y `IClock`.

### 6. El Administrador está exento para su propia contraseña

Un `Administrator` no pide aprobación para cambiar ni para recuperar su propia
contraseña.

**Por qué no es una excepción cómoda, sino la única salida:** `ApprovePermissionRequest`
rechaza resolver tu propia solicitud (`if (request.RequesterId == caller.Id) throw
new SelfResolutionRejectedException();`, RF-GP-07). Si el Administrador tuviera que
pedir aprobación, su solicitud solo podría aprobarla **otro** Administrador. Con un
solo Administrador —el caso normal en un negocio de dos o tres empleados— no habría
nadie, y el sistema quedaría sin administración para siempre: `force-reset` exige
una sesión de Administrador, que es justo lo que se perdió, y el seed (ADR 002) se
niega a crear un segundo.

La regla se aplica donde importa: el `Standard` no tiene ninguna excepción, ni con
sesión ni por recuperación.

Consecuencia asumida: quien controle el buzón del Administrador puede tomar su
cuenta. Ya ocurre hoy, porque la recuperación es pública y no mira el rol. Lo que
cambia este ADR es que el `Standard` deja de tener esa puerta.

### 7. El código de recuperación dura cuatro horas

`RecoveryCodeDuration` pasa de 15 minutos a 4 horas. Con el flujo nuevo el reloj
empieza a contar cuando el Administrador aprueba, no cuando el usuario pide la
recuperación, y el usuario puede no estar mirando el correo en ese momento. Cuatro
horas cubren una jornada; el código sigue siendo de un solo uso y se guarda solo
como SHA-256.

### 8. El orden consumo → cambio → guardado

Si el guardado falla después de consumir la concesión, la concesión se pierde y el
usuario tiene que pedir otra. Es el mismo compromiso que ADR 011 aceptó para la
anulación de facturas: consumir antes de escribir evita que una concesión se use
dos veces, y el precio es que un fallo de escritura obliga a repetir el trámite.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Exigir la concesión solo en `reset-password`, sin mover el código | Rompe RF-CA-11: el usuario tendría un código válido y no podría usarlo. |
| Exigir aprobación también al Administrador | Con un solo Administrador, RF-GP-07 hace imposible aprobar la solicitud y el sistema queda encerrado. |
| Que el Administrador apruebe su propia solicitud | Viola RF-GP-07, que es explícito: nadie resuelve su propia solicitud. |
| Un tercer rol "superadministrador" | Cambia RF-CA-04 (dos roles) y añade una jerarquía que ningún requisito pide. |
| Aprobar automáticamente toda solicitud de contraseña | La aprobación dejaría de existir y con ella el control que Franklin pidió. |
| Guardar la concesión con un nombre `ChangePassword:<guid>` | La concesión es por usuario y permiso; el índice único ya la hace única. El `guid` solo añadiría ruido. |
| Consumir antes de verificar la contraseña actual | Un error de tecleo gastaría una concesión aprobada por el Administrador. |
| Mantener 15 minutos | Con la aprobación en medio, el usuario puede recibir el código vencido. |

## Consecuencias

**A favor**

- Ningún cambio de contraseña de un `Standard` ocurre sin que un Administrador lo
  haya aprobado.
- RF-CA-09, RF-CA-10 y RF-CA-11 se siguen cumpliendo: el código válido ya implica
  aprobación.
- El Administrador no puede quedarse fuera de su propia cuenta.
- Las concesiones de contraseña se consumen una sola vez, con el mismo mecanismo
  que ya usa la anulación de facturas.
- Sin migración: los nombres nuevos caben en `permission_name varchar(64)`.

**En contra, asumido a conciencia**

- Un `Standard` no recupera su cuenta si no hay un Administrador disponible. Es
  exactamente lo que se pidió.
- La aprobación y el envío del código viven en piezas distintas: si el envío falla
  después de crear la concesión, el Administrador reintenta con `force-reset`.
- `recovery` deja de enviar correo, así que el usuario no recibe nada hasta que
  alguien aprueba. El mensaje de la API no lo dice, para no revelar si el correo
  existe; el README sí lo explica.
- Quien controle el buzón del Administrador sigue pudiendo tomar su cuenta.
- El flujo tiene un paso humano más, y con él una espera.

## Qué queda fuera

- El aviso al Administrador cuando llega una solicitud nueva (RF-NEG-07, semana 10).
- La auditoría de quién aprobó qué y cuándo (semana 14).
- El segundo factor para el Administrador.
- Un límite de intentos sobre `reset-password`: el código es largo, de un solo uso
  y se guarda hasheado, así que adivinarlo no es viable.
