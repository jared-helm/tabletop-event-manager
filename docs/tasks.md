# Build Tasks

This checklist turns [the software design](software-design.md) into an implementation sequence. Tasks should be completed in order unless a task is explicitly marked as parallelizable.

## Phase 1: Project Foundation

### API Boilerplate

- [x] Create the C# API project and solution structure.
- [x] Add configuration for SQLite and the database connection.
- [x] Add health-check or root endpoint for local verification.
- [x] Configure JSON serialization for ISO 8601 UTC timestamps.
- [x] Configure CORS for the React development origin.
- [x] Add API-level error handling and consistent validation-error responses.
- [x] Add a basic test project and one smoke test.

**Acceptance:** The API builds, starts locally, responds to a health request, and has no dependency on a hosted service.

### Frontend Boilerplate

- [x] Create the React application and source structure.
- [x] Add client-side routing for the calendar and registration page.
- [x] Add an API client with a configurable API base URL.
- [x] Add shared loading, error, modal, tab, and form primitives.
- [x] Add date/time helpers that convert API UTC timestamps to browser-local display values.
- [x] Add a basic frontend test setup and one render smoke test.

**Acceptance:** The frontend builds, starts, can call the API, and renders a placeholder calendar route.

### Docker Compose

- [x] Add `docker-compose.yml` for the API, frontend, and persistent SQLite storage.
- [x] Add Dockerfiles or equivalent build configuration for the API and frontend.
- [x] Configure service-to-service API URL wiring.
- [x] Configure a persistent database volume.
- [x] Add health checks or startup ordering where needed.
- [ ] Verify the primary local startup path is `docker compose up`.

**Acceptance:** A clean checkout can start the stack with one primary command and reach both frontend and API endpoints.

## Phase 2: SQLite Schema and Seed Data

### Schema

- [x] Create the SQLite schema for `GAME`.
- [x] Create the SQLite schema for `EVENT`.
- [x] Create the SQLite schema for `GAME_CONFIGURATION_OPTION`.
- [x] Create the SQLite schema for `GAME_CONFIGURATION_OPTION_VALUE`.
- [x] Create the SQLite schema for `EVENT_CONFIGURATION_SELECTION`.
- [x] Create the SQLite schema for `EVENT_REGISTRATION`.
- [x] Use `_utc` suffixes for all persisted timestamp fields.
- [x] Add foreign keys and required-field constraints.
- [x] Add uniqueness for game codes and `(game_id, key)` options.
- [x] Add uniqueness for `(option_id, value)` values.
- [x] Add uniqueness for `(event_id, option_id, selected_value)` selections.
- [x] Add `deleted_at_utc` as nullable soft-delete state.
- [x] Add indexes for active calendar events, event registrations, and registration duplicate lookups.
- [x] Add migration or initialization behavior that creates the schema on startup.

**Acceptance:** A fresh SQLite database can be initialized without manual table creation, and the schema matches [the ER diagram](er-diagram.md).

### Seed Templates

- [x] Run the existing `scripts/seed-game-templates.sql` as part of database initialization.
- [x] Verify seeded games: MTG, Pokemon TCG, and Yu-Gi-Oh TCG.
- [x] Verify seeded play types, formats, tournament formats, durations, and player constraints.
- [x] Make seeding repeatable without duplicate rows.
- [x] Confirm no event rows are inserted by the seed process.

**Acceptance:** A fresh database has the three game templates and no events or registrations.

## Phase 3: Calendar Page

### Calendar API

- [x] Implement `GET /api/events?startUtc=...&endUtc=...`.
- [x] Return only non-deleted events within the requested UTC range.
- [x] Return UTC start time, duration, calculated end time, name, game, capacity, and registration count.
- [x] Validate the range query parameters, requiring `startUtc` before `endUtc`.

### Calendar UI

- [x] Implement the month-grid calendar as the main route.
- [x] Add previous-month, next-month, and Today controls.
- [x] Render every event in its matching local calendar day cell.
- [x] Show event name and local start time in each event summary.
- [x] Support dense day cells without hiding events behind a `+N more` control.
- [x] Add the Create Event button above the calendar on the top-right.
- [x] Refresh the calendar after event creation and soft deletion.
- [x] Add loading, empty, and API-error states.

**Acceptance:** A month view loads from the API, shows all returned events on the correct browser-local dates, and has no pre-seeded events.

## Phase 4: Event Creation

### Template API

- [x] Implement `GET /api/games` for active seeded games.
- [x] Implement `GET /api/games/{gameId}/configuration`.
- [x] Return active options, values, controls, defaults, ordering, and required flags.
- [x] Keep template behavior data-driven rather than branching on game names.

### Create Modal and API

- [x] Implement the create-event modal with X-button close behavior and a non-dismissible backdrop.
- [x] Add accessible focus handling and keyboard behavior.
- [x] Render event fields and dynamic template controls.
- [x] Support `SELECT` and `CHECKBOX_GROUP` controls.
- [x] Show tournament format only for Tournament play type.
- [x] Convert browser-local date/time input to UTC before submission.
- [x] Add client-side validation for required fields, text constraints, dates, and capacity.
- [x] Implement `POST /api/events` with authoritative server-side validation.
- [x] Snapshot selected configuration and template duration onto the event.
- [x] Generate a unique registration slug.
- [x] Calculate and return the event end time.
- [x] Close the modal and refresh the calendar after success.
- [x] Preserve form values and display errors after failure.

**Acceptance:** An organizer can create an event for each seeded game, and the new event appears in the correct local calendar day without a full page reload.

## Phase 5: Event Details and Deletion

### Event Details Modal

- [ ] Implement `GET /api/events/{eventId}`.
- [ ] Open the event modal by clicking a calendar event.
- [ ] Add Event Details, Players, and Registration Resources tabs.
- [ ] Keep all event properties read-only after creation.
- [ ] Display calculated local start and end times.
- [ ] Add accessible tab and modal keyboard behavior.

### Soft Deletion

- [ ] Implement `DELETE /api/events/{eventId}` as a soft delete.
- [ ] Require a confirmation step in the UI.
- [ ] Allow deletion even when registrations exist.
- [ ] Set `deleted_at_utc` and retain registrations/configuration selections.
- [ ] Remove the event from active calendar results after deletion.
- [ ] Return an unavailable-event response for deleted registration pages/resources.
- [ ] Leave the data model compatible with future undeletion.

**Acceptance:** Deleting an event removes it from active views but preserves its database row and registrations.

## Phase 6: Registration Resources

- [ ] Implement `GET /api/events/{eventId}/registration-resources`.
- [ ] Display the clickable registration URL.
- [ ] Add a copy-to-clipboard action and feedback state.
- [ ] Generate a QR code with a maintained library.
- [ ] Ensure the QR code encodes the exact registration URL.
- [ ] Generate a downloadable ICS file with a maintained library.
- [ ] Use event start plus stored duration for the ICS end time.
- [ ] Include title, localizable start/end time, and location in the ICS file.
- [ ] Handle deleted or unknown events with an unavailable state.

**Acceptance:** The Registration Resources tab provides a working link, copy action, QR code, and importable ICS download for an active event.

## Phase 7: Players Tab and Registration

### Players Tab API and UI

- [ ] Implement `GET /api/events/{eventId}/registrations`.
- [ ] Return total count and player rows.
- [ ] Display total player count above the table.
- [ ] Display first name, last name, and optional player tag only.
- [ ] Add loading and empty states.
- [ ] Refresh the tab when the modal opens and after registration completes.

### Public Registration Page

- [ ] Implement `GET /api/registration/{slug}`.
- [ ] Implement `POST /api/registration/{slug}`.
- [ ] Add the registration route reachable from the slug.
- [ ] Display event name, game, local start/end time, and location.
- [ ] Collect required first name and last name separately.
- [ ] Collect optional player tag.
- [ ] Add client-side input validation and recoverable error handling.
- [ ] Show a success state only after the API confirms persistence.
- [ ] Show clear errors for full, started, deleted, unknown, invalid, and unavailable events.

### Registration Correctness

- [ ] Normalize names and player tags by trimming surrounding whitespace.
- [ ] Compare duplicate values case-insensitively.
- [ ] Treat blank player tags as absent.
- [ ] Reject matching normalized first/last name pairs.
- [ ] Reject matching non-empty normalized player tags.
- [ ] Return `Someone with that registration info has already registered.` for duplicates.
- [ ] Reject registrations at or after `start_at_utc`.
- [ ] Add a per-event lock around cutoff, duplicate, capacity, and insert operations.
- [ ] Count registrations and enforce capacity while the lock is held.
- [ ] Commit the registration before releasing the lock.
- [ ] Verify capacity-one behavior with concurrent requests.

**Acceptance:** A player can register once for an active event, receives a success response, appears in the Players tab, and cannot bypass cutoff, duplicate, or capacity rules.

## Phase 8: Hardening and Documentation

- [ ] Add API tests for template loading, event creation, UTC conversion, soft deletion, and validation.
- [ ] Add registration tests for cutoff, duplicates, capacity, and concurrent last-seat requests.
- [ ] Add frontend tests for calendar placement, modal behavior, dynamic controls, and registration states.
- [ ] Verify QR content and ICS fields against an active event.
- [ ] Verify browser-local rendering in at least two time zones or with mocked time-zone settings.
- [ ] Add README setup instructions centered on `docker compose up`.
- [ ] Add the required design write-up and AI usage note to `README.md`.
- [ ] Document deliberate cuts and any known limitations honestly.
- [ ] Run the full build and test suite from a clean database.

**Acceptance:** The complete create -> calendar -> share -> register flow works locally, and the README accurately describes setup, design decisions, scope cuts, and verification.
