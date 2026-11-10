# ADR 018 - Migrations run inside the composition

- **Estado:** aceptada
- **Fecha:** 2026-11-10
- **Requisitos:** RD-03, RD-08, RD-09; sección 1.2 del enunciado de la Práctica 4 ("sin pasos manuales (crear una base, ejecutar un script) fuera de la composición")
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

Hoy el esquema se aplica a mano. El paso 6 del README ejecuta cuatro comandos,
uno por módulo:

```powershell
dotnet ef database update --project Src/Core/PirryLedger.Core.AccessControl/PirryLedger.Core.AccessControl.Infrastructure
dotnet ef database update --project Src/Core/PirryLedger.Core.Notifications/PirryLedger.Core.Notifications.Infrastructure
dotnet ef database update --project Src/Core/PirryLedger.Core.Permissions/PirryLedger.Core.Permissions.Infrastructure
dotnet ef database update --project Src/Business/PirryLedger.Business/PirryLedger.Business.Infrastructure
```

Eso es un paso manual con una herramienta de desarrollo (`dotnet ef`) que la
imagen final no lleva: la etapa de ejecución es `aspnet:10.0`, sin SDK y sin
`dotnet-ef`. La Práctica 4 prohíbe los pasos manuales fuera de la composición y
la rúbrica baja la nota cuando existen.

La composición arranca la aplicación, PostgreSQL y SeaweedFS. Falta decidir
quién crea las tablas, en qué orden y qué pasa si falla.

## Decisión

### 1. El comando `--migrate` dentro del Host

El Host gana un modo alternativo, igual que el del correo (ADR 007):

```
dotnet PirryLedger.Host.dll --migrate
```

Aplica las migraciones de todos los módulos y termina. No arranca Kestrel ni
atiende peticiones.

**Por qué no un contenedor con `dotnet ef`.** Obligaría a una segunda imagen con
el SDK o a instalar la herramienta en la imagen de ejecución: más tamaño, más
superficie y una herramienta de diseño dentro de producción. `Database.Migrate()`
solo necesita el proveedor y las clases de migración, que ya viajan compiladas en
los ensamblados publicados.

### 2. Un paso de migración por módulo, detrás de un puerto

Cada pieza registra su propio paso. El puerto vive en
`PirryLedger.Core.Contracts.Persistence`:

```
public interface IModuleMigration
{
    string Name { get; }
    Task ApplyAsync(CancellationToken cancellationToken);
}
```

Cada `Infrastructure` implementa el suyo resolviendo su `DbContext` y llamando
`Database.Migrate()`. El Host recorre todos los pasos registrados, en orden de
registro, y para en el primer fallo.

**Por qué un puerto y no una lista de `DbContext` en el Host.** Con una lista de
tipos, el Host tendría que conocer el contexto de cada pieza y cambiar cada vez
que aparece una nueva. Con el puerto, una pieza se suma registrando su paso y el
Host no se toca. El Host sigue siendo composición: recorre y no decide.

**Por qué el orden es el de registro.** No hay claves foráneas entre piezas
(cada módulo es dueño de sus tablas), así que el orden no cambia el resultado.
Fijarlo en un sitio deja el comportamiento determinista y legible.

### 3. Las migraciones van antes del seed

`--migrate` aplica **solo esquema**. Los seeds que ya existen —la sucursal global
y el primer Administrator— siguen corriendo al arrancar la aplicación, después de
que el esquema esté aplicado.

**Por qué.** Si el comando de migración ejecutara también los seeds, el servicio
de migración crearía datos de arranque antes de que exista la aplicación, y un
fallo del seed se confundiría con un fallo de esquema. Cada cosa en su momento.

### 4. Un servicio `migrate` en la composición

La composición gana un servicio con la misma imagen y el comando `--migrate`:

```
migrate:
  build: .
  command: ["dotnet", "PirryLedger.Host.dll", "--migrate"]
  depends_on:
    db:
      condition: service_healthy

app:
  depends_on:
    db:
      condition: service_healthy
    migrate:
      condition: service_completed_successfully
```

**Por qué un servicio y no la aplicación migrando al arrancar.** Con un servicio,
el esquema se aplica una vez y la aplicación arranca sabiendo que está, con un
orden explícito y visible en el archivo. Si la aplicación migrara sola, cada
arranque mezclaría esquema y servicio, un fallo de migración dejaría una
aplicación a medio levantar confundida con un fallo de arranque, y dos instancias
podrían competir por la misma migración.

### 5. Códigos de salida y mensajes

| Situación | Salida | Qué se escribe |
|---|---|---|
| Todas las migraciones aplicadas | `0` | Una línea por módulo, con el número de migraciones aplicadas |
| Un módulo falla | `1` | El nombre del módulo y un código corto (por ejemplo `MIGRATION_FAILED`); el detalle va al log del proceso |

La respuesta de la API nunca incluye el error de migración: no hay traza, ni
rutas, ni consultas (RD-08). El log del contenedor sí lleva el detalle, porque es
donde se diagnostica un despliegue y no lo ve el usuario final.

### 6. El camino local no cambia

Quien trabaja sin Docker sigue aplicando el esquema con `dotnet ef database update`,
como hoy. El README mantiene ese paso para el desarrollo local y documenta
`docker compose up` como el camino único cuando hay contenedores.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| La aplicación migra al arrancar | Mezcla esquema y servicio, alarga cada arranque, un fallo de migración parece un fallo de arranque y dos instancias compiten. |
| Un servicio que ejecuta `dotnet ef` | Mete el SDK o `dotnet-ef` en la imagen de ejecución: más superficie y una herramienta de diseño en producción. |
| Un script de inicialización en la imagen de PostgreSQL | El SQL viviría fuera del modelo y de las migraciones: se desincroniza a la primera. |
| `EnsureCreated()` en lugar de migraciones | Crea el esquema sin historial: no se puede evolucionar ni saber qué se aplicó. |
| Seguir con el paso manual | El enunciado lo prohíbe y la rúbrica lo penaliza. |
| Un *job* de migración fuera de la composición (CI, tarea externa) | Es exactamente el paso manual que hay que eliminar. |
| Migrar cada módulo en su propio servicio | Cinco servicios para algo que es secuencial y de segundos; además, el orden tendría que vivir en la composición y no en el código. |

## Consecuencias

**A favor**

- `docker compose up --build` crea el esquema solo: no hay paso manual ni
  herramienta de desarrollo en la imagen.
- El orden es explícito y el fallo detiene el arranque de la aplicación en vez de
  dejarla sirviendo contra un esquema incompleto.
- Una pieza nueva se suma registrando su paso; el Host no cambia.
- El camino local sin Docker sigue funcionando igual que antes.

**En contra, asumido a conciencia**

- Un servicio más en la composición, efímero y con código de salida.
- `--migrate` y `--send-mail` comparten la validación de variables de entorno:
  si falta una obligatoria, el comando termina con el mensaje de la variable.
- No hay vuelta atrás: las migraciones son de solo avance, como el resto del
  proyecto. Un error se corrige con una migración nueva.
- Se asume una sola instancia de migración. No hay bloqueo entre migradores
  concurrentes y no hace falta hoy.

## Qué queda fuera

- Deshacer migraciones (*down*), *rollback* automático o *blue/green*.
- Sembrar datos desde las migraciones: los seeds siguen en el arranque de la
  aplicación.
- Un servicio de migración para otros entornos (nube, CI).
- Comprobar la versión del esquema desde la API.
