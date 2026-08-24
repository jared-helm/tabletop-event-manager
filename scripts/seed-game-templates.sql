BEGIN TRANSACTION;

INSERT OR IGNORE INTO GAME (code, display_name, is_active, created_at_utc)
VALUES ('mtg', 'Magic: The Gathering', 1, '2026-01-01T00:00:00Z');

INSERT OR IGNORE INTO GAME (code, display_name, is_active, created_at_utc)
VALUES ('pokemon-tcg', 'Pokemon TCG', 1, '2026-01-01T00:00:00Z');

INSERT OR IGNORE INTO GAME (code, display_name, is_active, created_at_utc)
VALUES ('yugioh-tcg', 'Yu-Gi-Oh TCG', 1, '2026-01-01T00:00:00Z');

-- Shared template options are seeded per game so each template can evolve independently.
INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, is_required, sort_order, is_active)
SELECT id, 'allowed_play_types', 'Allowed play types', 'ENUM', 'CHECKBOX_GROUP', 1, 10, 1
FROM GAME WHERE code = 'mtg';
INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, is_required, sort_order, is_active)
SELECT id, 'event_format', 'Event format', 'ENUM', 'SELECT', 1, 20, 1
FROM GAME WHERE code = 'mtg';
INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, is_required, sort_order, is_active)
SELECT id, 'tournament_format', 'Tournament format', 'ENUM', 'SELECT', 0, 30, 1
FROM GAME WHERE code = 'mtg';

INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, is_required, sort_order, is_active)
SELECT id, 'allowed_play_types', 'Allowed play types', 'ENUM', 'CHECKBOX_GROUP', 1, 10, 1
FROM GAME WHERE code = 'pokemon-tcg';
INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, is_required, sort_order, is_active)
SELECT id, 'event_format', 'Event format', 'ENUM', 'SELECT', 1, 20, 1
FROM GAME WHERE code = 'pokemon-tcg';
INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, is_required, sort_order, is_active)
SELECT id, 'tournament_format', 'Tournament format', 'ENUM', 'SELECT', 0, 30, 1
FROM GAME WHERE code = 'pokemon-tcg';

INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, is_required, sort_order, is_active)
SELECT id, 'allowed_play_types', 'Allowed play types', 'ENUM', 'CHECKBOX_GROUP', 1, 10, 1
FROM GAME WHERE code = 'yugioh-tcg';
INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, is_required, sort_order, is_active)
SELECT id, 'event_format', 'Event format', 'ENUM', 'SELECT', 1, 20, 1
FROM GAME WHERE code = 'yugioh-tcg';
INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, is_required, sort_order, is_active)
SELECT id, 'tournament_format', 'Tournament format', 'ENUM', 'SELECT', 0, 30, 1
FROM GAME WHERE code = 'yugioh-tcg';

INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, default_value, is_required, sort_order, is_active)
SELECT game.id, defaults.key, defaults.label, 'NUMBER', 'NUMBER', defaults.default_value, 1, defaults.sort_order, 1
FROM GAME game
JOIN (SELECT 'default_duration_minutes' AS key, 'Default duration' AS label, '120' AS default_value, 40 AS sort_order
      UNION ALL SELECT 'minimum_players', 'Minimum players', '2', 50
      UNION ALL SELECT 'maximum_players', 'Maximum players', '30', 60) defaults
WHERE game.code = 'mtg';

INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, default_value, is_required, sort_order, is_active)
SELECT game.id, defaults.key, defaults.label, 'NUMBER', 'NUMBER', defaults.default_value, 1, defaults.sort_order, 1
FROM GAME game
JOIN (SELECT 'default_duration_minutes' AS key, 'Default duration' AS label, '90' AS default_value, 40 AS sort_order
      UNION ALL SELECT 'minimum_players', 'Minimum players', '2', 50
      UNION ALL SELECT 'maximum_players', 'Maximum players', '30', 60) defaults
WHERE game.code = 'pokemon-tcg';

INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION
    (game_id, key, label, data_type, ui_control, default_value, is_required, sort_order, is_active)
SELECT game.id, defaults.key, defaults.label, 'NUMBER', 'NUMBER', defaults.default_value, 1, defaults.sort_order, 1
FROM GAME game
JOIN (SELECT 'default_duration_minutes' AS key, 'Default duration' AS label, '90' AS default_value, 40 AS sort_order
      UNION ALL SELECT 'minimum_players', 'Minimum players', '2', 50
      UNION ALL SELECT 'maximum_players', 'Maximum players', '30', 60) defaults
WHERE game.code = 'yugioh-tcg';

INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION_VALUE
    (option_id, value, label, sort_order, is_active)
SELECT option.id, seed_value.value, seed_value.label, seed_value.sort_order, 1
FROM GAME_CONFIGURATION_OPTION option
JOIN GAME game ON game.id = option.game_id
JOIN (SELECT 'CASUAL' AS value, 'Casual/Friendly' AS label, 10 AS sort_order
    UNION ALL SELECT 'TOURNAMENT', 'Tournament', 20) seed_value
WHERE game.code IN ('mtg', 'pokemon-tcg', 'yugioh-tcg')
  AND option.key = 'allowed_play_types';

INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION_VALUE
    (option_id, value, label, sort_order, is_active)
SELECT option.id, seed_value.value, seed_value.label, seed_value.sort_order, 1
FROM GAME_CONFIGURATION_OPTION option
JOIN GAME game ON game.id = option.game_id
JOIN (SELECT 'STANDARD' AS value, 'Standard' AS label, 10 AS sort_order
      UNION ALL SELECT 'COMMANDER', 'Commander', 20
      UNION ALL SELECT 'MODERN', 'Modern', 30
      UNION ALL SELECT 'LIMITED_DRAFT', 'Limited Draft', 40
    UNION ALL SELECT 'LIMITED_SEALED', 'Limited Sealed', 50) seed_value
WHERE game.code = 'mtg' AND option.key = 'event_format';

INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION_VALUE
    (option_id, value, label, sort_order, is_active)
SELECT option.id, seed_value.value, seed_value.label, seed_value.sort_order, 1
FROM GAME_CONFIGURATION_OPTION option
JOIN GAME game ON game.id = option.game_id
JOIN (SELECT 'STANDARD' AS value, 'Standard' AS label, 10 AS sort_order
      UNION ALL SELECT 'EXPANDED', 'Expanded', 20
    UNION ALL SELECT 'LIMITED', 'Limited', 30) seed_value
WHERE game.code = 'pokemon-tcg' AND option.key = 'event_format';

INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION_VALUE
    (option_id, value, label, sort_order, is_active)
SELECT option.id, seed_value.value, seed_value.label, seed_value.sort_order, 1
FROM GAME_CONFIGURATION_OPTION option
JOIN GAME game ON game.id = option.game_id
JOIN (SELECT 'ADVANCED' AS value, 'Advanced' AS label, 10 AS sort_order
      UNION ALL SELECT 'TIME_WIZARD', 'Time Wizard', 20
    UNION ALL SELECT 'TRADITIONAL', 'Traditional', 30) seed_value
WHERE game.code = 'yugioh-tcg' AND option.key = 'event_format';

INSERT OR IGNORE INTO GAME_CONFIGURATION_OPTION_VALUE
    (option_id, value, label, sort_order, is_active)
SELECT option.id, seed_value.value, seed_value.label, seed_value.sort_order, 1
FROM GAME_CONFIGURATION_OPTION option
JOIN GAME game ON game.id = option.game_id
JOIN (SELECT 'SWISS_TOP_CUT' AS value, 'Swiss + Top Cut' AS label, 10 AS sort_order
    UNION ALL SELECT 'DOUBLE_ELIMINATION', 'Double Elimination', 20) seed_value
WHERE game.code IN ('mtg', 'pokemon-tcg', 'yugioh-tcg')
  AND option.key = 'tournament_format';

COMMIT;
