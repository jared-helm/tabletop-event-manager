FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/TabletopEventManager.Api/TabletopEventManager.Api.csproj ./src/TabletopEventManager.Api/
RUN dotnet restore ./src/TabletopEventManager.Api/TabletopEventManager.Api.csproj

COPY scripts ./scripts
COPY src/TabletopEventManager.Api ./src/TabletopEventManager.Api
WORKDIR /src/src/TabletopEventManager.Api
RUN dotnet publish TabletopEventManager.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_EnableDiagnostics=0

RUN apt-get update \
    && apt-get install --no-install-recommends --yes wget \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/data
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "TabletopEventManager.Api.dll"]
