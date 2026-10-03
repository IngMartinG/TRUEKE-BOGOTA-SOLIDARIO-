# syntax=docker/dockerfile:1
# Imagen de la API Trueke Bogotá Solidario. Construir desde la carpeta backend/:
#   docker build -t trueke-api .

# ---------- 1) Compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Primero solo los .csproj: la restauración queda en caché mientras no cambien las dependencias
COPY nuget.config global.json ./
COPY TruekeBogotaSolidario.Datos/TruekeBogotaSolidario.Datos.csproj TruekeBogotaSolidario.Datos/
COPY TruekeBogotaSolidario.Negocio/TruekeBogotaSolidario.Negocio.csproj TruekeBogotaSolidario.Negocio/
COPY TruekeBogotaSolidario.Presentacion/TruekeBogotaSolidario.Presentacion.csproj TruekeBogotaSolidario.Presentacion/
RUN dotnet restore TruekeBogotaSolidario.Presentacion/TruekeBogotaSolidario.Presentacion.csproj

COPY TruekeBogotaSolidario.Datos/ TruekeBogotaSolidario.Datos/
COPY TruekeBogotaSolidario.Negocio/ TruekeBogotaSolidario.Negocio/
COPY TruekeBogotaSolidario.Presentacion/ TruekeBogotaSolidario.Presentacion/
RUN dotnet publish TruekeBogotaSolidario.Presentacion/TruekeBogotaSolidario.Presentacion.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false /p:DebugType=None /p:DebugSymbols=false

# ---------- 2) Ejecución (solo runtime, sin SDK ni código fuente) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

COPY --from=build --chown=root:root /app/publish .

# Usuario sin privilegios incluido en la imagen oficial (UID 1654). Los archivos son de root: la app no puede modificarlos.
USER $APP_UID
EXPOSE 8080

# Sin HEALTHCHECK de Docker a propósito: la imagen no incluye curl/wget (menos superficie de ataque).
# Azure App Service usa su propio "Health check" contra /health/ready; docker-compose usa depends_on.
ENTRYPOINT ["dotnet", "TruekeBogotaSolidario.Presentacion.dll"]
