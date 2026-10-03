# ADR 004 - Snake case column naming for PostgreSQL

- **Estado:** aceptada
- **Fecha:** 2026-10-03
- **Requisitos:** RD-09; RF-NOT-12 (afectado por la alternativa descartada)
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

Al revisar el esquema de la base aparecieron dos estilos de columna conviviendo
en el mismo esquema:

- **Las tablas ya eran consistentes**: todas en snake_case con prefijo de
  módulo (`ac_usuarios`, `ac_sesiones`, `not_correos_en_cola`). El prefijo es
  deliberado: las dos piezas del Core comparten una base de datos, pero
  compartir la base no significa compartir las tablas; cada pieza es la única
  que lee y escribe las suyas (`ConfiguracionDeBaseDeDatos.cs`, y el comentario
  de `CorreoEnColaConfiguration.cs`). Ese límite de módulo **no se toca**.
- **Las columnas de Notifications** estaban en snake_case de forma **explícita**:
  `CorreoEnColaConfiguration` declara `HasColumnName("id")`,
  `HasColumnName("fecha_creacion_utc")`, etc. Fue una decisión escrita a mano.
- **Las columnas de AccessControl** quedaron en PascalCase
  (`"Correo"`, `"HashDeContrasena"`, `"BloqueoHastaUtc"`) **sin que nadie lo
  decidiera**: los archivos `*Configuration.cs` no declaraban `HasColumnName`,
  así que EF Core aplicó su convención por defecto y usó el nombre de la
  propiedad C# como nombre de columna. Ninguna de las cuatro configuraciones
  contenía una línea que justificara ese estilo.

La mezcla tiene un costo concreto en PostgreSQL: el motor pliega los
identificadores **sin comillas** a minúsculas. Una columna guardada como
`"Correo"` exige comillas dobles en **cada** consulta manual
(`SELECT "Correo" FROM ac_usuarios`), y `SELECT Correo` —escrito en
minúsculas— falla con *column does not exist*. La comilla deja de ser una
elección y pasa a ser un requisito perpetuo que solo se puede recordar o
equivocar.

## Decisión

Todas las columnas de AccessControl pasan a snake_case minúscula, con mapeo
explícito:

1. Las cuatro configuraciones (`UsuarioConfiguration`,
   `CodigoRecuperacionConfiguration`, `TokenActivacionConfiguration`,
   `SesionConfiguration`) declaran `HasColumnName(...)` para **cada** columna,
   en el mismo estilo que ya usaba `CorreoEnColaConfiguration`.
2. El mapeo sigue la convención de nombres de la propia base:
   `HashDeContrasena` → `hash_de_contrasena`, `UsuarioId` → `usuario_id`,
   `BloqueoHastaUtc` → `bloqueo_hasta_utc` (30 columnas en total).
3. El cambio se hace con una migración de renombres
   (`20261003142652_UnificarNombresDeColumnas`): solo `RenameColumn` y la
   recreación de las tres claves foráneas, que son operaciones sobre
   metadatos. No borra filas ni altera tipos.
4. **Solo cambia la persistencia.** Las propiedades C# siguen en PascalCase y
   ningún código de Domain o Application se modifica: EF traduce el mapeo.
5. Notifications no cambia: ya era el estilo elegido y su SQL crudo (el
   `FOR UPDATE SKIP LOCKED` de `CorreoEnColaRepository`) sigue válido sin
   reescritura.
6. El prefijo de tabla (`ac_` / `not_`) **se mantiene**: sigue siendo el
   mecanismo que impide que una pieza toque las tablas de la otra. La
   decisión de esta ADR es sobre el nombre *dentro* de la tabla, no sobre su
   pertenencia.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Unificar a PascalCase (que `not_correos_en_cola` adopte el estilo de AccessControl) | Rompería el SQL crudo de `CorreoEnColaRepository.cs` (`UPDATE not_correos_en_cola SET estado = ... FOR UPDATE SKIP LOCKED`), que es el reclamo atómico de RF-NOT-12; habría que reescribirlo y revalidar el envío único. Además perpetúa las comillas obligatorias en PostgreSQL. |
| Dejar la mezcla como está | Cero costo inmediato, pero cada consulta manual a `ac_*` exige recordar qué tabla usa comillas, los dos módulos quedan con convenciones distintas y el estilo de AccessControl sigue sin ser una decisión, sino un accidente del default de EF. |
| Cambiar también el nombre de las tablas o quitar los prefijos | Rompe el límite de módulo a nivel de datos (RD-01): el prefijo es lo que hace visible que ninguna pieza es dueña de las tablas de la otra. Fuera del alcance de esta decisión. |

## Persistencia y compatibilidad

La migración renombra columnas; los datos quedan en su lugar. Antes de
aplicarla se registraron los conteos de las cinco tablas y después de
aplicarla se repitieron: idénticos (`ac_usuarios` 3,
`ac_codigos_recuperacion` 2, `ac_tokens_activacion` 2, `ac_sesiones` 8,
`not_correos_en_cola` 4).

Los índices conservan sus nombres (`ix_ac_usuarios_correo`, etc.) porque ya
estaban declarados con `HasDatabaseName`. Las tres claves foráneas se
recrean con el nombre nuevo (`FK_ac_sesiones_ac_usuarios_usuario_id`,
en minúsculas, siguiendo la convención de EF). La migración tiene `Down()`
completo: renombra de vuelta a PascalCase.

## Verificación

La decisión se verifica con:

- el esquema leído de `information_schema.columns`: las cuatro tablas `ac_*`
  quedan con todas sus columnas en snake_case minúscula;
- los conteos de filas antes y después, idénticos;
- una consulta en estilo README **sin comillas dobles** —
  `SELECT correo, activo, left(hash_de_contrasena, 25) FROM ac_usuarios` —
  que devuelve las filas con sus hashes Argon2 intactos;
- el arranque de la API, cuya semilla genera
  `SELECT EXISTS (SELECT 1 FROM ac_usuarios AS a WHERE a.rol = 'Administrador')`
  y responde `401` en `/yo` sin credencial;
- el build Release con 0 errores y 0 advertencias;
- las 90 pruebas automatizadas, ninguna fallando (no referencian nombres de
  columna, por lo que el impacto en ellas es nulo).

## Consecuencias

**A favor**

- Un solo estilo en toda la base: columnas y tablas, snake_case.
- Consultas manuales sin comillas dobles: `SELECT correo FROM ac_usuarios`
  funciona como cualquier persona esperaría de PostgreSQL.
- El nombre de columna deja de ser un efecto secundario del default de EF y
  pasa a estar declarado, igual que ya estaba en Notifications.
- El SQL crudo de RF-NOT-12 no se toca.
- Domain y Application quedan intactos: el cambio no se filtra fuera de la
  persistencia.

**En contra, asumido a conciencia**

- Las consultas con comillas dobles escritas contra las columnas antiguas
  (`SELECT "Correo" ...`) dejan de funcionar. El README se corrigió en esta
  misma rama (2026-10-03, decisión de Franklin): todas sus consultas se
  reescribieron en snake_case, se ejecutaron contra la base real y los JSON de
  la API se verificaron contra el proceso corriendo.
- Cualquier SQL externo escrito a mano contra `ac_*` por otra persona o
  herramienta tendrá que ajustarse al rename.
- La migración cambia el esquema de una práctica ya entregada; la práctica 2
  re-verifica este trabajo, por lo que el rename se hizo con conteos antes y
  después y verificación de runtime, no solo con build.

## Qué queda fuera

Esta ADR no renombra tablas, no elimina los prefijos `ac_` / `not_`, no toca
Notifications ni su SQL crudo, no modifica entidades de dominio ni
configuraciones de tipos. Tampoco introduce una convención
global automática (paquete de naming conventions de EF): el mapeo es explícito
y visible en cada archivo de configuración.
