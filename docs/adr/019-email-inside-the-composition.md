# ADR 019 - Email in the composition

- **Estado:** aceptada
- **Fecha:** 2026-11-10
- **Requisitos:** RF-NOT-08, RF-NOT-09, RF-NOT-12, RF-NOT-13; RF-CA-01 (activación), RF-CA-09 (recuperación); sección 1.2 y 1.5 del enunciado de la Práctica 4
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

El correo saliente ya está resuelto en el código: se encola en la base y lo envía
un proceso aparte con el comando `--send-mail` (ADR 007). Correrlo dos veces no
duplica envíos (RF-NOT-12), y sin variables de SMTP el emisor no entrega nada
pero la aplicación funciona (RF-NOT-13).

La Práctica 4 añade tres condiciones que ese diseño no resolvía:

1. El revisor completa el alta con **activación por correo** y la **recuperación
   de contraseña** siguiendo **solo el README**, sin credenciales propias.
2. Copiar `.env.example` a `.env` es el **único** paso previo. Si el archivo
   trae un SMTP de mentira, no se entrega ningún correo; si trae una cuenta real,
   el revisor necesita una cuenta que no tiene.
3. **Solo la aplicación publica un puerto.** Una bandeja de correo con interfaz
   web publicada incumple el enunciado.

Hoy nadie ejecuta `--send-mail` dentro de la composición: el correo queda
encolado para siempre.

## Decisión

### 1. Un servicio `mailpit` como destino SMTP, sin puerto publicado

La composición arranca `mailpit`, que escucha SMTP en el puerto 1025 y expone su
interfaz y su API en el 8025, **ambos solo dentro de la red de la composición**.

La aplicación apunta a él con las variables de SMTP que ya existen:

| Variable | Valor en la composición |
|---|---|
| `PIRRY_LEDGER_SMTP_HOST` | `mailpit` |
| `PIRRY_LEDGER_SMTP_PORT` | `1025` |
| `PIRRY_LEDGER_SMTP_SECURITY` | `Ninguno` |
| `PIRRY_LEDGER_SMTP_USER` | vacío |
| `PIRRY_LEDGER_SMTP_PASSWORD` | vacío |
| `PIRRY_LEDGER_SMTP_FROM` | un remitente de ejemplo |

**Por qué un receptor local y no una cuenta real.** El enunciado exige que
copiar `.env.example` baste. Una cuenta de Gmail obligaría al revisor a traer
credenciales propias, y con credenciales de mentira el alta no se podría
completar.

**Por qué sin puerto publicado.** El enunciado lo dice: solo la aplicación
publica un puerto. La bandeja se lee desde dentro de la red (punto 3).

**Por qué mailpit.** Es un binario pequeño, con SMTP y API HTTP para leer los
mensajes, y no necesita configurarse. Su contenido es desechable: no lleva
volumen, porque los correos de una prueba no son datos de la aplicación.

### 2. Un servicio `mailer` que ejecuta el comando, en bucle

La composición arranca un segundo proceso con la **misma imagen**:

```
mailer:
  build: .
  entrypoint: ["/bin/sh", "-c"]
  command: ["while true; do dotnet PirryLedger.Host.dll --send-mail; sleep 10; done"]
  depends_on:
    db:
      condition: service_healthy
    migrate:
      condition: service_completed_successfully
```

**Por qué un bucle y no un proceso que arranque solo dentro de la aplicación.**
El ADR 007 decidió que el envío es un **comando**, no un servicio alojado dentro
de la aplicación. Un contenedor que repite el comando respeta esa decisión: el
envío sigue siendo un proceso aparte, y la aplicación no envía correo.

**Por qué cada 10 segundos.** El revisor se registra, abre la bandeja y espera
segundos, no minutos. El comando solo toma lo pendiente, así que repetirlo no
duplica envíos (RF-NOT-12).

**Por qué la misma imagen.** No hay una segunda imagen que mantener ni una
herramienta extra: el comando ya existe y está probado.

### 3. El revisor lee el correo desde dentro de la red

El README documenta el comando exacto, sin publicar nada:

```bash
docker compose exec mailpit wget -qO- http://localhost:8025/api/v1/messages
```

De ahí salen el **enlace de activación** (que apunta a `http://localhost:8090`,
la dirección pública de la aplicación) y el **código de recuperación** que viaja
en el cuerpo del mensaje.

**Por qué así.** Es la única forma de que el revisor lea el correo sin publicar
un puerto y sin credenciales. El enlace funciona porque
`PIRRY_LEDGER_PUBLIC_BASE_URL` dice la dirección que el revisor tiene delante.

### 4. El emisor no autentica si no hay credenciales

`MailKitEmailTransporter` hoy siempre llama a `AuthenticateAsync`. Con un
servidor local sin credenciales eso es un intento de autenticación con cadenas
vacías, que puede rechazarse.

Se cambia: si usuario y contraseña están vacíos, no se autentica y se conecta
directo. Es un cambio pequeño y con prueba propia, y sirve también para
cualquier relevo SMTP sin autenticación.

**Por qué no configurar mailpit para exigir una contraseña cualquiera.** Ataría
la aplicación a una configuración de la bandeja; el cambio en el emisor es más
general y no debilita nada: si hay credenciales, sigue autenticando.

### 5. Una cuenta real sigue siendo posible

Las mismas seis variables permiten apuntar a un proveedor real (por ejemplo
Gmail con contraseña de aplicación) sin tocar código: se cambian los valores en
`.env` y se para el servicio `mailer` o se deja, porque el comando es el mismo.
El README documenta el camino real como opción, no como requisito.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| La aplicación escribe el correo pendiente en el log | Mete códigos de activación y de recuperación en los logs, y el "correo" deja de ser correo. La revisión acumulada pide activación por correo. |
| Publicar el puerto de la bandeja | El enunciado dice que solo la aplicación publica un puerto. |
| Una cuenta de Gmail real en `.env.example` | El revisor tendría que traer sus propias credenciales; copiar el archivo no bastaría. |
| Enviar el correo desde dentro de la aplicación | Contradice el ADR 007 y rompe la idempotencia que ya está probada. |
| MailHog | Proyecto menos mantenido que mailpit y con menos soporte de API. |
| Un único contenedor que migre, envíe correo y sirva la API | Mezcla tres responsabilidades en un proceso y hace que un fallo de correo afecte a la API. |
| Un `--send-mail` ejecutado a mano por el revisor | Es un paso manual fuera de la composición: el enunciado lo prohíbe. |

## Consecuencias

**A favor**

- El alta con activación y la recuperación se completan siguiendo solo el README.
- Ningún puerto extra publicado y ninguna credencial real necesaria.
- El ADR 007 se mantiene: el envío sigue siendo un comando en un proceso aparte.
- El mismo código sirve para un proveedor real cambiando seis variables.

**En contra, asumido a conciencia**

- Dos servicios más en la composición (la bandeja y el emisor).
- La bandeja pierde los mensajes al recrearse el contenedor: son de prueba.
- Hay hasta diez segundos de espera entre el registro y el correo.
- El emisor cambia para aceptar credenciales vacías: un caso más que probar.

## Qué queda fuera

- Reintentos con espera creciente y política de descarte de correos fallidos.
- Un proveedor real configurado en la composición.
- Plantillas HTML, adjuntos y varios idiomas.
- Métricas de entrega o panel de correos dentro de la API.
- TLS entre la aplicación y la bandeja: viajan en la red interna de la
  composición.

## Nota de implementación (2026-11-11)

La primera versión del servicio de correo usaba
`command: ["sh", "-c", "while true; ..."]` y **no enviaba nada**. La imagen
termina con `ENTRYPOINT ["dotnet", "PirryLedger.Host.dll"]`, y en Compose
`command` **no reemplaza** al `ENTRYPOINT`: se le agrega como argumentos. El
proceso que arrancaba era la API con `sh`, `-c` y el texto del bucle como
argumentos; no encontraba `--send-mail` y se quedaba sirviendo, con lo que el
correo nunca salía de la cola.

La decisión no cambia: el envío sigue siendo un comando en un proceso aparte. Lo
que cambia es cómo se pide: el bucle va en el **entrypoint** (`/bin/sh -c`) y el
`command` es el script. Por el mismo motivo, `migrate` quedó con
`command: ["--migrate"]` y la API no lleva `command`: los dos confían en el
`ENTRYPOINT` de la imagen. `migrate` funcionaba antes solo porque su `command`
repetía `--migrate` como argumento suelto.

Lo detectó el `docker compose ps`: la columna COMMAND del servicio `mailer`
mostraba `dotnet PirryLedger.…` en vez de `sh -c …`. La bandeja de Mailpit
apareció vacía con el correo pendiente en `not_email_queue`.
