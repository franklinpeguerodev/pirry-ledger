# Plan de ejecución — Práctica 1

## Propósito

Este documento compara el estado comprobado del repositorio con el alcance de
`docs/current-iteration.md` y divide el trabajo restante por ramas funcionales.
La fuente de alcance sigue siendo `docs/current-iteration.md`; este documento no
amplía la Práctica 1 ni reemplaza los requisitos originales.

## Línea base comprobada

Rama revisada: `feature/user-administration`, commit `1ad99e6`.

Comandos ejecutados:

```powershell
dotnet build pirry-ledger.slnx
dotnet test pirry-ledger.slnx --no-build
```

Resultado real:

- Compilación correcta: 0 advertencias y 0 errores.
- 82 pruebas superadas y 0 fallidas.
- `Tests/PirryLedger.Core.Notifications.Tests` existe, pero todavía no contiene
  pruebas detectables.

### Ya implementado

| Área | Estado | Evidencia |
|---|---|---|
| Cola mínima de correo | Implementada | `Src/Core/PirryLedger.Core.Notifications`, comando `--send-mail`, migración y documentación |
| Registro y activación | Implementada | `RF-CA-01`, `02`, `14`, `15`, `16`, `17`; pruebas y flujo HTTP |
| Sesión | Implementada | `RF-CA-03`, `07`, `18`, `19`; token opaco, tabla `Sesion`, invalidación y pruebas |
| Primer Administrador | Implementada en `develop` | Semilla idempotente por variables de entorno y ADR 002 |
| Punto único de autorización | Implementada en la rama revisada | `ExigenciasDeRol`, `FiltroDeAcceso` y pruebas de cobertura |
| Administración de usuarios | Implementada en la rama revisada | listar, cambiar rol, desactivar y reactivar; protecciones contra autooperación |

La administración está en cinco commits locales posteriores a `develop` y
corresponde a la rama `feature/user-administration`. Debe conservarse como PR
propio; no debe rehacerse dentro de recuperación de contraseña.

### Faltantes para cerrar la Práctica 1

1. Revisión final de los cuatro PR funcionales, commits atómicos, ausencia de
   credenciales en el historial y etiqueta `practica-1`.

La recuperación (`RF-CA-09` a `RF-CA-13`, `RF-CA-22`) y la estructura de
`Invoice` (`RF-NEG-03`, `RF-NEG-04`, `RF-NEG-05`, `RD-04`) ya fueron
implementadas y documentadas.

### Contradicciones documentales detectadas

- `README.md` todavía dice en su introducción que la administración de usuarios
  no existe, aunque la rama revisada ya expone sus endpoints.
- La bitácora conserva una sección antigua de “Qué NO incluye” que afirma que
  administración y `RF-CA-05` faltan, seguida de una sección que describe esa
  implementación.
- El README menciona enlaces de recuperación, pero no existe todavía el flujo
  que los genera.
- La máquina de estados no existe en el módulo Business ni como
  `docs/maquina-de-estados.md`.

Estas correcciones deben hacerse después de que cada funcionalidad esté
verificada, no antes, para que la documentación no prometa un flujo inexistente.

## Orden de ramas y PRs

La base de todas las ramas es `develop`, salvo que Franklin indique otra cosa.
Cada rama se integra mediante un PR con exactamente las cuatro secciones de
`.github/PULL_REQUEST_TEMPLATE.md`: **Qué cambia**, **Por qué**, **Cómo
probarlo** y **Qué NO incluye**.

### 0. Estado ya entregado — cola, registro y sesión

No crear ramas nuevas para repetir este trabajo. Mantener los PR históricos:

- `feature/data-access-and-mail-queue`
- `feature/registro-y-activacion`
- `feature/sesion`

Antes de continuar, usar sus criterios como regresión: registro con activación,
login, rechazo por rol, invalidación de sesión y recuperación cuando se añada.

### 1. `feature/user-administration` — administración de usuarios

**Estado:** implementada en la rama revisada; falta cerrar el PR según el flujo
del repositorio.

**Requisitos:** `RF-CA-04`, `RF-CA-05`, `RF-CA-06`, `RF-CA-08`, `RF-CA-20`,
`RF-CA-21`, `RD-06`, `RD-08`.

**Debe quedar incluido:**

- exigencias de rol declaradas en un único punto;
- rechazo server-side de un Estándar aunque invoque la ruta directamente;
- listado sin hashes, tokens ni datos internos;
- cambio de rol;
- desactivación y reactivación;
- invalidación de sesiones al desactivar;
- prohibición de cambiar el propio rol, desactivarse y dejar cero
  Administradores activos;
- semilla del primer Administrador documentada y verificable.

**Verificación mínima:**

```powershell
dotnet test Tests/PirryLedger.Core.AccessControl.Tests/PirryLedger.Core.AccessControl.Tests.csproj
dotnet build pirry-ledger.slnx
```

Probar también por HTTP una petición manual de un usuario Estándar, la
desactivación con sesión abierta y el listado de datos no sensibles.

### 2. `feature/password-recovery` — recuperación y cambio de contraseña

**Estado:** implementada en la rama de ejecución actual; falta separar y cerrar
el PR funcional según el flujo del repositorio.

**Requisitos:** `RF-CA-09`, `RF-CA-10`, `RF-CA-11`, `RF-CA-12`, `RF-CA-13`,
`RF-CA-22`, además de `RF-NOT-08`.

**Archivos/superficies esperados:**

- casos de uso en `AccessControl.Application`;
- endpoint registration dentro de AccessControl, no en Host;
- persistencia de `CodigoRecuperacion` en el repositorio existente;
- uso de `IClock`, `IPasswordHasher`, `IEmailQueue` y
  `CredencialVersion`;
- pruebas unitarias de expiración, uso único, correo existente/inexistente,
  contraseña actual incorrecta, reset forzado e invalidación de sesiones;
- README y bitácora con comandos reproducibles.

**Reglas que no se pueden omitir:**

- iniciar recuperación responde igual exista o no el correo;
- el código se almacena de forma no reversible, vence y solo sirve una vez;
- recuperación y reset forzado encolan correo y no conectan a SMTP;
- la contraseña anterior deja de funcionar;
- toda sesión anterior al cambio o reset queda inválida;
- la política `RF-CA-14` se reutiliza, no se duplica;
- los errores HTTP no exponen correo, token, consultas ni trazas.

**Verificación mínima:**

```powershell
dotnet test Tests/PirryLedger.Core.AccessControl.Tests/PirryLedger.Core.AccessControl.Tests.csproj
dotnet build pirry-ledger.slnx
```

Después, con una base desechable, ejecutar el flujo real: correo existente y
no existente, código usado dos veces, código vencido, contraseña antigua,
sesión emitida antes del cambio y reset forzado por Administrador.

### 3. `feature/business-invoice-state-machine` — estructura de estados

**Estado:** implementada con la decisión explícita de usar `Invoice`; falta
separar y cerrar el PR funcional según el flujo del repositorio.

**Requisitos:** `RF-NEG-03`, `RF-NEG-04`, `RF-NEG-05`, `RD-04`.

Franklin eligió `Invoice` como entidad central y autorizó los estados `Draft`,
`Issued`, `Paid` y `Cancelled`.

Una vez autorizada la elección, la rama debe:

- declarar una entidad central con su estado;
- declarar entre 3 y 5 estados en un único lugar;
- resolver las transiciones permitidas en un único componente;
- representar al menos una transición prohibida explícita;
- tener al menos un estado terminal sin transiciones salientes;
- añadir `docs/maquina-de-estados.md` con columnas `from`, `to`, `who executes`
  y `condition`;
- dejar las pruebas para la iteración de la semana 8, porque están fuera del
  alcance de Práctica 1.

No debe añadir facturación, persistencia completa, endpoints ni reportes que no
exige esta iteración.

### 4. `docs/practica-1-readiness` — documentación y cierre

**Estado:** pendiente de ejecutar después de las ramas funcionales.

Esta rama/PR de documentación debe corregir únicamente lo que ya esté verificado:

- introducción y endpoints de `README.md`;
- variables y pasos exactos de recuperación;
- criterios de aceptación faltantes;
- estado real de administración;
- `docs/bitacora-practica-1.md`, eliminando contradicciones y agregando la
  entrada de recuperación y la de la máquina de estados;
- referencias a `docs/maquina-de-estados.md`;
- instrucciones de migración y pruebas realmente ejecutadas.

No escribir un ADR nuevo en esta rama. Si la elección de la entidad o cualquier
otra decisión cambia estructura, persistencia, protocolo o seguridad, Franklin
debe autorizar primero el ADR correspondiente.

### 5. `chore/practica-1-release` — verificación de entrega

No es una rama para nueva funcionalidad. Es una lista de verificación previa a
crear la etiqueta:

```powershell
git status --short --branch
git diff --staged
dotnet build pirry-ledger.slnx
dotnet test pirry-ledger.slnx
git log -p --all
git check-ignore -v <cada archivo nuevo>
```

Comprobar además:

- cuatro PR funcionales: registro/activación, sesión, recuperación y
  administración;
- cada PR usa las cuatro secciones de la plantilla;
- commits atómicos con asunto imperativo e IDs de requisitos;
- ningún secreto, archivo generado o credencial en el historial;
- migraciones aplicables en una base nueva;
- README ejecutable siguiendo sus pasos sin instrucciones inventadas;
- regresión de registro, login, autorización y recuperación;
- decisión de entidad de estados resuelta y tabla documental presente.

Solo después de esa revisión, Franklin ejecuta y publica:

```powershell
git tag practica-1
git push origin --tags
```

## Dependencias y límites

```text
cola + registro + sesión
          |
          +--> administración de usuarios
          +--> recuperación de contraseña

decisión de entidad --> máquina de estados --> documentación de estados

todas las ramas funcionales --> documentación final --> release/tag
```

Quedan fuera de este plan: reintentos y estado fallido de correo, auditoría,
permisos, documentos, reportes, frontend, inventario completo, pruebas de la
máquina de estados y cualquier infraestructura no pedida por Práctica 1.
