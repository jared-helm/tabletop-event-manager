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

## Design Write-Up

### Capacity: where it lives and how concurrency is handled

Capacity is a first-class column on the `EVENT` table (`capacity`, checked to be between 0 and 30) and is further constrained at creation time to each game template's configured minimum/maximum player counts. There is no separate "spots remaining" table — availability is always computed as `capacity - COUNT(EVENT_REGISTRATION rows)`.

Enforcement happens entirely on the server, inside `EventRepository.RegisterPlayerAsync`. Each event has its own in-process lock (a `SemaphoreSlim` keyed by event ID). A registration request:

1. Acquires that event's lock.
2. Re-reads the event's current state and registration count inside the lock.
3. Rejects if the event is deleted, has already started, is a duplicate registrant, or is full.
4. Otherwise inserts the registration and releases the lock.

Because the count check and the insert happen under the same lock (not just the same DB transaction), two concurrent requests for the last seat cannot both pass the capacity check before either has written its row. This was verified directly: firing 10 simultaneous registration requests at a capacity-1 event resulted in exactly one `201 Created` and nine `409 Conflict` "This event is full." responses.

This in-process lock is a deliberate, documented simplification for a single API instance. If the API were ever horizontally scaled to multiple instances, this would need to move to a database-level lock (e.g., a `SELECT ... FOR UPDATE`-style pattern, or a serializable transaction with retry) or an external distributed lock, since separate processes don't share the in-memory semaphore dictionary.

### Template system: how it works and what a 4th game requires

Game behavior is entirely data-driven through three tables: `GAME`, `GAME_CONFIGURATION_OPTION`, and `GAME_CONFIGURATION_OPTION_VALUE`. An option describes a key, label, data type (`STRING`/`NUMBER`/`BOOLEAN`/`ENUM`), UI control (`TEXT`/`NUMBER`/`TOGGLE`/`SELECT`/`CHECKBOX_GROUP`), a default value, and whether it's required. Enum options have their allowed values in a child table.

`EventRepository` never branches on a game's name or code anywhere in event creation, validation, or registration — it only ever queries `GAME_CONFIGURATION_OPTION` rows by `game_id`. The event-format validity check, the tournament-format check, and the min/max player enforcement are all generic lookups against these template rows, not per-game conditionals.

Adding a 4th game (including a non-card game) requires only:

1. Insert a row into `GAME`.
2. Insert its `GAME_CONFIGURATION_OPTION` rows (at minimum `event_format`, `allowed_play_types`, `default_duration_minutes`, `minimum_players`, `maximum_players`).
3. Insert the corresponding `GAME_CONFIGURATION_OPTION_VALUE` rows for any enum options.

No changes to `EventRepository.cs`, `Program.cs`, or the frontend are required — the create-event form and validation already render and enforce whatever options exist for the selected game. [scripts/seed-game-templates.sql](scripts/seed-game-templates.sql) is the existing example to copy from.

### What was cut or faked, and what's next

Deliberately out of scope for this timebox (documented earlier in [docs/functional-requirements.md](docs/functional-requirements.md) and [docs/tasks.md](docs/tasks.md)):

- Editing or cancelling events after creation — event properties are read-only once created, and deletion is soft (the row and its registrations are retained).
- Tournament execution (pairings, standings, rounds, brackets, match results) — only the tournament *format* is recorded.
- Waitlists, authentication, and player accounts.
- A fixed store location is assumed rather than collected per event; the `location` column exists end-to-end (schema, API, ICS) but the create-event form doesn't currently expose it.

Known limitations:

- The per-event registration lock is in-process only (see above); it does not extend across multiple API instances.
- Duplicate-name matching uses SQLite's `lower()`, which is ASCII-only, so non-ASCII case folding (e.g., accented characters) isn't fully normalized.
- The transitive `SQLitePCLRaw.lib.e_sqlite3` package has a known NuGet advisory (`GHSA-2m69-gcr7-jv3q`); it wasn't upgraded/patched within the timebox.

What I'd build next: waitlists, an audited event-edit flow with a warning when registrants already exist, a distributed lock for multi-instance capacity enforcement, and stronger duplicate-registration matching (e.g., normalized/Unicode-aware comparison).

## AI Usage Note

This project was built with GitHub Copilot (Claude-based agent mode) for planning documents, schema design, the C# API, the React frontend, and the integration test suite. One example of AI output that had to be fixed: an early version of `scripts/seed-game-templates.sql` used `values` as a derived-table alias, which is a reserved SQL keyword in SQLite and caused the seed script to fail with a parse error; it was caught by directly running the script against a temporary SQLite schema and fixed by renaming the alias to `seed_value`.

