# ER Diagram

This ER diagram models event scheduling, template-driven game configuration, and player registration.

```mermaid
erDiagram
    Game {
        int Id PK
        string Code UK
        string DisplayName
        bool IsActive
        datetime CreatedAtUtc
    }

    Event {
        int Id PK
        int GameId FK
        string Name
        datetime StartAtUtc
        int DurationMinutes
        int Capacity
        string Location
        string PlayType "CASUAL|TOURNAMENT"
        string TournamentFormat "SWISS_TOP_CUT|DOUBLE_ELIMINATION|NULL"
        string RegistrationSlug UK
        datetime CreatedAtUtc
        datetime DeletedAtUtc "NULL"
    }

    GameConfigurationOption {
        int Id PK
        int GameId FK
        string Key
        string Label
        string DataType "STRING|NUMBER|BOOLEAN|ENUM"
        string UiControl "TEXT|NUMBER|TOGGLE|SELECT|CHECKBOX_GROUP"
        string DefaultValue "NULL"
        bool IsRequired
        int SortOrder
        bool IsActive
    }

    GameConfigurationOptionValue {
        int Id PK
        int OptionId FK
        string Value
        string Label
        int SortOrder
        bool IsActive
    }

    EventConfigurationSelection {
        int Id PK
        int EventId FK
        int OptionId FK
        string SelectedValue
    }

    EventRegistration {
        int Id PK
        int EventId FK
        string FirstName
        string LastName
        string PlayerTag "NULL"
        datetime RegisteredAtUtc
    }

    Game ||--o{ Event : "has many"
    Game ||--o{ GameConfigurationOption : "defines"
    GameConfigurationOption ||--o{ GameConfigurationOptionValue : "allows"

    Event ||--o{ EventConfigurationSelection : "stores"
    GameConfigurationOption ||--o{ EventConfigurationSelection : "selected as"

    Event ||--o{ EventRegistration : "accepts"
```

## Notes

- An event references exactly one game.
- All database timestamps use UTC and have a `_utc` suffix.
- `duration_minutes` is stored on the event so its calculated end time remains stable if a template changes later.
- `deleted_at_utc` implements soft deletion; non-null values exclude an event from active calendar and registration queries while retaining its history.
- Game configuration options can be mixed controls and data types.
- Event configuration selections snapshot chosen values at event creation.
- A checkbox-group option may have multiple selection rows for the same event and option; enforce uniqueness across `(event_id, option_id, selected_value)`.
- Registration duplicate checks compare trimmed, case-insensitive first/last names together or non-empty player tags.
- Tournament management is out of scope; only tournament format selection is modeled.
- Capacity enforcement and duplicate registration checks are implemented at the API layer using event registration data.
