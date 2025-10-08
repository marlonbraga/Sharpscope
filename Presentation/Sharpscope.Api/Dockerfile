# Etapa base (runtime) — ASP.NET 9.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# Precisamos ser root para instalar pacotes
USER root

# Instala git e certificados (para HTTPS do GitHub)
RUN apt-get update \
 && apt-get install -y --no-install-recommends git ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# Se o VS/compose estiver setando APP_UID, voltamos pra esse usuário.
# Caso APP_UID não seja fornecido em dev, caímos num valor padrão (1000).
ARG APP_UID=1000
# Opcional: garantir que o usuário exista (ignora erro se já existir)
RUN id -u ${APP_UID} >/dev/null 2>&1 || useradd -m -u ${APP_UID} appuser

USER ${APP_UID}

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Etapa de build (SDK 9.0)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Presentation/Sharpscope.Api/Sharpscope.Api.csproj", "Presentation/Sharpscope.Api/"]
# (adicione aqui os demais .csproj das libs referenciadas, se necessário)
RUN dotnet restore "./Presentation/Sharpscope.Api/Sharpscope.Api.csproj"

COPY . .
WORKDIR "/src/Presentation/Sharpscope.Api"
RUN dotnet build "./Sharpscope.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publicação
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Sharpscope.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final (runtime com git já instalado)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sharpscope.Api.dll"]
