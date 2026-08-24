# ER Diagram

This ER diagram models event scheduling, template-driven game configuration, and player registration.

```mermaid
erDiagram
    GAME {
        int id PK
        string code UK
        string display_name
        bool is_active
        datetime created_at_utc
    }

    EVENT {
        int id PK
        int game_id FK
        string name
        datetime start_at_utc
        int duration_minutes
        int capacity
        string location
        string play_type "CASUAL|TOURNAMENT"
        string tournament_format "SWISS_TOP_CUT|DOUBLE_ELIMINATION|NULL"
        string registration_slug UK
        datetime created_at_utc
        datetime deleted_at_utc "NULL"
    }

    GAME_CONFIGURATION_OPTION {
        int id PK
        int game_id FK
        string key
        string label
        string data_type "STRING|NUMBER|BOOLEAN|ENUM"
        string ui_control "TEXT|NUMBER|TOGGLE|SELECT|CHECKBOX_GROUP"
        string default_value "NULL"
        bool is_required
        int sort_order
        bool is_active
    }

    GAME_CONFIGURATION_OPTION_VALUE {
        int id PK
        int option_id FK
        string value
        string label
        int sort_order
        bool is_active
    }

    EVENT_CONFIGURATION_SELECTION {
        int id PK
        int event_id FK
        int option_id FK
        string selected_value
    }

    EVENT_REGISTRATION {
        int id PK
        int event_id FK
        string first_name
        string last_name
        string player_tag "NULL"
        datetime registered_at_utc
    }

    GAME ||--o{ EVENT : "has many"
    GAME ||--o{ GAME_CONFIGURATION_OPTION : "defines"
    GAME_CONFIGURATION_OPTION ||--o{ GAME_CONFIGURATION_OPTION_VALUE : "allows"

    EVENT ||--o{ EVENT_CONFIGURATION_SELECTION : "stores"
    GAME_CONFIGURATION_OPTION ||--o{ EVENT_CONFIGURATION_SELECTION : "selected as"

    EVENT ||--o{ EVENT_REGISTRATION : "accepts"
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
