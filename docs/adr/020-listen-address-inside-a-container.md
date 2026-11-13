# ADR 020 - Listen address inside a container

- **Estado:** aceptada
- **Fecha:** 2026-11-10
- **Requisitos:** RD-08, RD-10; sección 1.2 del enunciado de la Práctica 4 (puerto publicado, dirección documentada)
- **Decide:** Franklin. **Redacta:** el agente.
- **Modifica:** ADR 005 (solo la dirección de escucha; la fuente única del puerto y de los enlaces no cambia)

## Enmienda autorizada por Franklin (2026-11-12)

Kestrel escucha por HTTP en `0.0.0.0`, en el puerto obligatorio `PORT`.
`PIRRY_LEDGER_PUBLIC_BASE_URL` admite HTTP y HTTPS y se usa para generar
los enlaces de activación. Se eligió exigir el puerto en cada entorno para
evitar que un cambio en el enlace altere la escucha del servidor.

Compose publica el puerto 8090 y reenvía al puerto configurado del contenedor.
En Render, el proxy termina TLS y reenvía por HTTP a Kestrel. La configuración
propuesta usa `PORT=8090` en ambos entornos y el dominio HTTPS en Render.

La decisión original se conserva a continuación como registro histórico.

## Contexto

El ADR 005 dejó `PIRRY_LEDGER_PUBLIC_BASE_URL` como **única fuente**: de ella
salen el host y el puerto donde escucha Kestrel y la dirección con la que se
arman los enlaces de activación. En su tabla de alternativas rechazó
explícitamente "escuchar en todos los interfaces (`+:{puerto}`)" con este
argumento: abriría el puerto en todas las interfaces aunque la variable dijera
`localhost`, y los enlaces seguirían diciendo `localhost`, que no sirve desde la
tablet.

Ese razonamiento era correcto para un proceso que corre directamente en la
máquina. Con la Práctica 4 el proyecto corre en un contenedor y la situación
cambia: el puerto publicado por Docker (`8090:8090`) llega a la interfaz de red
del contenedor, **no** a su `localhost`. Un proceso atado a `127.0.0.1` dentro
del contenedor no recibe nada, aunque el puerto esté publicado. El revisor vería
el contenedor en marcha y la dirección del README sin respuesta.

Además, si la variable dice `http://localhost:8090` y el proceso tiene que atarse
a otra cosa, aparece una segunda configuración de escucha: justo lo que el ADR
005 eliminó.

## Decisión

1. `PIRRY_LEDGER_PUBLIC_BASE_URL` sigue siendo la **única fuente** de la
   dirección pública y del puerto. Los enlaces de activación se arman con ella.
2. El proceso escucha **siempre en `http://0.0.0.0:{puerto}`**, donde el puerto
   se toma de esa misma variable. No hay una segunda variable de escucha.
3. El **puerto del host es el mismo** que el del contenedor (`8090`), así que la
   variable no necesita traducción: `http://localhost:8090` es la dirección que
   el revisor abre y el puerto que el proceso escucha.
4. Solo se admite `http`. Si la variable trae `https`, la aplicación se detiene
   con un mensaje que nombra la variable: hoy no hay certificado ni TLS
   configurado, y aceptarlo en silencio produciría un arranque que no responde.
5. El puerto publicado en la composición es `8090:8090` y es el único publicado.

**Qué cambia respecto del ADR 005.** No cambia la fuente del puerto ni la de los
enlaces; cambia el **host de escucha**, que deja de copiar el host de la
variable. La única divergencia que queda —la variable dice `localhost` y el
proceso escucha en todas las interfaces— es intencional y es la que permite que
un puerto publicado funcione.

El efecto secundario se asume: en desarrollo, sin contenedor, la API también
escucha en todas las interfaces de la máquina. Para el caso de uso del proyecto
—una tablet abriendo la aplicación por la red local— eso es una ventaja, no un
problema.

## Alternativas consideradas

| Alternativa | Motivo para descartarla |
|---|---|
| Mantener el host de la variable como host de escucha | En un contenedor, `localhost` ata el proceso a su propia loopback y el puerto publicado no llega. La aplicación no responde en la dirección del README. |
| Una segunda variable solo para la dirección de escucha | Vuelve al problema que el ADR 005 eliminó: dos valores que pueden desincronizarse y nadie lo nota. |
| `network_mode: host` en la composición | No funciona igual en Docker Desktop ni en Codespaces, y pierde el aislamiento de puertos que el enunciado pide. |
| `ASPNETCORE_URLS` en la composición | `UseUrls` gana sobre ella (verificado en el ADR 005): sería una segunda fuente que no manda. |
| Un proxy inverso dentro de la composición | Un servicio más para un problema que resuelve una sola línea de escucha. |
| Atar `localhost` fuera del contenedor y `0.0.0.0` dentro, con una variable de entorno | Dos comportamientos que mantener y dos formas de fallar; la tablet deja de poder conectarse en desarrollo. |
| Aceptar `https` en la variable y atar en ese esquema | Sin certificado, Kestrel no arranca, y el error aparecería lejos de su causa. |

## Consecuencias

**A favor**

- `docker compose up --build` responde en `http://localhost:8090`, que es
  exactamente lo que dice el README.
- Los enlaces de activación siguen saliendo de una sola variable y funcionan en
  el navegador del revisor.
- No aparece una segunda variable de escucha que pueda desincronizarse.

**En contra, asumido a conciencia**

- En desarrollo local la API queda accesible desde la red de la máquina, no solo
  desde `localhost`.
- El host de la variable ya no coincide literalmente con el host de escucha; la
  coincidencia que importa —el puerto— se mantiene.
- HTTPS sigue fuera: si algún día se necesita, su ADR tendrá que decidir
  certificado, puerto y si la variable puede decir `https`.

## Verificación

Pendiente en el Codespace, porque esta máquina no tiene Docker:

- `docker compose up --build` → la aplicación responde en `http://localhost:8090`.
- `docker compose exec app dotnet --info` o el log de arranque muestran el
  puerto escuchado.
- Con el contenedor parado, la misma variable en `dotnet run` sigue escuchando
  el mismo puerto.

## Qué queda fuera

- TLS, certificados y redirección de HTTP a HTTPS.
- Proxy inverso, CORS y reglas de firewall.
- Varias instancias detrás de un balanceador.
