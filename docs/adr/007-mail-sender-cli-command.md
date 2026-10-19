# ADR 007 - Mail sender: CLI command `--send-mail` in the Host

- **Estado:** aceptada
- **Fecha:** 2026-10-04
- **Requisitos:** RF-NOT-08, RF-NOT-09, RF-NOT-12; RD-08, RD-09, RD-10
- **Decide:** Franklin. **Redacta:** el agente.

## Contexto

El correo saliente (activación de cuenta, recuperación de contraseña,
restablecimiento forzado) debe enviarse **fuera** de la operación HTTP que
lo encola: una operación que registra un usuario no debe abrir una
conexión SMTP ni bloquear su respuesta esperando el resultado del envío
(RF-NOT-09). Además, un correo ya enviado no debe reenviarse aunque se
vuelva a ejecutar el enviador (RF-NOT-12). El sistema tiene un solo
despliegue, modular, sin microservicios. La pregunta es **cómo** se hace
llegar el correo a Internet sin convertir al Host en un servidor de fondo
que vigile la cola.

`current-iteration.md` reserva para la semana 11 los reintentos, el
estado fallido, el último error y la vista administrativa de la cola.
Aquí se decide solo la **forma** del envío: la lógica de reintentos vive
en el caso de uso `ProcesarColaDeCorreo` y se documenta aparte.

## Decisión

El envío de correo es un **comando de línea** del Host, no un servicio
hospedado ni un proceso separado:

1. El Host expone un único binario (`PirryLedger.Host`). Sus dos modos
   son:
   - **API HTTP:** sin argumentos especiales (o sin `--send-mail`); el
     proceso se queda escuchando y responde a las rutas de AccessControl
     y Notifications.
   - **Enviador:** cuando uno de los argumentos es exactamente
     `--send-mail` (comparación insensible a mayúsculas), el proceso
     resuelve `ProcesarColaDeCorreo` del contenedor de DI, ejecuta un
     solo ciclo, imprime cuatro líneas de resumen y termina con código
     de salida `0`.
2. La cola (`not_correos_en_cola`) es la única fuente de verdad sobre
   correos por enviar, en proceso y enviados. Tanto el comando del Host como
   cualquier futura pieza administrativa la leen de ahí; nadie la
   escribe en paralelo.
3. El comando es **idempotente sobre correos enviados**: el caso de uso
   toma los correos en estado `Pendiente`, los marca `Procesando` y los
   marca `Enviado` cuando el SMTP responde bien. Una segunda ejecución
   ve la cola ya vacía de pendientes y termina sin enviar nada.
4. La invocación documentada es:

   ```powershell
   dotnet run --project Src/Host/PirryLedger.Host -- --send-mail
   ```

5. La configuración SMTP (`PIRRY_LEDGER_SMTP_*`) se lee del mismo modo en
   los dos modos. Sin SMTP configurado, el caso de uso devuelve los
   correos a `Pendiente` con `intentos += 1` (RF-NOT-13); el comando
   imprime `Correos fallidos: N` y termina `0`. La operación HTTP nunca
   falla por una configuración SMTP ausente.

Sin paquetes nuevos: `Microsoft.Extensions.Hosting` viene en el SDK Web
y ya se usa para el Host. El reparto entre "API" y "comando" se hace con
un `if` sobre `args` antes de `app.Run()`.

### Por qué no un `BackgroundService` / `IHostedService` en el mismo proceso

Se descartó por tres razones concretas:

- **Acoplamiento al ciclo de vida del API.** Un servicio hospedado arranca
  con `app.Run()` y muere con él. En un despliegue donde el Host se
  reinicia por una actualización, el enviador se reinicia con él y
  cualquier correo en estado `Procesando` queda huérfano. Con el
  comando, el operador decide cuándo se ejecuta y cuándo no.
- **Concurrencia con el caso de uso.** Un servicio hospedado en el mismo
  proceso compite por el `DbContext` con las operaciones HTTP. La cola
  ya tiene el reclamo atómico (`UPDATE ... FOR UPDATE SKIP LOCKED`)
  necesario para que dos procesos no envíen el mismo correo, y eso se
  aprovecha por construcción. Con un servicio hospedado dentro del
  proceso, el problema no aparece, pero también se pierde la opción de
  ejecutar el envío en otra máquina (otra sucursal, un cron externo)
  sin cambiar código.
- **Distinguir "no se ejecuta" de "no hay correos".** Con un
  `BackgroundService` el operador nunca sabe si el envío está corriendo
  o no, y la bitácora operacional queda vacía. Con el comando, la salida
  es explícita: `Correos tomados: N`, `Correos enviados: M`,
  `Correos fallidos: K`, más una línea final que dice qué pasó con la
  cola. La salida se puede redirigir a un archivo o a un pipe, igual que
  cualquier comando de la terminal.

### Por qué no un ejecutable separado

Se descartó por una razón concreta: obligaría un segundo proyecto, una
segunda configuración de DI y un segundo punto de versionado, para
ejecutar las mismas cuatro líneas de resumen sobre el mismo caso de uso.
El comando `--send-mail` reusa el binario que ya está en producción; el
cliente SMTP, las variables de entorno, el `IClock` y la cola son los
mismas. La pieza separada aporta cero funcionalidad y duplica el coste
de despliegue.

## Consecuencias

**A favor**

- El envío corre **fuera** de la operación HTTP que encola el correo:
  registrar un usuario nunca espera al SMTP (RF-NOT-09).
- Reenviar el comando es seguro: la cola ya no tiene nada pendiente o
  bien el caso de uso devuelve los fallidos a `Pendiente` con
  `intentos += 1` (RF-NOT-12, RF-NOT-13).
- La salida es texto plano: cabe en un pipe, en un archivo de log o en
  un monitor sin parsear JSON.
- Operacionalmente, un reinicio del Host no se lleva por delante un
  envío en curso.

**En contra, asumido a conciencia**

- El envío no es continuo: alguien o algo tiene que lanzar el comando.
  Para Práctica 1 eso es una persona (`dotnet run ... -- --send-mail`
  desde una terminal). En el futuro se podrá envolver en un cron o en
  un programador de tareas de Windows sin tocar el caso de uso.
- El comando no aplica límites de tiempo ni reintentos automáticos: si
  el SMTP está caído, los correos vuelven a `Pendiente` con
  `intentos += 1` y se vuelven a intentar al próximo lanzamiento. La
  pieza de reintentos y de último error queda para la semana 11.
- La salida se imprime en `Console.Out` en el idioma del operador
  (español hoy). Quien quiera consumirlas desde otra herramienta tendrá
  que parsear las líneas por su cuenta o cambiar la salida.

## Limpieza

Las filas `Enviado` con más de N días pueden purgarse cuando la cola
crezca; la columna `enviado_en_utc` ya tiene un índice en la migración
de creación, así que la limpieza es una consulta pequeña. La política de
retención se decide en la semana 11 con el resto de la pieza.