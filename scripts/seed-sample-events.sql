-- Sample events for local development and manual verification of the calendar.
-- Uses fixed registration slugs so the insert stays idempotent across restarts.
BEGIN TRANSACTION;

INSERT OR IGNORE INTO EVENT
    (game_id, name, start_at_utc, duration_minutes, capacity, play_type, tournament_format, registration_slug, created_at_utc)
SELECT id, 'Standard Showdown', '2026-08-27T18:00:00Z', 120, 16, 'TOURNAMENT', 'SWISS_TOP_CUT', 'seed-mtg-standard-showdown', '2026-01-01T00:00:00Z'
FROM GAME WHERE code = 'mtg';

INSERT OR IGNORE INTO EVENT
    (game_id, name, start_at_utc, duration_minutes, capacity, play_type, tournament_format, registration_slug, created_at_utc)
SELECT id, 'Casual Trainer Night', '2026-09-10T23:00:00Z', 90, 8, 'CASUAL', NULL, 'seed-pokemon-trainer-night', '2026-01-01T00:00:00Z'
FROM GAME WHERE code = 'pokemon-tcg';

INSERT OR IGNORE INTO EVENT
    (game_id, name, start_at_utc, duration_minutes, capacity, play_type, tournament_format, registration_slug, created_at_utc)
SELECT id, 'Advanced Duelist Cup', '2026-09-24T22:00:00Z', 90, 12, 'TOURNAMENT', 'DOUBLE_ELIMINATION', 'seed-yugioh-duelist-cup', '2026-01-01T00:00:00Z'
FROM GAME WHERE code = 'yugioh-tcg';

INSERT OR IGNORE INTO EVENT_CONFIGURATION_SELECTION (event_id, option_id, selected_value)
SELECT event.id, option.id, 'STANDARD'
FROM EVENT event
JOIN GAME_CONFIGURATION_OPTION option ON option.game_id = event.game_id AND option.key = 'event_format'
WHERE event.registration_slug = 'seed-mtg-standard-showdown';

INSERT OR IGNORE INTO EVENT_CONFIGURATION_SELECTION (event_id, option_id, selected_value)
SELECT event.id, option.id, 'STANDARD'
FROM EVENT event
JOIN GAME_CONFIGURATION_OPTION option ON option.game_id = event.game_id AND option.key = 'event_format'
WHERE event.registration_slug = 'seed-pokemon-trainer-night';

INSERT OR IGNORE INTO EVENT_CONFIGURATION_SELECTION (event_id, option_id, selected_value)
SELECT event.id, option.id, 'ADVANCED'
FROM EVENT event
JOIN GAME_CONFIGURATION_OPTION option ON option.game_id = event.game_id AND option.key = 'event_format'
WHERE event.registration_slug = 'seed-yugioh-duelist-cup';

COMMIT;
