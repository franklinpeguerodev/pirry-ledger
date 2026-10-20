# ADR 009 - Role changes invalidate sessions via CredentialVersion

- **Estado:** aceptada
- **Fecha:** 2026-10-20
- **Requisitos:** RF-CA-08 (cambiar rol); RF-GP-07 (camino realista); indirectamente, RF-CA-12 (mismo mecanismo para cambio de contraseña) y RF-CA-20 (mismo mecanismo para desactivar)
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

La credencial de sesión (ADR 001) usa `CredentialVersion`, un entero en `ac_users`, para invalidar todas las sesiones de un usuario con un solo incremento. Hoy, las operaciones que lo incrementan son tres:

- `User.ChangePassword(newHash, utcNow)` (RF-CA-11, RF-CA-12).
- `User.Deactivate()` (RF-CA-20, decisión propia: subir la versión al desactivar).
- (implícitamente, `User.CreateAsAdministrator` la pone en `0` por construcción.)

`User.ChangeRole(newRole)` **no** la incrementa. Esto significa que un usuario al que se le cambia el rol —en particular, un Estándar ascendido a Administrador— conserva su sesión abierta con los permisos del rol anterior. El nuevo rol no se refleja en sus peticiones hasta que el token vence (8 horas, ADR 001) o el usuario cierra sesión y vuelve a entrar.

Hay tres consecuencias concretas que la pieza de Gestión de permisos (RF-GP-07, Asignación 2) hace visibles:

1. **El camino realista de RF-GP-07 deja de ser realista.** El criterio exige que un Estándar cree una solicitud, sea ascendido mientras está Pendiente, e intente aprobarla. Si la sesión sigue activa con el rol viejo (Estándar), el filtro de acceso le niega la ruta antes de llegar al caso de uso: nunca llega al rechazo de "no resuelvas tu propia solicitud", que es lo que la regla realmente quiere comprobar. La prueba se vuelve un 403 del filtro, no un 400 de la regla de negocio.
2. **Una promoción no se nota hasta el siguiente login.** Un Administrador que acaba de ser ascendido sigue viendo la API con permisos de Estándar durante el resto de su jornada. Esto no es un problema teórico: con dos o tres empleados por sucursal, una promoción puede ocurrir a media mañana y el promovido pasa horas pensando que aún es Estándar.
3. **La regla general "un cambio sensible invalida la sesión" no es uniforme.** Hoy la cumplen cambio de contraseña y desactivación; no la cumple cambio de rol. La asimetría es exactamente el tipo de hueco que RF-CA-08 y RF-GP-07 quieren evitar.

El mecanismo ya existe: `CredentialVersion` está en la entidad, está mapeada en la base y la lógica de validación de sesión (`Session.IsValid`) ya la compara con la del usuario. Lo que falta es que `ChangeRole` la incremente.

## Decisión

`User.ChangeRole(newRole, utcNow)` se modifica para sumar `+1` a `CredentialVersion` además de asignar el rol nuevo. La operación que llama (`ChangeUserRole` en la capa de Application) sigue siendo la misma; lo único que cambia es que ahora recibe el `IClock` y le pasa `utcNow` a la entidad. La firma pública de la entidad pasa de:

```csharp
public void ChangeRole(Role newRole)
```

a:

```csharp
public void ChangeRole(Role newRole, DateTime utcNow)
```

`UserConfiguration` no cambia (la columna `credential_version` ya existe y está mapeada, ADR 003 + 004). La migración tampoco.

### Por qué `CredentialVersion` y no un campo nuevo

`CredentialVersion` es, desde ADR 001, **el mecanismo único** para invalidar sesiones. Añadir un segundo contador (un `RoleVersion` paralelo) tendría dos consecuencias:

- Doble lectura en `Session.IsValid`: hoy compara `CredentialVersion` de la sesión contra `CredentialVersion` del usuario. Con un campo nuevo, compararía dos números y la lógica se duplica.
- Riesgo de divergencia: una promoción que incrementara solo `RoleVersion` dejaría la sesión vigente con el rol viejo; una desactivación que solo incrementara `CredentialVersion` no protegería contra el mismo problema. El doble campo invita a olvidos.

### Por qué re-login y no un "refresh" de la sesión

La sesión no almacena el rol; almacena `CredentialVersion`. Si quisiéramos que la misma sesión siga valiendo con el rol nuevo, tendríamos que cambiar `Session.IsValid` para que la comparación del rol sea siempre fresca y no requiera re-emisión del token. Esto es lo que pasa hoy: el rol se relee en cada petición desde `IUserRepository.FindByIdAsync`. La "frescura" del rol ya está. Lo que falta es **invalidar la sesión al cambiar el rol**, no "refrescar" la sesión.

Re-login es la opción correcta: la sesión se considera un artefacto ligado al estado del usuario al emitirla. Si el estado cambia sensiblemente, el artefacto se invalida. Es exactamente lo que ya hacen `ChangePassword` y `Deactivate`.

### Compatibilidad con la firma de `ChangeRole` y los tests existentes

Cambiar la firma de `User.ChangeRole` de `(Role)` a `(Role, DateTime)` es una modificación breaking para:

- `ChangeUserRole` (Application): debe pasar `utcNow` además de `newRole`.
- Los tests que llaman directamente a `User.ChangeRole` en `Tests/PirryLedger.Core.AccessControl.Tests`: deben pasar el `DateTime` (pueden usar `DateTime.UtcNow` o un `FakeClock` ya disponible).
- Los tests que verifican que un cambio de rol no invalida sesiones (si existen): pasan a verificar lo contrario, que es la nueva propiedad.

Esta rotura es local al módulo de AccessControl y no afecta a otros consumidores: ningún otro módulo de Core ni Business llama a `User.ChangeRole` directamente (la entidad es interna al módulo; el contrato hacia fuera es `IUserRepository`).

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| No incrementar `CredentialVersion` (estado actual) | La sesión sigue activa con el rol viejo. El camino realista de RF-GP-07 falla con 403 antes de probar la regla de negocio. La promoción tarda hasta 8 horas en notarse, lo que es operacionalmente incorrecto. Es la situación que esta ADR viene a corregir. |
| Incrementar un campo nuevo `RoleVersion` | Doble lectura en `Session.IsValid`, riesgo de divergencia con `CredentialVersion` (olvidar uno u otro). Mecanismo paralelo cuando el mecanismo único ya existe. |
| Hacer que `Authenticate` re-lea el rol cada vez sin incrementar versión | El rol ya se re-lee cada vez (`IUserRepository.FindByIdAsync` resuelve al usuario actual en cada `Authenticate.ExecuteAsync`); lo que se mantiene stale es la sesión, no el rol. Re-leer no invalida la sesión, que es exactamente el problema. Esta alternativa no aborda el cambio. |
| Forzar logout programático del usuario al promover | Hoy no hay un canal para invalidar una sesión específica sin un cambio de versión. `CredentialVersion` es el camino; `Logout` cierra una sesión a la vez, no todas. Se podría implementar, pero duplica el mecanismo sin ventaja. |
| Incrementar solo cuando la promoción es a `Administrator` (no cuando es a `Standard`) | La regla "un cambio sensible invalida la sesión" debe ser uniforme. Si solo se invalida al subir, un cambio Estándar → Estándar también es un cambio de autorización (aunque no cambie nada en este caso, no se puede generalizar). Mantener la regla simple — "todo cambio de rol invalida" — es más legible y testeable. |

## Persistencia y compatibilidad

- `User.ChangeRole` cambia de firma en la entidad. `ChangeUserRole` (Application) se actualiza para pasar `IClock.UtcNow`.
- `UserConfiguration` no cambia. La columna `credential_version` ya existe (migración inicial `20261018191406_InitialSchema`) y ya está mapeada con `HasColumnName("credential_version")` (ADR 004).
- No se añade migración nueva. El campo `credential_version` ya soporta el incremento.
- Los tests existentes de `PruebasDeAdministracionDeUsuarios` que llaman a `User.ChangeRole(Role)` se actualizan para pasar `DateTime utcNow`. Los dobles (`FakeUserRepository.Create`) siguen creando el usuario por su fábrica pública; ningún test del repositorio se ve afectado.
- El conteo antes y después del cambio: el número de filas y de columnas en `ac_users` no cambia. No hay migración. La verificación es por runtime, no por esquema.

## Verificación

- `dotnet build pirry-ledger.slnx` con 0 errores.
- `dotnet test pirry-ledger.slnx`: la suite completa en verde, incluyendo los tests de AccessControl que ahora pasan `utcNow` a `ChangeRole`.
- **Nueva prueba en `PruebasDeAdministracionDeUsuarios`**: tras `ChangeRole`, `CredentialVersion` se incrementa exactamente en `+1`.
- **Nueva prueba en `PruebasDeInvalidacionDeCredencial` (o equivalente)**: una sesión emitida antes del cambio de rol queda invalidada tras el cambio. El token antiguo devuelve 401 en `Authenticate.ExecuteAsync`.
- **Prueba de integración (Host levantado)**: un Estándar inicia sesión, un Administrador lo promueve, la misma sesión devuelve 401 en `/me` y el usuario tiene que re-login para que `/me` devuelva `role: Administrator`. Esta es la verificación que el enunciado de Asignación 2 pide para RF-GP-07 en su camino realista.

## Consecuencias

**A favor**

- El camino realista de RF-GP-07 funciona: la sesión del promovido se invalida, el usuario re-entra, y al intentar aprobar su propia solicitud recibe el rechazo propio de la regla, no un 403 del filtro.
- Una promoción se nota en el siguiente login (≤ unos segundos de operacionalidad perdida), no en el siguiente vencimiento de sesión (≤ 8 horas).
- Regla uniforme: todo cambio sensible — contraseña, desactivación, rol — invalida la sesión vía `CredentialVersion`. RF-CA-08 hereda el mismo principio que RF-CA-12 y RF-CA-20.
- Sin migración: el campo ya está y la columna ya está. Es un cambio de una línea de lógica y una de firma.

**En contra, asumido a conciencia**

- Un usuario promovido en medio de su jornada tiene que volver a escribir su contraseña. Es un coste operacional pequeño y conocido (las contraseñas son lo que el usuario ya tiene); es el precio de que la promoción sea efectiva inmediatamente.
- La firma `ChangeRole(Role)` cambia a `ChangeRole(Role, DateTime)`. Toca tests existentes (los que llamaban directamente a la entidad). Es una rotura contenida al módulo AccessControl.

## Qué queda fuera

- Notificación al usuario promovido de que tiene que re-login (semana 11).
- Revocación de permisos en `per_permission_grants` (ADR 008, pieza futura).
- Otros mecanismos de invalidación (expirar sesiones individuales por `SessionId`). Hoy no son necesarios; si en el futuro aparece un caso así, se documenta en su momento.
- Invalidación selectiva por nivel de cambio (solo promociones, no degradaciones). Hoy la regla es uniforme para todos los cambios de rol. Una excepción selectiva invita a confusión y no aporta una propiedad testeable mejor.