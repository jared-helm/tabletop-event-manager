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

The health endpoint returns the service status and a UTC timestamp. Docker Compose startup instructions will be added when the frontend and container setup are implemented.
