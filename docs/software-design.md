# Software Design

## 1. Purpose

The tabletop event manager lets one organizer create and inspect in-store events for Magic: The Gathering, Pokemon TCG, and Yu-Gi-Oh TCG. Players use an event-specific link or QR code to register.

The design prioritizes a complete create -> calendar -> share -> register flow within the three-hour take-home timebox. Tournament management is deliberately excluded: the application records the selected tournament format but does not run pairings, standings, rounds, brackets, or match results.

## 2. Technology and Runtime

- **Frontend:** React single-page application.
- **Backend:** C# API responsible for validation, persistence, capacity enforcement, duplicate detection, QR/ICS generation, and soft deletion.
- **Database:** SQLite with UTC timestamps.
- **Local runtime:** Docker Compose, with startup requiring one or two commands and `docker compose up` as the primary path.
- **QR codes:** Use a maintained QR-code library.
- **Calendar invites:** Use a maintained iCalendar/ICS library.

There is no authentication or authorization in v1. The application assumes one implicit organizer, and players are anonymous until they submit a registration.

## 3. Architectural Boundaries

```text
React UI
  |
  | HTTP/JSON
  v
C# API
  |-- Event service
  |-- Template service
  |-- Registration service
  |-- QR/ICS resource service
  v
SQLite
```

### Frontend responsibilities

- Render the month-grid calendar.
- Open create and event-detail modals.
- Render game configuration from API-provided option metadata.
- Perform immediate client-side validation and display server errors.
- Convert UTC timestamps from API responses to the browser user's local time zone.
- Refresh calendar and player data after successful operations.

### API responsibilities

- Validate every input independently of the client.
- Return active seeded games and configuration options.
- Create events and snapshot selected configuration and duration values.
- Return non-deleted calendar events and event details.
- Soft-delete events by setting `deleted_at_utc`.
- Generate or return registration resources.
- Enforce registration cutoff, duplicate detection, and capacity inside a serialized per-event write operation.

### Database responsibilities

- Persist events, seeded templates, configuration selections, registrations, and audit timestamps.
- Preserve registrations when an event is soft-deleted.
- Provide indexes and uniqueness constraints that support lookup and integrity.

## 4. Domain Model

### Game and template configuration

A `GAME` identifies a supported game. Each game owns a set of `GAME_CONFIGURATION_OPTION` rows. An option describes its key, label, data type, UI control, default value, required status, ordering, and active status. `GAME_CONFIGURATION_OPTION_VALUE` contains the allowed values for enum options.

Supported controls are:

- `TEXT` for one string value
- `NUMBER` for one numeric value
- `TOGGLE` for one boolean value
- `SELECT` for one enum value
- `CHECKBOX_GROUP` for one or more enum values

Templates are data-driven. Core event logic must not branch on game names. Adding a fourth game means adding a game row, its seeded options, and its option values; it should not require changes to event creation or registration logic.

Template creation and editing are out of scope for v1. The initial templates are seeded by [scripts/seed-game-templates.sql](../scripts/seed-game-templates.sql).

### Event

An event stores:

- Name
- Game reference
- UTC start time
- Duration in minutes
- Player capacity, from 0 through 30 subject to template constraints
- Optional location
- One play type: `CASUAL` or `TOURNAMENT`
- Tournament format when applicable: `SWISS_TOP_CUT` or `DOUBLE_ELIMINATION`
- Unique registration slug
- UTC creation timestamp
- Nullable UTC deletion timestamp

The end time is calculated as `start_at_utc + duration_minutes`. The duration is copied onto the event at creation time so later template changes do not alter an existing event.

Event properties are read-only after creation in v1. The organizer may soft-delete an event, including an event with registrations.

### Event configuration selections

Selected template values are copied into `EVENT_CONFIGURATION_SELECTION` at event creation. A select has one selection row; a checkbox group may have multiple rows for the same event and option. Enforce uniqueness across `(event_id, option_id, selected_value)`.

### Registration

An event registration stores:

- Event reference
- Required first name
- Required last name
- Optional player tag
- UTC registration timestamp

Players remain anonymous and no account is created in v1. Stronger duplicate detection using authenticated accounts is a future consideration.

## 5. Seeded Game Templates

The seed data supports:

| Game | Event formats | Default duration | Minimum players | Maximum players |
| --- | --- | ---: | ---: | ---: |
| Magic: The Gathering | Standard, Commander, Modern, Limited Draft, Limited Sealed | 120 minutes | 2 | 30 |
| Pokemon TCG | Standard, Expanded, Limited | 90 minutes | 2 | 30 |
| Yu-Gi-Oh TCG | Advanced, Time Wizard, Traditional | 90 minutes | 2 | 30 |

All three games seed both `CASUAL` and `TOURNAMENT` as allowed play types, plus Swiss + Top Cut and Double Elimination as tournament-format choices. Structured deck, product, ban-list, rotation, and ruleset settings are not included in v1.

## 6. Time Handling

- All persisted timestamps use UTC and field names ending in `_utc`, such as `start_at_utc`, `created_at_utc`, `registered_at_utc`, and `deleted_at_utc`.
- The browser converts UTC values to the user's local time zone for calendar display, event details, registration context, and ICS presentation where appropriate.
- Create-event date/time input is interpreted using the browser's local time zone and converted to UTC before the API request.
- The server compares the current UTC time with `start_at_utc` when deciding whether registration is open.
- The API should return unambiguous ISO 8601 UTC timestamps.

## 7. Primary User Flows

### Create an event

1. The organizer opens the Create Event modal from the top-right calendar action.
2. The frontend loads active game templates and renders the game selector.
3. Selecting a game loads its active configuration options and values.
4. The organizer supplies event details, selects one play type, and selects a tournament format when the play type is Tournament.
5. The frontend validates required fields, permitted characters, dates, capacity, and enum values.
6. The API repeats validation, resolves template defaults, snapshots duration and configuration selections, creates the event, and returns it.
7. The modal closes and the month calendar refreshes without a full page reload.

Clicking outside the modal does not close it. The X button and Escape attempt to close it; if the form has unsaved changes, the organizer must confirm before those changes are discarded.

### View an event

1. The organizer clicks an event in a calendar day cell.
2. The API returns the event and its read-only details.
3. The event modal opens on the Event Details tab.
4. The Players tab loads registrations and displays the total count above a compact table containing first name, last name, and player tag.
5. The Registration Resources tab displays a clickable/copyable registration URL, a QR code for that URL, and an ICS download.

The month grid provides previous-month, next-month, and Today controls. Every event remains visible in its day cell; a dense cell may scroll rather than hide events behind a `+N more` control. Past non-deleted events remain visible for history.

### Soft-delete an event

1. The organizer chooses Delete event from the bottom of the event modal.
2. The UI requests confirmation.
3. The API sets `deleted_at_utc` rather than removing the row.
4. Registrations and event configuration selections remain attached for audit history.
5. The modal closes and the event disappears from active calendar results.
6. The registration URL and QR destination show an unavailable-event state for the deleted event.

The schema leaves room for a future undelete operation. Previously downloaded ICS files remain valid files.

### Register a player

1. A player opens the registration URL directly or scans the QR code.
2. The API returns the event only if it exists and is not soft-deleted.
3. The page displays event name, game, local start time, calculated end time, and location when available.
4. The player submits required first name and last name plus an optional player tag.
5. The server executes the registration operation under a lock scoped to that event.
6. On success, the page shows a registration confirmation with event context.

If the event is full, has started, is unavailable, or the registration is a duplicate, the page shows an appropriate error and no success state.

## 8. Registration Correctness

Registration writes use a per-event lock so unrelated events can accept registrations concurrently. The operation is ordered as follows:

1. Acquire the event-specific lock.
2. Reload the event and reject it if soft-deleted or if current UTC time is at or after `start_at_utc`.
3. Normalize submitted names and player tag by trimming surrounding whitespace and comparing case-insensitively.
4. Reject a duplicate if the normalized first and last names match an existing registration together, or if a non-empty normalized player tag matches an existing registration.
5. Count existing registrations and reject the request if the count is at capacity.
6. Insert the registration and commit the transaction.
7. Release the lock.

A duplicate response uses the message: `Someone with that registration info has already registered.` Capacity enforcement remains server-side and cannot be bypassed through the UI. Waitlists are not supported.

For a single API instance, an in-process per-event lock is sufficient for the v1 runtime. If the application later runs multiple API instances, the lock must move to a database-backed transaction/locking strategy or another shared coordination mechanism.

## 9. Suggested API Surface

The exact route names may change during implementation, but responsibilities should remain separated:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/games` | List active seeded games |
| `GET` | `/api/games/{gameId}/configuration` | Load active options and values for a game |
| `GET` | `/api/events?month=YYYY-MM` | Load non-deleted events for a calendar month |
| `POST` | `/api/events` | Create an event and snapshot its template configuration |
| `GET` | `/api/events/{eventId}` | Load read-only event details and configuration |
| `DELETE` | `/api/events/{eventId}` | Soft-delete an event |
| `GET` | `/api/events/{eventId}/registrations` | Load player rows and total count for the Players tab |
| `GET` | `/api/events/{eventId}/registration-resources` | Return registration URL and resource metadata |
| `GET` | `/api/events/{eventId}/calendar-invite` | Download the generated ICS file |
| `GET` | `/api/registration/{slug}` | Load a public registration page's event context |
| `POST` | `/api/registration/{slug}` | Validate and create a player registration |

The public registration endpoints must not expose organizer-only data beyond what the registration page needs.

## 10. Validation and Error Behavior

Validation occurs in both layers:

- React gives immediate field-level feedback and prevents avoidable submissions.
- The API is authoritative and repeats all validation, including template option membership, capacity, event cutoff, duplicate detection, and soft-delete checks.

Important error states include:

- Invalid or missing event data
- Capacity reached
- Registration closed at event start
- Duplicate registration
- Unknown or deleted event
- Unexpected server or network failure

The frontend preserves recoverable form values, prevents duplicate submissions while a request is active, and never displays registration success until the API confirms persistence.

## 11. Scope and Deliberate Cuts

Included in v1:

- Month-grid calendar
- Event creation
- Read-only event details modal
- Soft deletion
- Seeded game templates
- Dynamic configuration controls
- Player registration
- Per-event serialized capacity enforcement
- Case-insensitive duplicate rejection
- QR code and ICS resources

Out of scope for v1:

- Editing event properties after creation
- Template creation or editing
- Tournament execution and match management
- Authentication and player accounts
- Strong account-based de-duplication
- Waitlists
- Payments, email, recurring events, and admin dashboards
- Live push updates
- Structured deck, product, legality, rotation, or ruleset configuration
- Deployment and CSS polish

## 12. Verification Strategy

The implementation should verify the highest-risk behavior first:

- Create an event for each seeded game and confirm its dynamic options.
- Confirm UTC persistence and browser-local rendering around time-zone boundaries.
- Confirm event duration produces the expected calculated end time and ICS end time.
- Confirm soft deletion removes an event from active calendar and registration results while preserving registrations.
- Confirm a capacity of one allows only one successful registration under concurrent requests.
- Confirm duplicate names and player tags are rejected after trimming and case folding.
- Confirm registration at or after the event start time is rejected.
- Confirm QR and ICS resources point to the correct event.
