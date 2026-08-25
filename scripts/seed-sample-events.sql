-- Sample events for local development and manual verification of the calendar.
-- Uses fixed registration slugs so the insert stays idempotent across restarts.
BEGIN TRANSACTION;

INSERT OR IGNORE INTO Event
    (GameId, Name, StartAtUtc, DurationMinutes, Capacity, PlayType, TournamentFormat, RegistrationSlug, CreatedAtUtc)
SELECT Id, 'Standard Showdown', '2026-08-27T18:00:00Z', 120, 16, 'TOURNAMENT', 'SWISS_TOP_CUT', 'seed-mtg-standard-showdown', '2026-01-01T00:00:00Z'
FROM Game WHERE Code = 'mtg';

INSERT OR IGNORE INTO Event
    (GameId, Name, StartAtUtc, DurationMinutes, Capacity, PlayType, TournamentFormat, RegistrationSlug, CreatedAtUtc)
SELECT Id, 'Casual Trainer Night', '2026-09-10T23:00:00Z', 90, 8, 'CASUAL', NULL, 'seed-pokemon-trainer-night', '2026-01-01T00:00:00Z'
FROM Game WHERE Code = 'pokemon-tcg';

INSERT OR IGNORE INTO Event
    (GameId, Name, StartAtUtc, DurationMinutes, Capacity, PlayType, TournamentFormat, RegistrationSlug, CreatedAtUtc)
SELECT Id, 'Advanced Duelist Cup', '2026-09-24T22:00:00Z', 90, 12, 'TOURNAMENT', 'DOUBLE_ELIMINATION', 'seed-yugioh-duelist-cup', '2026-01-01T00:00:00Z'
FROM Game WHERE Code = 'yugioh-tcg';

INSERT OR IGNORE INTO EventConfigurationSelection (EventId, OptionId, SelectedValue)
SELECT event.Id, option.Id, 'STANDARD'
FROM Event event
JOIN GameConfigurationOption option ON option.GameId = event.GameId AND option.Key = 'event_format'
WHERE event.RegistrationSlug = 'seed-mtg-standard-showdown';

INSERT OR IGNORE INTO EventConfigurationSelection (EventId, OptionId, SelectedValue)
SELECT event.Id, option.Id, 'STANDARD'
FROM Event event
JOIN GameConfigurationOption option ON option.GameId = event.GameId AND option.Key = 'event_format'
WHERE event.RegistrationSlug = 'seed-pokemon-trainer-night';

INSERT OR IGNORE INTO EventConfigurationSelection (EventId, OptionId, SelectedValue)
SELECT event.Id, option.Id, 'ADVANCED'
FROM Event event
JOIN GameConfigurationOption option ON option.GameId = event.GameId AND option.Key = 'event_format'
WHERE event.RegistrationSlug = 'seed-yugioh-duelist-cup';

COMMIT;
