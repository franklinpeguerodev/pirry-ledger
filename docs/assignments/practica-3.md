# Práctica 3 — Tu módulo de negocio, sus pruebas y su integración

Programación III · TDS-007 · ITLA · 2026-C-3
Bloque 3 | Semana 8 | 8 puntos | Individual

| Plazo | Entrega tardía |
|---|---|
| El que indica la plataforma virtual. | Hasta 24 horas después, con 50 % de penalidad. Luego no se acepta. |

Tercer hito de tu repositorio. Entregas tu módulo de negocio funcionando, la suite que protege su máquina de estados con la regla del mutante y la prueba que demuestra que tu negocio y Gestión de permisos trabajan juntos. Además se verifica que Control de acceso y Gestión de permisos sigan funcionando.

En resumen: tu negocio funciona, sus pruebas detectan cuatro defectos que plantas tú en tu propio código, y una prueba de integración conecta tu negocio con Gestión de permisos.

## 1. Alcance

### 1.1 Tu módulo de negocio implementado

Este hito no completa tu módulo de negocio, lo lleva a una etapa. Sus entidades ya existen desde la semana 2 y su estructura se evaluó en la Práctica 1; aquí lo haces funcionar y lo pruebas. Sus notificaciones y sus reportes (RF-NEG-07 y RF-NEG-08) llegan en la semana 10, y adjuntar documentos a una entidad (RF-NEG-10) es opcional.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-NEG-02 | Al menos cinco funcionalidades separables. | Cada una se puede probar por separado de las demás y tu README explica cómo probarla. |
| RF-NEG-03 | Máquina de estados propia, entre 3 y 5 estados. | Los estados están declarados en un solo lugar del código. |
| RF-NEG-04 | Al menos una transición prohibida de forma explícita. | Intentarla se rechaza y el estado no cambia. |
| RF-NEG-05 | Al menos un estado terminal del que no se puede salir. | Ninguna transición parte de ese estado. |
| RF-NEG-06 | Sus operaciones están protegidas por Control de acceso. | Ninguna operación del negocio se ejecuta sin sesión válida y sin verificación de rol. |
| RF-NEG-09 | Su máquina de estados es independiente de la de Gestión de permisos. | Cambiar los estados de Gestión de permisos no obliga a cambiar los del negocio. |

Aplican además RD-04 (las transiciones se resuelven en un solo punto del código), RD-07 (entrada inválida produce un rechazo controlado) y RD-09 (los datos del negocio sobreviven a reiniciar la aplicación). Tu archivo `docs/maquina-de-estados.md` refleja lo que implementaste. Lo que no está en la tabla de arriba no se califica en esta práctica.

Al menos una operación de tu negocio requiere un permiso concedido por Gestión de permisos: el usuario Estándar la solicita, un Administrador la aprueba y solo entonces puede ejecutarla.

### 1.2 La suite de tu máquina de estados

- Pruebas con el framework de pruebas del lenguaje que usas, que ejercitan tu máquina de estados sin levantar la aplicación completa (RD-12).
- Al menos una prueba por cada fila de las tablas de transiciones permitidas y prohibidas de tu `docs/maquina-de-estados.md`. En las prohibidas, la prueba verifica el rechazo y que el estado no cambió. Además, una prueba de que del estado terminal no se sale.
- Cada prueba tiene tres fases identificables (preparar, actuar, verificar), verifica un solo comportamiento y su nombre dice cuál. Las pruebas son independientes entre sí.
- Un único comando, documentado en la sección **Pruebas** de tu README, ejecuta toda la suite desde un clon limpio. La suite pasa.

### 1.3 Cuatro defectos plantados en tu propio código

Igual que en la Práctica 2, un defecto es un cambio pequeño que rompe a propósito tu código. No lo corriges: lo plantas para comprobar que tu suite lo detecta. Como tu máquina de estados es distinta de la de tus compañeros, los defectos se describen sobre tus tablas. Si tu código expresa la regla de otra forma, aplica el cambio equivalente y no toques nada más.

| Defecto | Qué plantar en tu código | Cómo se comporta tu aplicación con el defecto |
|---|---|---|
| 1 | Agrega a las transiciones permitidas la primera transición de la tabla de prohibidas de tu `docs/maquina-de-estados.md`. | Una transición prohibida pasa a ser permitida. |
| 2 | Agrega una transición que parta de tu estado terminal. | El estado terminal deja de ser terminal. |
| 3 | Elimina la verificación de la condición de la primera transición de tu tabla que declare una condición (por ejemplo, quién la ejecuta). | Esa transición se ejecuta sin cumplir su condición. |
| 4 | En la operación de tu negocio que depende de un permiso de Gestión de permisos, elimina la consulta a Gestión de permisos. | La operación se permite aunque el permiso no se haya aprobado. |

Para cada defecto N (1 a 4) entregas:

- Una etiqueta `practica-3-defecto-N` sobre un commit cuyo padre es el commit etiquetado `practica-3`.
- Ese commit modifica únicamente código de producción, ningún archivo de pruebas. Su mensaje dice qué archivo y qué línea cambiaste.
- Sobre ese commit, el comando de tu README hace fallar al menos una prueba. Sobre `practica-3`, todas pasan.
- En tu README, una sección **Defectos** con una línea por defecto: el nombre de la prueba que lo detecta y el comportamiento que comprueba.

Si al plantar un defecto todas las pruebas pasan, tu suite no lo detecta: completa la suite, vuelve a etiquetar `practica-3` y planta el defecto de nuevo sobre el nuevo estado. El defecto 4 lo debe detectar tu prueba de integración.

### 1.4 La prueba de integración

- Al menos una prueba que use Gestión de permisos y tu módulo reales, sin dobles que reemplacen a ninguno de los dos y sin la aplicación levantada.
- La prueba verifica las dos caras: la operación de tu negocio se rechaza mientras la solicitud no está aprobada, y se permite después de que un Administrador la aprueba.

### 1.5 Revisión acumulada

Se arranca tu proyecto desde el punto etiquetado, siguiendo tu README al pie de la letra (lo que el README no dice, no se busca), y se comprueban cinco frentes:

- Control de acceso: registro con activación por correo.
- Control de acceso: inicio y cierre de sesión.
- Control de acceso: rechazo a un usuario Estándar que invoca una operación de Administrador.
- Control de acceso: recuperación de contraseña.
- Gestión de permisos: las solicitudes se crean, se aprueban y se rechazan, y las transiciones fuera de la tabla se rechazan.

## 2. Cómo se entrega

```powershell
git tag practica-3
git tag practica-3-defecto-1 <commit del defecto 1>
# ... una etiqueta por defecto, hasta practica-3-defecto-4
git push origin --tags
```

En Moodle, en la tarea «Práctica 3»: la URL de tu repositorio y el nombre de la etiqueta, `practica-3`. Las cuatro etiquetas de defecto se leen del mismo repositorio. Se evalúa el punto etiquetado; puedes seguir trabajando en `main` después.

## 3. Rúbrica (8.0 puntos)

Cada criterio se califica como No logrado (0 %), Parcialmente logrado (50 %) o Logrado (100 %).

### Módulo de negocio implementado RF-NEG-02 a 06 y 09 (1.5 pts)

- **No logrado:** no hay operaciones del negocio funcionando, o se ejecutan sin sesión válida.
- **Parcialmente logrado:** hay menos de cinco funcionalidades, o alguna operación no exige el rol que le corresponde, o las transiciones no se resuelven en un solo punto del código.
- **Logrado:** al menos cinco funcionalidades separables, protegidas por Control de acceso, con estados y transiciones en un solo lugar, la prohibida y el estado terminal rechazados, y el README explica cómo provocar cada una.

### Suite de tu máquina de estados (1.0 pt)

- **No logrado:** no hay suite, o no corre, o requiere levantar la aplicación completa, o falla en el tag.
- **Parcialmente logrado:** la suite corre y pasa, pero falta la prueba de alguna fila de tus tablas o del estado terminal, o alguna prueba depende del orden, o no tiene tres fases identificables.
- **Logrado:** un comando documentado ejecuta la suite desde un clon limpio, pasa, y hay al menos una prueba por cada fila de tus tablas y por el estado terminal.

### Detección de los defectos y su forma (1.5 pts)

- **No logrado:** con ningún defecto plantado falla una prueba, o no hay etiquetas de defecto.
- **Parcialmente logrado:** falla una prueba con uno o dos de los tres defectos, o con los tres pero la forma no cumple: algún commit no cuelga de `practica-3`, toca pruebas, su mensaje no dice la línea o el README no identifica la prueba que lo detecta.
- **Logrado:** con los tres defectos falla al menos una prueba; sobre `practica-3` todas pasan; los commits cuelgan de `practica-3`, no tocan pruebas, su mensaje dice archivo y línea, y el README identifica la prueba que detecta cada uno.

### Prueba de integración con Gestión de permisos (2.0 pts)

- **No logrado:** no hay prueba de integración, o usa dobles en lugar de los componentes reales, o requiere la aplicación levantada.
- **Parcialmente logrado:** hay prueba real con las dos caras, pero con el defecto 4 plantado todas las pruebas pasan; o solo verifica una de las dos caras.
- **Logrado:** la prueba usa los componentes reales, verifica el rechazo antes de aprobar y la ejecución después de aprobar, y falla con el defecto 4 plantado (etiqueta `practica-3-defecto-4` con la forma pedida).

### Revisión acumulada: Control de acceso y Gestión de permisos (2.0 pts)

- **No logrado:** el proyecto no arranca siguiendo el README, o funciona uno de los cinco frentes o ninguno.
- **Parcialmente logrado:** funcionan de dos a cuatro de los cinco frentes.
- **Logrado:** funcionan los cinco frentes.

Total: 8.0 puntos.

## 4. Qué se revisa exactamente

- `git clone` del repositorio y `git checkout practica-3`.
- El comando de la sección «Pruebas» del README: todas las pruebas pasan.
- Por cada defecto: que el padre de `practica-3-defecto-N` sea `practica-3`, `git diff practica-3 practica-3-defecto-N --stat` (solo código de producción, ningún archivo de pruebas), el mensaje del commit y el mismo comando de pruebas sobre la etiqueta, que debe reportar al menos una prueba fallida.
- El README: la sección «Defectos» contra las pruebas que fallan, y cómo provocar cada funcionalidad del negocio.
- Tu `docs/maquina-de-estados.md` contra tu código, y la prueba de integración.
- Control de acceso y Gestión de permisos, arrancados desde el README, en los cinco frentes.

## Notas del repositorio (no son texto del profesor)

Franklin las añadió al transcribir el PDF. El agente las aplica y no las trata como una contradicción que hay que preguntar.

- **«Tres» defectos.** La rúbrica del PDF dice «los tres defectos» en dos filas, pero el alcance (sección 1.3) y la entrega (sección 2) hablan de cuatro. Son **cuatro** defectos, de `practica-3-defecto-1` a `practica-3-defecto-4`.
- **«Entre 3 y 5 estados» (RF-NEG-03).** Franklin indica que el profesor confirmó que el mínimo es 3 y que no hay máximo. La máquina de la factura tiene siete estados. La decisión está registrada en `docs/current-iteration.md`. El texto de la tabla de arriba se deja como lo escribió el profesor.