# Imagen de la API de Pirry Ledger: un solo despliegue (monolito modular, RD-03).
#
# Etapa 1 compila y publica con el SDK; etapa 2 solo ejecuta el runtime, asi que
# la imagen final no lleva compilador ni codigo fuente.
#
# La configuracion NO viaja en la imagen (RD-10): la cadena de conexion, las seis
# SMTP, las tres del primer Administrator y PIRRY_LEDGER_PUBLIC_BASE_URL llegan
# como variables de entorno al ejecutar el contenedor. El archivo .env no se
# copia: esta en .dockerignore.

# ---- Etapa 1: compilar y publicar ------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# global.json fija el SDK 10.0.4xx con rollForward latestFeature. Se copia antes
# del restore para que la imagen lo respete: si la imagen trae una banda anterior
# de SDK, el build se detiene aqui con el mensaje de global.json.
COPY global.json ./
COPY Src/ ./Src/

# Se publica el Host y solo el Host: es el unico ejecutable. Las pruebas quedan
# fuera (las excluye .dockerignore) y no hacen falta para desplegar.
RUN dotnet publish Src/Host/PirryLedger.Host/PirryLedger.Host.csproj \
    --configuration Release \
    --output /app/publish \
    -p:UseAppHost=false

# ---- Etapa 2: ejecutar -----------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Production por defecto (el SDK tambien lo hace, pero explicito no hereda un
# ASPNETCORE_ENVIRONMENT que venga de fuera y evita publicar /openapi, que solo
# existe en Development).
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

# El puerto de escucha NO se fija aqui: sale de PIRRY_LEDGER_PUBLIC_BASE_URL, que
# es la unica fuente de verdad del host y puerto de Kestrel y de los enlaces de
# activacion (docs/adr/005-listen-address-from-public-base-url.md). UseUrls gana
# sobre ASPNETCORE_URLS, asi que definir esta ultima no cambia nada. EXPOSE solo
# documenta el puerto por defecto; el mapeo real se hace al ejecutar.
EXPOSE 8080

# Usuario sin privilegios que ya trae la imagen (APP_UID). La aplicacion no
# escribe en disco, asi que con permiso de lectura sobre /app alcanza.
USER $APP_UID

ENTRYPOINT ["dotnet", "PirryLedger.Host.dll"]
