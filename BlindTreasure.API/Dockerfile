# BASE image cho runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# BUILD stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy từng csproj để tận dụng cache restore
COPY BlindTreasure.API/BlindTreasure.API.csproj BlindTreasure.API/
COPY BlindTreasure.Application/BlindTreasure.Application.csproj BlindTreasure.Application/
COPY BlindTreasure.Infrastructure/BlindTreasure.Infrastructure.csproj BlindTreasure.Infrastructure/
COPY BlindTreasure.Domain/BlindTreasure.Domain.csproj BlindTreasure.Domain/

RUN dotnet restore BlindTreasure.API/BlindTreasure.API.csproj

# Sau restore xong mới copy toàn bộ source code
COPY . .

WORKDIR /src/BlindTreasure.API
RUN dotnet build BlindTreasure.API.csproj -c $BUILD_CONFIGURATION -o /app/build

# PUBLISH stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish BlindTreasure.API.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# FINAL image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlindTreasure.API.dll"]
