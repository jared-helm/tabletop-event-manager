# tabletop-event-manager
A platform to manage tabletop events at your local card shop.

## Current Development Startup

The API currently targets .NET 10.

### Prerequisites

- .NET 10 SDK

### Run the API

From the repository root:

```powershell
dotnet restore
dotnet run --project .\src\TabletopEventManager.Api\TabletopEventManager.Api.csproj --launch-profile http
```

The API starts at `http://localhost:5080`.

Verify the service is running:

- Root endpoint: http://localhost:5080/
- Health endpoint: http://localhost:5080/health

The health endpoint returns the service status and a UTC timestamp.

## Frontend Development

The React frontend is located in `src/TabletopEventManager.Web` and uses Vite.

From the repository root:

```powershell
cd .\src\TabletopEventManager.Web
cmd /c npm install
cmd /c npm run dev
```

The frontend starts at `http://localhost:5173` and proxies `/api` requests to the API at `http://localhost:5080`.

The frontend production build can be verified with:

```powershell
cmd /c npm run build
```

The frontend smoke-test setup is included with Vitest and Testing Library.

## Docker Compose

Docker Desktop with Docker Compose is required for the containerized startup.
From the repository root:

```powershell
docker compose up --build
```

The frontend is available at `http://localhost:5173`. The API is available at `http://localhost:5080`, and its health endpoint is `http://localhost:5080/health`.

SQLite data is persisted in the `sqlite-data` Docker volume. Stop the stack with `Ctrl+C`. To remove the containers and persisted local database volume, run:

```powershell
docker compose down -v
```
