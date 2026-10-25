# ADR 010 - Role serializado como texto en las respuestas

- **Estado:** aceptada
- **Fecha:** 2026-10-24
- **Requisitos:** RF-CA-21 (el listado dice el role de cada usuario); RD-06 (la
  autorización se verifica en el servidor)
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

RF-CA-21 pide que un Administrador liste los usuarios **con su rol y su
estado**. El listado administrativo devuelve la entidad proyectada
`ListedUser`, que lleva `Role Role`: el enum de dominio.

Por defecto, `System.Text.Json` serializa un enum como su **número**. La
respuesta real de `/api/admin/users` era:

```json
{"id":"...","name":"...","email":"...","role":0,"active":true,"createdAtUtc":"..."}
```

Un `0` no dice si es Administrador o Estándar: el cliente tiene que saber de
memoria el orden del enum para interpretarlo. El README prometía el nombre del
role, y el enunciado pide que el listado diga el rol.

La causa no era la proyección, sino la configuración de serialización: el Host
no registraba ningún converter, así que se aplicaba el valor por defecto del
framework.

El enum `Role` también se guarda en la base como texto (`HasConversion<string>()`
en `UserConfiguration`), por lo que la columna `ac_users.role` ya contiene
`Administrator` / `Standard`. El número solo aparecía en el JSON.

## Decisión

El Host registra un `JsonStringEnumConverter` en las opciones de JSON de HTTP,
y el `role` de las respuestas viaja como texto:

```json
{"id":"...","name":"...","email":"...","role":"Administrator","active":true,"createdAtUtc":"..."}
```

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
```

El converter se registra **en el Host**, no en la pieza de AccessControl, por la
misma razón que el resto de la configuración de la aplicación: el Host es
composición y lee la configuración; la pieza no sabe nada del Host. La
decisión de qué se serializa sigue siendo de la pieza, que es quien decide la
forma de `ListedUser`.

El alcance es el que tiene hoy: **solo `Role` se serializa como enum** en una
respuesta. `PermissionRequestState` se convierte a texto a mano en el mapeo de
la pieza de permisos, y `Operation`, `AccessLevel` y `RoleRequirements` no
viajan nunca en una respuesta. Si en el futuro un enum nuevo llegara a una
respuesta, hereda la serializacion como texto, que es lo que quiere el
criterio.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Dejar el enum como número y corregir el README | El README documentaría que el cliente tiene que descifrar un `0`. RF-CA-21 pide que el listado diga el rol, y un número no lo dice: el contrato dependería de un detalle de implementación del servidor que el cliente no puede ver. |
| Convertir `Role` a `string` en `ListedUser`, como hace `MeResponse` | Obliga a convertir el enum a texto en cada proyección y a mantener el paso a mano en cada sitio donde aparezca. Un converter lo resuelve una vez y no se puede olvidar en una proyección nueva. |
| Serializar solo `Role` con un converter por tipo | `[JsonConverter(typeof(...))]` sobre el enum ata la pieza de dominio al formato JSON de la API. La opcion global mantiene la pieza sin dependencias de presentacion (RD-02). |
| Renombrar el enum para que sus valores fuesen legibles (`AdministratorRole.Standard`) | No arregla el problema: seguiria serializandose como numero. El problema es la serializacion, no el nombre de los valores. |

## Consecuencias

**A favor**

- RF-CA-21 se cumple en letra: el listado dice el rol de cada usuario.
- El cliente no depende del orden del enum para leer la respuesta.
- La base y el JSON hablan el mismo idioma: los dos usan `Administrator` y
  `Standard`.
- El README puede documentar los nombres de campo reales sin excusas.

**En contra, asumido a conciencia**

- Cambia el contrato HTTP de `/api/admin/users`: `role` pasa de numero a
  texto. Un cliente que leyera el numero tendria que actualizarse. No hay
  clientes externos todavia (la API se consume desde la tablet del negocio),
  y el enunciado pide el texto.
- El converter es global: un enum nuevo que se serialice sale como texto
  automaticamente. Es el comportamiento que se quiere, pero conviene tenerlo
  presente al anadir una enumeracion a una respuesta.

## Qué queda fuera

- Cambiar los valores del enum `Role`. Siguen siendo `Administrator` y
  `Standard`, que son los del enunciado (RF-CA-04).
- Serializar como texto los estados de la pieza de permisos: ya se hacen a
  mano en `PermissionRequestMapping` y funcionan.
- Versionar el contrato de la API. No hay versionado en este repositorio.
