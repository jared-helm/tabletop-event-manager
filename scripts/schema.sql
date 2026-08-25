PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS GAME (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    code TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS EVENT (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id INTEGER NOT NULL,
    name TEXT NOT NULL,
    start_at_utc TEXT NOT NULL,
    duration_minutes INTEGER NOT NULL CHECK (duration_minutes > 0),
    capacity INTEGER NOT NULL CHECK (capacity >= 0 AND capacity <= 30),
    location TEXT,
    play_type TEXT NOT NULL CHECK (play_type IN ('CASUAL', 'TOURNAMENT')),
    tournament_format TEXT CHECK (tournament_format IS NULL OR tournament_format IN ('SWISS_TOP_CUT', 'DOUBLE_ELIMINATION')),
    registration_slug TEXT NOT NULL UNIQUE,
    created_at_utc TEXT NOT NULL,
    deleted_at_utc TEXT,
    FOREIGN KEY (game_id) REFERENCES GAME(id)
);

CREATE TABLE IF NOT EXISTS GAME_CONFIGURATION_OPTION (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id INTEGER NOT NULL,
    key TEXT NOT NULL,
    label TEXT NOT NULL,
    data_type TEXT NOT NULL CHECK (data_type IN ('STRING', 'NUMBER', 'BOOLEAN', 'ENUM')),
    ui_control TEXT NOT NULL CHECK (ui_control IN ('TEXT', 'NUMBER', 'TOGGLE', 'SELECT', 'CHECKBOX_GROUP')),
    default_value TEXT,
    is_required INTEGER NOT NULL DEFAULT 0 CHECK (is_required IN (0, 1)),
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    UNIQUE (game_id, key),
    FOREIGN KEY (game_id) REFERENCES GAME(id)
);

CREATE TABLE IF NOT EXISTS GAME_CONFIGURATION_OPTION_VALUE (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    option_id INTEGER NOT NULL,
    value TEXT NOT NULL,
    label TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    UNIQUE (option_id, value),
    FOREIGN KEY (option_id) REFERENCES GAME_CONFIGURATION_OPTION(id)
);

CREATE TABLE IF NOT EXISTS EVENT_CONFIGURATION_SELECTION (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    event_id INTEGER NOT NULL,
    option_id INTEGER NOT NULL,
    selected_value TEXT NOT NULL,
    UNIQUE (event_id, option_id, selected_value),
    FOREIGN KEY (event_id) REFERENCES EVENT(id),
    FOREIGN KEY (option_id) REFERENCES GAME_CONFIGURATION_OPTION(id)
);

CREATE TABLE IF NOT EXISTS EVENT_REGISTRATION (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    event_id INTEGER NOT NULL,
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    player_tag TEXT,
    registered_at_utc TEXT NOT NULL,
    FOREIGN KEY (event_id) REFERENCES EVENT(id)
);

CREATE INDEX IF NOT EXISTS IX_EVENT_ACTIVE_START
    ON EVENT (deleted_at_utc, start_at_utc);

CREATE INDEX IF NOT EXISTS IX_EVENT_REGISTRATION_EVENT
    ON EVENT_REGISTRATION (event_id);

CREATE INDEX IF NOT EXISTS IX_EVENT_REGISTRATION_NAME
    ON EVENT_REGISTRATION (event_id, lower(trim(first_name)), lower(trim(last_name)));

CREATE INDEX IF NOT EXISTS IX_EVENT_REGISTRATION_TAG
    ON EVENT_REGISTRATION (event_id, lower(trim(player_tag)));
