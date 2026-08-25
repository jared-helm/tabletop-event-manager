BEGIN TRANSACTION;

INSERT OR IGNORE INTO Game (Code, DisplayName, IsActive, CreatedAtUtc)
VALUES ('mtg', 'Magic: The Gathering', 1, '2026-01-01T00:00:00Z');

INSERT OR IGNORE INTO Game (Code, DisplayName, IsActive, CreatedAtUtc)
VALUES ('pokemon-tcg', 'Pokemon TCG', 1, '2026-01-01T00:00:00Z');

INSERT OR IGNORE INTO Game (Code, DisplayName, IsActive, CreatedAtUtc)
VALUES ('yugioh-tcg', 'Yu-Gi-Oh TCG', 1, '2026-01-01T00:00:00Z');

-- Shared template options are seeded per game so each template can evolve independently.
INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, IsRequired, SortOrder, IsActive)
SELECT Id, 'allowed_play_types', 'Allowed play types', 'ENUM', 'CHECKBOX_GROUP', 1, 10, 1
FROM Game WHERE Code = 'mtg';
INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, IsRequired, SortOrder, IsActive)
SELECT Id, 'event_format', 'Event format', 'ENUM', 'SELECT', 1, 20, 1
FROM Game WHERE Code = 'mtg';
INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, IsRequired, SortOrder, IsActive)
SELECT Id, 'tournament_format', 'Tournament format', 'ENUM', 'SELECT', 0, 30, 1
FROM Game WHERE Code = 'mtg';

INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, IsRequired, SortOrder, IsActive)
SELECT Id, 'allowed_play_types', 'Allowed play types', 'ENUM', 'CHECKBOX_GROUP', 1, 10, 1
FROM Game WHERE Code = 'pokemon-tcg';
INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, IsRequired, SortOrder, IsActive)
SELECT Id, 'event_format', 'Event format', 'ENUM', 'SELECT', 1, 20, 1
FROM Game WHERE Code = 'pokemon-tcg';
INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, IsRequired, SortOrder, IsActive)
SELECT Id, 'tournament_format', 'Tournament format', 'ENUM', 'SELECT', 0, 30, 1
FROM Game WHERE Code = 'pokemon-tcg';

INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, IsRequired, SortOrder, IsActive)
SELECT Id, 'allowed_play_types', 'Allowed play types', 'ENUM', 'CHECKBOX_GROUP', 1, 10, 1
FROM Game WHERE Code = 'yugioh-tcg';
INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, IsRequired, SortOrder, IsActive)
SELECT Id, 'event_format', 'Event format', 'ENUM', 'SELECT', 1, 20, 1
FROM Game WHERE Code = 'yugioh-tcg';
INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, IsRequired, SortOrder, IsActive)
SELECT Id, 'tournament_format', 'Tournament format', 'ENUM', 'SELECT', 0, 30, 1
FROM Game WHERE Code = 'yugioh-tcg';

INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, DefaultValue, IsRequired, SortOrder, IsActive)
SELECT game.Id, defaults.key, defaults.label, 'NUMBER', 'NUMBER', defaults.default_value, 1, defaults.sort_order, 1
FROM Game game
JOIN (SELECT 'default_duration_minutes' AS key, 'Default duration' AS label, '120' AS default_value, 40 AS sort_order
      UNION ALL SELECT 'minimum_players', 'Minimum players', '2', 50
      UNION ALL SELECT 'maximum_players', 'Maximum players', '30', 60) defaults
WHERE game.Code = 'mtg';

INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, DefaultValue, IsRequired, SortOrder, IsActive)
SELECT game.Id, defaults.key, defaults.label, 'NUMBER', 'NUMBER', defaults.default_value, 1, defaults.sort_order, 1
FROM Game game
JOIN (SELECT 'default_duration_minutes' AS key, 'Default duration' AS label, '90' AS default_value, 40 AS sort_order
      UNION ALL SELECT 'minimum_players', 'Minimum players', '2', 50
      UNION ALL SELECT 'maximum_players', 'Maximum players', '30', 60) defaults
WHERE game.Code = 'pokemon-tcg';

INSERT OR IGNORE INTO GameConfigurationOption
    (GameId, Key, Label, DataType, UiControl, DefaultValue, IsRequired, SortOrder, IsActive)
SELECT game.Id, defaults.key, defaults.label, 'NUMBER', 'NUMBER', defaults.default_value, 1, defaults.sort_order, 1
FROM Game game
JOIN (SELECT 'default_duration_minutes' AS key, 'Default duration' AS label, '90' AS default_value, 40 AS sort_order
      UNION ALL SELECT 'minimum_players', 'Minimum players', '2', 50
      UNION ALL SELECT 'maximum_players', 'Maximum players', '30', 60) defaults
WHERE game.Code = 'yugioh-tcg';

INSERT OR IGNORE INTO GameConfigurationOptionValue
    (OptionId, Value, Label, SortOrder, IsActive)
SELECT option.Id, seed_value.value, seed_value.label, seed_value.sort_order, 1
FROM GameConfigurationOption option
JOIN Game game ON game.Id = option.GameId
JOIN (SELECT 'CASUAL' AS value, 'Casual/Friendly' AS label, 10 AS sort_order
    UNION ALL SELECT 'TOURNAMENT', 'Tournament', 20) seed_value
WHERE game.Code IN ('mtg', 'pokemon-tcg', 'yugioh-tcg')
    AND option.Key = 'allowed_play_types';

INSERT OR IGNORE INTO GameConfigurationOptionValue
    (OptionId, Value, Label, SortOrder, IsActive)
SELECT option.Id, seed_value.value, seed_value.label, seed_value.sort_order, 1
FROM GameConfigurationOption option
JOIN Game game ON game.Id = option.GameId
JOIN (SELECT 'STANDARD' AS value, 'Standard' AS label, 10 AS sort_order
      UNION ALL SELECT 'COMMANDER', 'Commander', 20
      UNION ALL SELECT 'MODERN', 'Modern', 30
      UNION ALL SELECT 'LIMITED_DRAFT', 'Limited Draft', 40
    UNION ALL SELECT 'LIMITED_SEALED', 'Limited Sealed', 50) seed_value
WHERE game.Code = 'mtg' AND option.Key = 'event_format';

INSERT OR IGNORE INTO GameConfigurationOptionValue
    (OptionId, Value, Label, SortOrder, IsActive)
SELECT option.Id, seed_value.value, seed_value.label, seed_value.sort_order, 1
FROM GameConfigurationOption option
JOIN Game game ON game.Id = option.GameId
JOIN (SELECT 'STANDARD' AS value, 'Standard' AS label, 10 AS sort_order
      UNION ALL SELECT 'EXPANDED', 'Expanded', 20
    UNION ALL SELECT 'LIMITED', 'Limited', 30) seed_value
WHERE game.Code = 'pokemon-tcg' AND option.Key = 'event_format';

INSERT OR IGNORE INTO GameConfigurationOptionValue
    (OptionId, Value, Label, SortOrder, IsActive)
SELECT option.Id, seed_value.value, seed_value.label, seed_value.sort_order, 1
FROM GameConfigurationOption option
JOIN Game game ON game.Id = option.GameId
JOIN (SELECT 'ADVANCED' AS value, 'Advanced' AS label, 10 AS sort_order
      UNION ALL SELECT 'TIME_WIZARD', 'Time Wizard', 20
    UNION ALL SELECT 'TRADITIONAL', 'Traditional', 30) seed_value
WHERE game.Code = 'yugioh-tcg' AND option.Key = 'event_format';

INSERT OR IGNORE INTO GameConfigurationOptionValue
    (OptionId, Value, Label, SortOrder, IsActive)
SELECT option.Id, seed_value.value, seed_value.label, seed_value.sort_order, 1
FROM GameConfigurationOption option
JOIN Game game ON game.Id = option.GameId
JOIN (SELECT 'SWISS_TOP_CUT' AS value, 'Swiss + Top Cut' AS label, 10 AS sort_order
    UNION ALL SELECT 'DOUBLE_ELIMINATION', 'Double Elimination', 20) seed_value
WHERE game.Code IN ('mtg', 'pokemon-tcg', 'yugioh-tcg')
    AND option.Key = 'tournament_format';

COMMIT;
