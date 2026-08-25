PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Game (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Code TEXT NOT NULL UNIQUE,
    DisplayName TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    CreatedAtUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Event (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GameId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    StartAtUtc TEXT NOT NULL,
    DurationMinutes INTEGER NOT NULL CHECK (DurationMinutes > 0),
    Capacity INTEGER NOT NULL CHECK (Capacity >= 0 AND Capacity <= 30),
    Location TEXT,
    PlayType TEXT NOT NULL CHECK (PlayType IN ('CASUAL', 'TOURNAMENT')),
    TournamentFormat TEXT CHECK (TournamentFormat IS NULL OR TournamentFormat IN ('SWISS_TOP_CUT', 'DOUBLE_ELIMINATION')),
    RegistrationSlug TEXT NOT NULL UNIQUE,
    CreatedAtUtc TEXT NOT NULL,
    DeletedAtUtc TEXT,
    FOREIGN KEY (GameId) REFERENCES Game(Id)
);

CREATE TABLE IF NOT EXISTS GameConfigurationOption (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GameId INTEGER NOT NULL,
    Key TEXT NOT NULL,
    Label TEXT NOT NULL,
    DataType TEXT NOT NULL CHECK (DataType IN ('STRING', 'NUMBER', 'BOOLEAN', 'ENUM')),
    UiControl TEXT NOT NULL CHECK (UiControl IN ('TEXT', 'NUMBER', 'TOGGLE', 'SELECT', 'CHECKBOX_GROUP')),
    DefaultValue TEXT,
    IsRequired INTEGER NOT NULL DEFAULT 0 CHECK (IsRequired IN (0, 1)),
    SortOrder INTEGER NOT NULL DEFAULT 0,
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    UNIQUE (GameId, Key),
    FOREIGN KEY (GameId) REFERENCES Game(Id)
);

CREATE TABLE IF NOT EXISTS GameConfigurationOptionValue (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OptionId INTEGER NOT NULL,
    Value TEXT NOT NULL,
    Label TEXT NOT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    UNIQUE (OptionId, Value),
    FOREIGN KEY (OptionId) REFERENCES GameConfigurationOption(Id)
);

CREATE TABLE IF NOT EXISTS EventConfigurationSelection (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId INTEGER NOT NULL,
    OptionId INTEGER NOT NULL,
    SelectedValue TEXT NOT NULL,
    UNIQUE (EventId, OptionId, SelectedValue),
    FOREIGN KEY (EventId) REFERENCES Event(Id),
    FOREIGN KEY (OptionId) REFERENCES GameConfigurationOption(Id)
);

CREATE TABLE IF NOT EXISTS EventRegistration (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId INTEGER NOT NULL,
    FirstName TEXT NOT NULL,
    LastName TEXT NOT NULL,
    PlayerTag TEXT,
    RegisteredAtUtc TEXT NOT NULL,
    FOREIGN KEY (EventId) REFERENCES Event(Id)
);

CREATE INDEX IF NOT EXISTS IX_EventActiveStart
    ON Event (DeletedAtUtc, StartAtUtc);

CREATE INDEX IF NOT EXISTS IX_EventRegistrationEvent
    ON EventRegistration (EventId);

CREATE INDEX IF NOT EXISTS IX_EventRegistrationName
    ON EventRegistration (EventId, lower(trim(FirstName)), lower(trim(LastName)));

CREATE INDEX IF NOT EXISTS IX_EventRegistrationTag
    ON EventRegistration (EventId, lower(trim(PlayerTag)));
