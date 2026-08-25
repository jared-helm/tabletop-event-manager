# Page and Interaction Requirements

This document describes the pages, modals, controls, and user flows for the tabletop event manager. The application has one primary organizer page and one player-facing registration page.

## Page Overview

| Page | Audience | Purpose |
| --- | --- | --- |
| Calendar | Organizer | View scheduled events and create, inspect, edit, or delete events |
| Registration | Player | Submit player details for a specific event |

Event details, player registrations, and registration resources are presented in tabs within the event modal.

## 1. Calendar Page

The calendar is the application's main page.

### Layout

- Display scheduled events in a month-grid calendar.
- Each day cell must show all events scheduled for that day, including enough identifying information to distinguish multiple events.
- Clicking an event opens its event modal.
- Place a `Create event` button above the calendar in the top-right area.
- After an event is created or deleted, the calendar updates immediately to reflect the change.

### Create Event Action

Clicking `Create event` opens the create-event form in a modal.

## 2. Create Event Modal

### Modal Behavior

- The modal opens over the calendar without navigating away from the page.
- An `X` button closes the modal.
- Clicking outside the modal does not close it.
- Pressing Escape or clicking the X button attempts to close the modal.
- If the form has unsaved changes, closing prompts the user to confirm before discarding them.
- Closing an unchanged modal without submitting discards its values.
- The modal should provide a clear accessible title and focus should move into the modal when it opens.

### Event Details Form

The form collects the event properties required to create an event, including:

- Event name
- Game type
- Date and start time
- Player capacity
- Play type, such as Casual/Friendly or Tournament
- Game-specific event format
- Tournament format when Tournament is selected
- Any additional options defined by the selected game's configuration template

Location is not collected per event; the application is scoped to a single store, so location is a fixed application setting rather than a per-event field.

### Dynamic Game Configuration

- Selecting a game loads that game's active configuration template.
- The form dynamically renders the options and controls defined by the template.
- Select inputs display the active values configured for that game and option.
- Checkbox groups allow multiple values to be selected.
- Required options are visibly identified and validated.
- Tournament format is shown only when the event play type is Tournament.
- The UI must not contain game-specific conditional logic that would prevent adding another game template.

### Client-Side Constraints

The UI validates data before submission, including:

- Required fields cannot be empty.
- Text fields allow only the permitted characters and lengths.
- Player capacity must be a whole number.
- Player capacity cannot be negative.
- Player capacity cannot exceed the configured maximum or the application maximum of 30.
- Date and time must be valid.
- Event format and other enumerated values must come from the options supplied by the selected game template.
- Tournament format is required when Tournament is selected.
- Invalid fields show clear, local validation messages.

Client-side validation improves the user experience, but the server must repeat all relevant validation before saving.

### Successful Submission

- Submit the form to the API.
- When the event is saved successfully, close the modal.
- Add the new event to the calendar without requiring a full page reload.
- Display the event's saved date, time, and title on the calendar.

### Failed Submission

- Keep the modal open so the organizer can correct the form.
- Display a clear error message near the relevant field when possible.
- Display a general error message for unexpected server or network failures.
- Preserve the entered values unless the server response makes them unsafe or invalid.

## 3. Event Modal

Clicking an event on the calendar opens a modal for that event.

### Modal Behavior

- The modal contains an `X` button.
- Clicking outside the modal does not close it.
- Pressing Escape or clicking the X button attempts to close it.
- The modal has tabs across the top.
- The modal opens on the Event Details tab by default.
- The event properties are read-only after creation in v1.
- Switching tabs does not involve unsaved event changes.

### Tab 1: Event Details

This tab allows the organizer to:

- View the event properties.
- View the game type and its game-specific configuration values.
- See the event's calculated end time based on its configured duration.

A `Delete event` action appears at the bottom of this tab. It should be visually separated from the read-only event details because deletion is destructive.

Editing event properties is out of scope for v1. This avoids changing event setup after players may have registered, since changes to capacity, timing, play type, or game configuration could affect whether existing players still intend to attend. The event's game type and all other properties are therefore read-only after creation.

### Delete Event Flow

- Clicking `Delete event` asks for confirmation before deleting.
- If deletion succeeds, close the modal.
- Remove the event from the calendar immediately.
- If deletion fails, keep the modal open and show the failure message.
- An organizer may delete an event even when it has registrations.
- Deletion is a soft delete: mark the event deleted and retain its registrations and related data for audit purposes.
- Soft-deleted events are excluded from the calendar and unavailable through the player registration page in v1.
- The data model should leave room for a future undelete operation.

### Tab 2: Players

The Players tab displays registrations for the selected event.

- Show registered players in a compact table.
- Show each player's first name, last name, and optional player tag.
- Display the total player count immediately above the table.
- Update the table after a new registration is submitted.
- Display the event capacity alongside the total player count.
- When there are no registrations, show an appropriate empty state.
- Do not expose organizer-only controls that are not supported by the requirements.

Registration is capacity-enforced by the server. Once the event is full, new submissions are rejected with a clear error. Waitlist behavior is not required for v1.

### Tab 3: Registration Resources

The third tab is named `Registration Resources` and provides everything needed to share the event registration page.

- Display the registration URL as a clickable link.
- Provide a copy action for the registration URL.
- Provide a download action for the event's `.ics` calendar invite.
- Display a QR code beneath the URL and actions.
- The QR code encodes the same registration URL shown in the tab.
- Scanning the QR code opens the player registration page.
- Show feedback after the URL is copied or a download fails.

## 4. Registration Page

The registration page is reached through the event's registration link or QR code.

### Page Content

- Identify the event clearly, including its name, game, date, time, and location when available.
- Display the registration form.
- Collect first name and last name as separate required fields.
- Collect an optional player tag.
- Apply field validation before submission.
- Show the event's current capacity or availability where appropriate.

### Registration Submission

- Submit the player details to the API.
- The server rejects registrations submitted at or after the event start time.
- The server validates capacity and saves the registration atomically.
- Registration writes for an event are serialized with a lock: each write waits for the previous write to finish before checking capacity and saving.
- The server rejects a duplicate when the trimmed first name and trimmed last name exactly match an existing registration, or when the player tag matches an existing non-empty player tag.
- Duplicate comparisons are case-insensitive. When rejected, show: `Someone with that registration info has already registered.`
- On success, show a clear registration success message.
- Include enough event context in the success state for the player to know what they registered for.
- Do not require authentication.

### Registration Failure

- If the submission cannot be saved, show a clear error state or error page.
- Explain whether the event is full, the submitted data is invalid, or the service is temporarily unavailable when that information is known.
- Explain when registration has closed because the event start time has passed.
- Explain when the player appears to be a duplicate registration.
- Preserve entered values for recoverable validation errors.
- Do not show a success message unless the server confirms that the registration was saved.

## 5. Shared Interaction and Accessibility Expectations

- All modal controls and tabs must be keyboard accessible.
- The active tab must be visually and programmatically identifiable.
- Escape should close an open modal unless a confirmation dialog is active.
- Focus should not move behind an open modal.
- Destructive actions require confirmation.
- Loading, saving, deleting, copying, downloading, and registration states should be visible to the user.
- Controls must prevent duplicate submissions while a request is in progress.
- Server errors must be represented in the UI rather than silently ignored.

## 6. Resolved V1 Decisions

- [x] Calendar view: support only a month-grid calendar. Each day cell shows all events scheduled for that day.
- [x] Event editing: editing event properties is out of scope for v1. Event details are read-only after creation.
- [x] Event deletion: an organizer may delete an event with registrations. Deletion is soft; registrations remain attached for audit history, and the schema should allow future undeletion.
- [x] Event end time: calculate it from the configured template duration rather than collecting a separate end time.
- [x] Registration fields: collect required first name and last name separately, plus an optional player tag.
- [x] Duplicate registrations: flag an exact match of trimmed first and last name, or a match on a non-empty player tag. Stronger de-duplication using authenticated user accounts is a future consideration.
- [x] Duplicate registration message and matching: compare names and tags case-insensitively after trimming; reject with `Someone with that registration info has already registered.`
- [x] Players tab: show only first name, last name, and player tag, with the total player count above the table.
- [x] Registration cutoff: registration closes at the event start time and is enforced by the server.
- [x] Concurrent registrations: serialize registration writes per event with a lock. Each write waits for the previous write to finish before checking capacity and saving.
- [x] Registration URL: use the event's existing unique registration slug as the shareable URL.
- [x] Calendar invite duration: generate the `.ics` end time from the event start time and configured duration.
- [x] Unknown or deleted event: show a clear unavailable-event error page and do not show a registration form.
- [x] Registration refresh: refresh the Players tab when the event modal opens and after a registration flow completes. Live push notifications are out of scope for v1.
- [x] Modal structure: use the event modal for viewing an existing event and a separate create-event modal for creation. They may share form and presentation components where useful.
- [x] Template management: seed the templates and option values for the three supported games. Creating or editing templates through the UI or API is out of scope for v1.

## 7. Finalized V1 Decisions

- Time zone: store event start times, registration times, deletion times, and other database timestamps as UTC with `_utc` field names. The browser converts UTC values to the user's local time zone for display and form interaction.
- Calendar navigation: provide previous-month, next-month, and Today controls. Do not add a second calendar view in v1.
- Dense calendar days: show all events in each day cell using a compact scrollable cell if necessary; do not hide events behind a `+N more` control because the requirement is to see all events for a day.
- Calendar event summary: show the event start time and name in each day cell. Capacity and game details remain in the event modal.
- Duplicate matching: reject matching registration data rather than saving a warning. Treat blank or whitespace-only player tags as absent.
- Registration locking: use a per-event lock so registrations for different events can proceed independently. Acquire the lock before checking the event's start time, duplicate data, and capacity, then save and release it.
- Deleted-event resources: previously downloaded ICS files remain valid files, but active registration links and QR codes for soft-deleted events show the unavailable-event state.
- Past events: retain them in the month calendar and allow the organizer to view their read-only details and retained registrations. Only soft-deleted events are hidden.
- Template snapshots: copy the selected template duration and event configuration values to the event at creation time so later seed/template changes do not alter existing events.
