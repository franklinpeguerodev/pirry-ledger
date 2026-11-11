# Práctica 4 — Tu proyecto en un contenedor, con documentos y una API externa

Programación III · TDS-007 · ITLA · 2026-C-3
Bloque 4 | Semana 9 | 8 puntos | Individual

| Plazo | Entrega tardía |
|---|---|
| El que indica la plataforma virtual. | Hasta 24 horas después, con 50 % de penalidad. Luego no se acepta. |

Cuarto hito de tu repositorio. Construyes el Manejador de documentos del Core, empaquetas tu proyecto para que cualquier persona lo corra con un solo comando, haces que sus archivos sobrevivan a destruir el contenedor e integras una API externa cuya clave nunca aparece en el repositorio. Además se verifica que Control de acceso, Gestión de permisos y tu módulo de negocio sigan funcionando.

En resumen: quien clone tu repositorio, copie un archivo de ejemplo y ejecute un comando, tiene tu proyecto corriendo con sus documentos guardados, sin instalar nada más que Docker.

## 1. Alcance

### 1.1 Tu Manejador de documentos

Pieza 3 del Core. Todas sus operaciones requieren sesión válida.

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-DOC-01 | Subir un documento registrando nombre, tipo, tamaño, fecha y quién lo subió. | El registro queda consultable con todos esos datos. |
| RF-DOC-02 | Listar los documentos que el usuario tiene permitido ver. | El listado no incluye documentos eliminados ni de otros usuarios, salvo para el Administrador. |
| RF-DOC-03 | Descargar un documento. | Solo el dueño o un Administrador lo logran; cualquier otro usuario recibe un rechazo. |
| RF-DOC-04 | Eliminar un documento de forma lógica, no física. | Deja de aparecer en el listado, pero el registro sigue existiendo en el almacenamiento. |

Aplican además RD-06 (la autorización se verifica en el servidor: invocar la operación directamente también se rechaza) y RD-07 (un archivo ausente o vacío produce un rechazo controlado, no una excepción sin manejar). La auditoría de subidas y eliminaciones (RF-DOC-06) llega con la Pieza 6 en la semana 12; en esta práctica no se califica.

### 1.2 Tu proyecto levanta con un solo comando

Tu proyecto completo corre en contenedores. Entregas la receta para construirlo y la composición que lo arranca:

- Un `Dockerfile` que construye la imagen de tu aplicación, y un `.dockerignore` que deja fuera lo que no debe copiarse (dependencias instaladas, carpetas de compilación, el historial de git).
- Un `docker-compose.yml` que arranca todo lo que tu proyecto necesita: tu aplicación y, si usas un motor de base de datos, ese motor como otro servicio. Los servicios se encuentran entre sí por nombre, no por `localhost`.
- Un único comando, `docker compose up --build`, arranca el proyecto desde un clon limpio, sin instalar nada en el equipo salvo Docker y sin pasos manuales (crear una base, ejecutar un script) fuera de la composición.
- Tú eliges el puerto del equipo en el que tu aplicación responde. Solo tu aplicación publica un puerto: la base de datos no es accesible desde fuera de la composición.
- Una sección **Contenedor** en tu README con el comando, la dirección completa donde responde (por ejemplo `http://localhost:PUERTO`, con tu puerto) y cómo detener el proyecto.

### 1.3 Los datos sobreviven

- Los archivos de los documentos se guardan en un volumen con nombre, fuera de la imagen (RF-DOC-05).
- Los datos de tu aplicación (usuarios, solicitudes, registros de documentos) también sobreviven (RD-09). Si usas un motor de base de datos, su carpeta de datos va en otro volumen.
- La prueba es esta: subes un documento, ejecutas `docker compose down` (sin `-v`), vuelves a levantar con `docker compose up`, y el documento sigue en el listado y se descarga. Tu usuario sigue pudiendo iniciar sesión.

### 1.4 Una API externa con su clave como secreto

Integras al proyecto una API externa real que exija una clave (clima, tasa de cambio o geolocalización: la que se ajuste mejor a tu dominio). Esto es lo que se pide:

- Al menos una operación de tu proyecto, del Core o de tu negocio, usa datos que obtiene de esa API. Un endpoint suelto que solo reenvía la respuesta no cuenta.
- La clave se lee de una variable de entorno (RD-10). Llega al contenedor desde un archivo `.env` que no se versiona: tu `.gitignore` lo excluye.
- Versionas un `.env.example` con todas las variables que tu proyecto necesita y un valor de ejemplo para cada una. Copiar ese archivo a `.env` es el único paso previo para levantar el proyecto.
- La clave no está en el código, en el `Dockerfile`, en el `docker-compose.yml`, en la imagen ni en ningún commit del historial.
- Si la clave no está definida o la API no responde, la operación devuelve un rechazo controlado con un mensaje comprensible (RD-07, RD-08) y el resto del proyecto sigue funcionando.
- Una sección **API externa** en tu README: qué API es, qué operación la usa, el nombre de la variable y un ejemplo real de la respuesta que obtuviste (sin la clave).

### 1.5 Revisión acumulada

Se copia tu `.env.example` a `.env`, se arranca tu proyecto desde el punto etiquetado con el comando de tu README, siguiéndolo al pie de la letra (lo que el README no dice, no se busca), y se comprueban seis frentes:

- Control de acceso: registro con activación por correo.
- Control de acceso: inicio y cierre de sesión.
- Control de acceso: rechazo a un usuario Estándar que invoca una operación de Administrador.
- Control de acceso: recuperación de contraseña.
- Gestión de permisos: las solicitudes se crean, se aprueban y se rechazan, y las transiciones fuera de la tabla se rechazan.
- Tu módulo de negocio: una operación válida funciona y la transición prohibida de tu máquina de estados se rechaza.

## 2. Cómo se entrega

```powershell
git tag practica-4
git push origin --tags
```

En Moodle, en la tarea «Práctica 4»: la URL de tu repositorio y el nombre de la etiqueta, `practica-4`. Se evalúa el punto etiquetado; puedes seguir trabajando en `main` después.

## 3. Rúbrica (8.0 puntos)

Cada criterio se califica como No logrado (0 %), Parcialmente logrado (50 %) o Logrado (100 %).

### Manejador de documentos RF-DOC-01 a 04 (2.0 pts)

- **No logrado:** no se puede subir un documento, o las operaciones se ejecutan sin sesión válida.
- **Parcialmente logrado:** subir, listar, descargar y eliminar funcionan, pero falla alguna regla: el listado incluye eliminados o documentos ajenos, otro usuario Estándar logra descargar o eliminar un documento ajeno, o la eliminación borra el registro en lugar de ocultarlo.
- **Logrado:** subir guarda nombre, tipo, tamaño, fecha y quién lo subió; el listado excluye eliminados y ajenos (salvo para el Administrador); solo el dueño o un Administrador descarga o elimina y a otro usuario se le rechaza; la eliminación es lógica.

### Contenedor con un solo comando (1.5 pts)

- **No logrado:** el comando falla, o el contenedor sale al arrancar, o el proyecto no responde.
- **Parcialmente logrado:** el proyecto levanta, pero requiere pasos manuales fuera de la composición (crear la base, instalar algo), o responde en un puerto distinto del que dice el README, o el README no documenta el comando o el puerto.
- **Logrado:** desde un clon limpio, copiar `.env.example` a `.env` y ejecutar `docker compose up --build` levanta el proyecto completo, que responde en el puerto que indica el README. Hay `Dockerfile`, `.dockerignore`, `docker-compose.yml` y una sección «Contenedor» en el README.

### Volumen persistente RF-DOC-05 (1.0 pt)

- **No logrado:** los documentos subidos desaparecen al reiniciar el contenedor.
- **Parcialmente logrado:** los documentos sobreviven a reiniciar el contenedor, pero no a `docker compose down` y volver a levantar; o sobreviven, pero los datos de la aplicación (usuarios) no.
- **Logrado:** tras `docker compose down` (sin `-v`) y volver a levantar, el documento se lista y se descarga, y el usuario registrado inicia sesión. El archivo vive en un volumen, no en la imagen.

### API externa con la clave como secreto (1.5 pts)

- **No logrado:** no hay API externa integrada, o la clave está escrita en el código, en el `Dockerfile`, en el `docker-compose.yml` o en algún commit.
- **Parcialmente logrado:** la integración funciona y la clave llega por variable de entorno, pero hay un archivo `.env` con valor real versionado o en el historial; o sin clave la aplicación falla con una excepción sin manejar.
- **Logrado:** una operación del proyecto usa datos de la API externa; la clave llega por variable de entorno desde un `.env` no versionado; `.env.example` trae la variable con un valor de ejemplo; ningún commit contiene la clave; sin clave o con la API caída el rechazo es controlado y el resto del proyecto sigue funcionando; el README documenta la integración.

### Revisión acumulada: Control de acceso, Gestión de permisos y negocio (2.0 pts)

- **No logrado:** el proyecto no levanta con el comando del README, o funciona uno de los seis frentes o ninguno.
- **Parcialmente logrado:** funcionan de dos a cinco de los seis frentes.
- **Logrado:** funcionan los seis frentes.

Total: 8.0 puntos.

## 4. Qué se revisa exactamente

- `git clone` del repositorio y `git checkout practica-4`.
- La presencia de `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example` y un `.gitignore` que excluye `.env`; y que `.env` no exista en ningún commit.
- `.env.example` copiado a `.env` y el comando de la sección «Contenedor» del README: el proyecto levanta y responde en la dirección que indica el README.
- Subir un documento, listarlo, descargarlo y eliminarlo con su dueño, y comprobar que otro usuario Estándar es rechazado al descargar y al eliminar.
- `docker compose down` y volver a levantar: el documento sigue ahí y el usuario inicia sesión.
- La operación que usa la API externa, con la clave de ejemplo (rechazo controlado), y la sección «API externa» del README.
- El historial completo, en busca de la clave o de un archivo `.env`.
- Control de acceso, Gestión de permisos y tu módulo de negocio, arrancados desde el README, en los seis frentes.
