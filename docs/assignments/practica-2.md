# Práctica 2 — Una suite que detecta defectos: Gestión de permisos

Programación III · TDS-007 · ITLA · 2026-C-3
Bloque 3 | Semana 7 | 8 puntos | Individual

| Plazo | Entrega tardía |
|---|---|
| El que indica la plataforma virtual. | Hasta 24 horas después, con 50 % de penalidad. Luego no se acepta. |

Una suite se mide por si falla cuando el código tiene un defecto, no por cuánto código recorre. Se parte de la suite de la Asignación 2 y se amplía hasta cubrir toda la pieza Gestión de permisos; luego se demuestra que detecta seis defectos que publica el profesor. Además se verifica que Control de acceso, entregado en la Práctica 1, siga funcionando.

En resumen: se amplía la suite hasta cubrir las siete reglas de Gestión de permisos; luego se plantan en el código seis defectos, uno por uno, y se comprueba que la suite falla con cada uno.

## 1. Alcance

### 1.1 La suite sobre Gestión de permisos

Para cada requisito hay al menos una prueba cuyo nombre o comentario lo identifica por su ID:

| ID | Requisito | Lo que la suite verifica |
|---|---|---|
| RF-GP-01 | El Estándar crea una solicitud. | La solicitud nace en estado Pendiente. |
| RF-GP-02 | El Administrador consulta las pendientes. | Un Estándar no ve las solicitudes de otros usuarios. |
| RF-GP-03 | El Administrador aprueba una pendiente. | Queda registrado quién aprobó y cuándo. |
| RF-GP-04 | El Administrador rechaza una pendiente con motivo. | Sin motivo, ausente o vacío, el rechazo no se completa y el estado no cambia. |
| RF-GP-05 | Al aprobar, el cambio se aplica automáticamente. | Tras aprobar, la solicitud llega a Aplicada y el permiso pedido queda efectivo. |
| RF-GP-06 | Solo se permiten las transiciones de la tabla. | Aprobar dos veces, pasar de Rechazada a Aplicada y cualquier otra transición fuera de la tabla se rechazan y el estado no cambia. |
| RF-GP-07 | Nadie resuelve su propia solicitud. | Un Administrador que aprueba o rechaza una solicitud creada por él recibe un rechazo. |

Se aplican las condiciones de la Asignación 2: pruebas que ejercitan la pieza sin levantar la aplicación completa, independientes entre sí, con preparar–actuar–verificar, y un único comando documentado en la sección «Pruebas» del README que las ejecuta todas. En el punto etiquetado la suite pasa.

### 1.2 Detección de defectos plantados

Un defecto es un cambio pequeño que rompe a propósito el código de Gestión de permisos. No se corrige: se planta para comprobar que la suite lo detecta. El documento de defectos (`practica-2-defectos.md`) trae los seis defectos de la variante que corresponde según el último dígito de la matrícula: 0 a 3, variante A; 4 a 6, variante B; 7 a 9, variante C. Para cada defecto N (del 1 al 6) se entrega:

- Una etiqueta `practica-2-defecto-N` sobre un commit cuyo padre es el commit etiquetado `practica-2`.
- Ese commit modifica únicamente código de Gestión de permisos, ningún archivo de pruebas. Su mensaje dice qué archivo y qué línea se cambió.
- Sobre ese commit, el comando del README hace fallar al menos una prueba. Sobre `practica-2`, todas pasan.
- En el README, una sección **Defectos** con una línea por defecto: el nombre de la prueba que lo detecta y el comportamiento que comprueba.

Si al plantar un defecto todas las pruebas pasan, la suite no lo detecta: se completa la suite, se vuelve a etiquetar `practica-2` y se planta el defecto de nuevo sobre el nuevo estado.

### 1.3 Revisión acumulada: Control de acceso sigue funcionando

Se arranca el proyecto desde el punto etiquetado, siguiendo el README al pie de la letra (lo que el README no dice, no se busca), y se repiten las comprobaciones de la Práctica 1 en cuatro frentes:

- Registro con activación por correo.
- Inicio y cierre de sesión.
- Rechazo a un usuario Estándar que invoca una operación de Administrador.
- Recuperación de contraseña.

## 2. Cómo se entrega 

```powershell
git tag practica-2
git tag practica-2-defecto-1 <commit del defecto 1>
# ... una etiqueta por defecto, hasta practica-2-defecto-6
git push origin --tags
```

En Moodle, en la tarea «Práctica 2»: la URL del repositorio y el nombre de la etiqueta, `practica-2`. Las seis etiquetas de defecto se leen del mismo repositorio. Se evalúa el punto etiquetado; se puede seguir trabajando en `main` después.

## 3. Rúbrica (8.0 puntos)

Cada criterio se califica como No logrado (0 %), Parcialmente logrado (50 %) o Logrado (100 %).

### Suite sobre Gestión de permisos RF-GP-01 a 07 (1.5 pts)

- **No logrado:** no hay suite, o no pasa en el tag, o requiere levantar la aplicación completa, o a tres o más requisitos RF-GP les falta su prueba.
- **Parcialmente logrado:** la suite pasa, pero a uno o dos requisitos RF-GP les falta su prueba, o no se identifica a qué requisito corresponde cada prueba, o falta el comando documentado.
- **Logrado:** pasa con un solo comando documentado, cada RF-GP-01 a 07 tiene al menos una prueba que lo identifica y las pruebas son independientes y sin la aplicación levantada.

### Detección de los seis defectos (3.0 pts)

- **No logrado:** con ningún defecto plantado falla una prueba, o no hay etiquetas de defecto.
- **Parcialmente logrado:** falla al menos una prueba con uno a cinco de los seis defectos. Cada defecto detectado suma 0.5.
- **Logrado:** con los seis defectos plantados falla al menos una prueba, y sobre `practica-2` todas pasan.

### Forma de los defectos plantados (1.5 pts)

- **No logrado:** no existen las etiquetas `practica-2-defecto-N`, o los commits modifican archivos de pruebas.
- **Parcialmente logrado:** existen las etiquetas, pero en alguno el commit planta algo distinto al defecto de la variante o además otros cambios, o su padre no es `practica-2`, o el mensaje no dice qué línea se cambió, o el README no indica la prueba que detecta cada defecto.
- **Logrado:** los seis commits cuelgan de `practica-2`, plantan exactamente el defecto de la variante sin tocar pruebas, su mensaje dice qué archivo y línea se cambió, y el README identifica la prueba que detecta cada uno.

### Revisión acumulada: Control de acceso (2.0 pts)

- **No logrado:** el proyecto no arranca siguiendo el README, o funciona uno o ninguno de los cuatro frentes.
- **Parcialmente logrado:** funcionan dos o tres de los cuatro frentes.
- **Logrado:** funcionan los cuatro: registro con activación, sesión, rechazo por rol y recuperación de contraseña.

## 4. Qué se revisa exactamente

- `git clone` del repositorio y `git checkout practica-2`.
- El comando de la sección «Pruebas» del README: todas las pruebas pasan.
- Por cada defecto: que el padre de `practica-2-defecto-N` sea `practica-2`, `git diff practica-2 practica-2-defecto-N --stat` (solo código de Gestión de permisos, ningún archivo de pruebas), el mensaje del commit y el mismo comando de pruebas sobre la etiqueta, que debe reportar al menos una prueba fallida.
- El README: la sección «Defectos» contra las pruebas que fallan, y los nombres de las pruebas contra los requisitos RF-GP.
- Control de acceso, arrancado desde el README, en los cuatro frentes.
