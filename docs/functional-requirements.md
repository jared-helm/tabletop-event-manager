# Functional Requirements

Derived from [Senior_Software_Engineer_—_Take-Home.md](../Senior_Software_Engineer_—_Take-Home.md). Only requirements explicitly stated or directly implied by that document are listed here.

Note: A trailing `*` marks requirements added after the original take-home prompt.

## 1. Event Creation

- FR-1.1: An organizer must be able to create an event.
- FR-1.2: Each event must record, at minimum: a name, a game type, a date and start time, and a player capacity.
- FR-1.3: The player capacity field must support values up to 30 players.
- FR-1.4: Event start time and duration must be stored on the event so the end time can be calculated without relying on later template changes. *

## 2. Game Types & Templates

- FR-2.1: The app must support at least 3 trading card games.
- FR-2.2: The app must support the following game types: Magic: The Gathering, Pokemon TCG, and Yu-Gi-Oh TCG. *
- FR-2.3: Selecting a game type during event creation must dynamically control event configuration.
- FR-2.4: Game-specific configuration must include at least two properties (e.g., available play formats, default event duration, default/max capacity, minimum players to start the event).
- FR-2.5: Each event must declare a play type, at minimum: Casual/Friendly or Tournament. *
- FR-2.6: Each game template must define which play types are available for that game. *
- FR-2.7: Each game template must define selectable event formats at event creation time (e.g., MTG Standard/Commander, Pokemon Standard, Yu-Gi-Oh Advanced). *
- FR-2.8: Tournament events must allow selecting Swiss + Top Cut as the tournament format. *
- FR-2.9: Tournament events must allow selecting Double Elimination as the tournament format. *
- FR-2.10: Tournament structure options may be constrained by game template. *

## 3. Calendar View

- FR-3.1: Scheduled events must be displayed on a calendar.
- FR-3.2: An organizer must be able to see what events are happening on a given day.
- FR-3.3: The calendar must provide a month grid view. Each day cell must show all events scheduled for that day.

## 4. Calendar Invite (.ics Export)

- FR-4.1: An event page must offer a downloadable calendar invite file in `.ics` format for that event.
- FR-4.2: The `.ics` file must contain the correct event title, start/end time, and location.
- FR-4.3: The `.ics` file must be importable into Google Calendar and Outlook.

## 5. Registration with QR Code

- FR-5.1: Each event must have a registration link.
- FR-5.2: The event page must display a QR code that encodes the event's registration link.
- FR-5.3: Scanning the QR code must take the player to a registration form.
- FR-5.4: The registration form requires a first name and last name as separate fields and may collect an optional player tag. *
- FR-5.5: Registration must be capacity-enforced: once an event is full, further registration attempts must be rejected with a clear message.
- FR-5.6: Waitlist behavior is not required for v1. *
- FR-5.7: Registration must close at the event start time, enforced by the server. *
- FR-5.8: Duplicate registrations must be rejected when the case-insensitive, trimmed first and last names match an existing registration together, or when a non-empty case-insensitive, trimmed player tag matches. *

## 6. Explicitly Out of Scope

The source document explicitly states the following must **not** be built:

- Payments
- Email sending
- Recurring events
- Editing or cancelling events
- Admin dashboards

Additionally:
- No authentication is required; there is a single, implicit organizer.
- Players remain anonymous until they submit the registration form.
- Age-division handling is not required for v1. *
- Tournament management is not required for v1 (no pairings, standings, round progression, bracket execution, or match result reporting). *
- Template creation and editing are not required for v1; the three supported game templates and their option values are seeded. *
- Editing event properties after creation is not required for v1. *
- All database timestamps must be stored in UTC and rendered in the browser using the user's local time zone. *
- Registration duplicate matching must be case-insensitive after trimming surrounding whitespace. *
