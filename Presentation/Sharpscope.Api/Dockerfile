# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Presentation/Sharpscope.Api/Sharpscope.Api.csproj", "Presentation/Sharpscope.Api/"]
COPY ["Application/Sharpscope.Application/Sharpscope.Application.csproj", "Application/Sharpscope.Application/"]
COPY ["Domain/Sharpscope.Domain/Sharpscope.Domain.csproj", "Domain/Sharpscope.Domain/"]
COPY ["Infrastructure/Sharpscope.Adapters.csharp/Sharpscope.Adapters.Csharp.csproj", "Infrastructure/Sharpscope.Adapters.csharp/"]
COPY ["Infrastructure/Sharpscope.Infrastructure/Sharpscope.Infrastructure.csproj", "Infrastructure/Sharpscope.Infrastructure/"]
RUN dotnet restore "./Presentation/Sharpscope.Api/Sharpscope.Api.csproj"
COPY . .
WORKDIR "/src/Presentation/Sharpscope.Api"
RUN dotnet build "./Sharpscope.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Sharpscope.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Sharpscope.Api.dll"]