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

## Write up

### How did you determine and enforce how many people can attend an event? Where does capacity live, and what happens under concurrent registrations for the last seat?

The admin is able to determine how many people can attend an event when they set the event up. Since 30 attendees was our hard limit the UI doesn't allow values greater than this and that validation is run as a redundant check in the api using a fluent validator. Capacity lives on the event itself since its common to all events. 

In the case of concurrent registrations, only one registration is able to write to the database with the use of event specific semaphores. Use of these semaphores prevents exceeding the max registrations as well as serializing critical checks such duplicate checking and ensuring the event time has not passed. If two players attempt to claim the final seat concurrently, one request completes successfully and commits the registration. The next request then rechecks the updated registration count and receives the “event is full” response. Registrations for different events can proceed concurrently because the semaphore is scoped per event.

If this were a larger system we could look into using a distributed lock to handle these scenarios.

### How does your template system work, and what would adding a 4th game (or a non-card game) require?

The template system has a core entity of `Game` that holds basic details such as the name of the game. Properties specific to each game are seeded into `GameConfigurationOption`. Each game type can map any number of configuration options based on how that game plays. When an actual event is set up the selected values of those options for that event are stored in `EventConfigurationSelection`. For options with an enum of values available to select, we have a separate table called `GameConfigurationOptionValue`. This table contains arrays of possible values for our configuration options. 

Using this model we were able to support game driven event properties such as event duration (default changes based on the game) and event format. 

Adding a 4th game would require a game row, its configuration options, and any allowed option values in the seed data. The front end is designed to load template metadata but additional work would be needed to make the UI fully generic. 

### What did you deliberately cut or fake to stay in the timebox, and what would you build next?

I deliberately did not build out a system for adding or removing properties for a game type or any kind of detail screen showing the configuration for each game. All configuration is pre-seeded with the files in the scripts folder. 

As a note, I also removed the location field from the event form and hard coded the .ics file location to be "Jareds card shop". Since the prompt was framed as events at a local store it didn't make sense to specify the location on the creation of every event.

Full scope cuts are detailed in the [software design doc](https://github.com/jared-helm/tabletop-event-manager/blob/main/docs/software-design.md#11-scope-and-deliberate-cuts).

There's a lot of things that could be added to this project but the first choice would probably be accounts and authentication. This would add security and allow us to enforce authorization rules. It would also allow users registering for events to save their information so registration could be super straightforward. This would also allow us to link event results to an account so that participants could see their progress tracked over time at various events. 

After adding accounts, creating screens for managing game types would be a huge plus. Being able to add a new game type easily would allow a store manager to easily adapt to new popular games. 

### AI usage note (a few sentences): which tools you used and for what, and one example of AI output you rejected or had to fix.

I used github copilot inside of vs code for designing and implementing this project. I used chatgpt occasionally for quick questions to save on token cost. I started by having copilot scan the requirements document to general functional and non-functional requirements and worked with AI to edit them. 

One example of AI output i had to fix was the organization of the api project. It put all of the routes in the program.cs file and I had to prompt it to create controllers and service methods as well as instructing it to pull business logic out of the repository layer. I had to make similar adjustments to the front end to get it to organize the files into components and pages. 

## AI Usage Note

This project was built with GitHub Copilot (Claude-based agent mode) for planning documents, schema design, the C# API, the React frontend, and the integration test suite. 

