# tabletop-event-manager
A platform to manage tabletop events at your local card shop.

## Quick Start (Docker Compose)

Docker Desktop with Docker Compose is the primary supported way to run this project locally.

From the repository root:

```powershell
docker compose up --build
```

- Frontend: http://localhost:5173
- API: http://localhost:5080
- API health check: http://localhost:5080/health

On first startup, the API automatically creates the SQLite schema and seeds the three supported game templates (Magic: The Gathering, Pokemon TCG, Yu-Gi-Oh TCG) plus three example events. SQLite data is persisted in the `sqlite-data` Docker volume, so events and registrations survive container restarts.

Stop the stack with `Ctrl+C`. To remove the containers and reset the persisted database, run:

```powershell
docker compose down -v
```

## Local Development (without Docker)

Use these steps if you want to run the API and frontend directly on your machine instead of in containers.

### API

Prerequisites: .NET 10 SDK.

```powershell
dotnet restore
dotnet run --project .\src\TabletopEventManager.Api\TabletopEventManager.Api.csproj --launch-profile http
```

The API starts at `http://localhost:5080` and initializes/seeds the SQLite database the same way as the containerized setup.

### Frontend

The React frontend is located in `src/TabletopEventManager.Web` and uses Vite.

```powershell
cd .\src\TabletopEventManager.Web
cmd /c npm install
cmd /c npm run dev
```

The frontend starts at `http://localhost:5173` and proxies `/api` requests to the API at `http://localhost:5080`.

Verify the frontend production build with:

```powershell
cmd /c npm run build
```

## Running Tests

```powershell
dotnet test .\TabletopEventManager.sln
```

This runs both the unit/smoke test project (`TabletopEventManager.Api.Tests`) and the integration test project (`TabletopEventManager.Api.IntegrationTests`). Integration tests spin up the real API pipeline against an isolated temporary SQLite file per test class, so they don't touch your local development database.

Frontend tests:

```powershell
cd .\src\TabletopEventManager.Web
cmd /c npm test
```

## AI Usage Note

This project was built with GitHub Copilot (Claude-based agent mode) for planning documents, schema design, the C# API, the React frontend, and the integration test suite. 

