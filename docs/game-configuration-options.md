# Game Configuration Options

This document proposes the initial configuration templates for Magic: The Gathering, Pokemon TCG, and Yu-Gi-Oh TCG. These are event-creation settings only; tournament execution, pairings, standings, brackets, and match results are out of scope.

Configuration options are stored using `GAME_CONFIGURATION_OPTION` and `GAME_CONFIGURATION_OPTION_VALUE` from [the ER diagram](er-diagram.md). A `CHECKBOX_GROUP` supports multiple selected values. A `SELECT` supports one selected value.

## Control and Data-Type Conventions

| Data type | UI control | Selection behavior |
| --- | --- | --- |
| `STRING` | `TEXT` | One text value |
| `NUMBER` | `NUMBER` | One numeric value |
| `BOOLEAN` | `TOGGLE` | On or off |
| `ENUM` | `SELECT` | One value from the configured choices |
| `ENUM` | `CHECKBOX_GROUP` | One or more values from the configured choices |

## Shared Options

These options apply to all supported games unless a game template overrides or removes one.

| Key | Label | Type | Control | Required | Values or example |
| --- | --- | --- | --- | --- | --- |
| `allowed_play_types` | Allowed play types | `ENUM` | `CHECKBOX_GROUP` | Yes | `CASUAL`, `TOURNAMENT` |
| `event_format` | Event format | `ENUM` | `SELECT` | Yes | Defined by the game template |
| `default_duration_minutes` | Default duration | `NUMBER` | `NUMBER` | Yes | Template-provided default |
| `minimum_players` | Minimum players | `NUMBER` | `NUMBER` | Yes | Template-provided minimum |
| `maximum_players` | Maximum players | `NUMBER` | `NUMBER` | Yes | Template-provided maximum, capped at 30 by the event requirement |

`allowed_play_types` controls which play types the organizer may choose for an event. The event itself still has one `play_type`: `CASUAL` or `TOURNAMENT`. If the selected play type is `TOURNAMENT`, the event also chooses one tournament format.

## Magic: The Gathering

| Key | Label | Type | Control | Required | Values or example |
| --- | --- | --- | --- | --- | --- |
| `event_format` | Event format | `ENUM` | `SELECT` | Yes | `STANDARD`, `COMMANDER`, `MODERN`, `LIMITED_DRAFT`, `LIMITED_SEALED` |
| `allowed_play_types` | Allowed play types | `ENUM` | `CHECKBOX_GROUP` | Yes | `CASUAL`, `TOURNAMENT` |
| `tournament_format` | Tournament format | `ENUM` | `SELECT` | Conditional | `SWISS_TOP_CUT`, `DOUBLE_ELIMINATION` |
| `default_duration_minutes` | Default duration | `NUMBER` | `NUMBER` | Yes | Varies by event format |
| `minimum_players` | Minimum players | `NUMBER` | `NUMBER` | Yes | Template default, likely 2 |
| `maximum_players` | Maximum players | `NUMBER` | `NUMBER` | Yes | Template default, capped at 30 |

Notes:

- `COMMANDER` is commonly casual, but the template should not force that assumption; organizers may decide which play types are allowed.
- `LIMITED_DRAFT` and `LIMITED_SEALED` may need additional configuration later, such as product type or number of rounds. Those details are omitted from v1 unless required by the event workflow.

## Pokemon TCG

| Key | Label | Type | Control | Required | Values or example |
| --- | --- | --- | --- | --- | --- |
| `event_format` | Event format | `ENUM` | `SELECT` | Yes | `STANDARD`, `EXPANDED`, `LIMITED` |
| `allowed_play_types` | Allowed play types | `ENUM` | `CHECKBOX_GROUP` | Yes | `CASUAL`, `TOURNAMENT` |
| `tournament_format` | Tournament format | `ENUM` | `SELECT` | Conditional | `SWISS_TOP_CUT`, `DOUBLE_ELIMINATION` |
| `default_duration_minutes` | Default duration | `NUMBER` | `NUMBER` | Yes | Varies by event format |
| `minimum_players` | Minimum players | `NUMBER` | `NUMBER` | Yes | Template default, likely 2 |
| `maximum_players` | Maximum players | `NUMBER` | `NUMBER` | Yes | Template default, capped at 30 |

Notes:

- `LIMITED` is included as a placeholder for sealed or draft-style events. Confirm whether it should be split into separate `SEALED` and `DRAFT` values.
- Rotation and legality details should not be entered as free text in v1; they can be added as structured options if the product needs them later.

## Yu-Gi-Oh TCG

| Key | Label | Type | Control | Required | Values or example |
| --- | --- | --- | --- | --- | --- |
| `event_format` | Event format | `ENUM` | `SELECT` | Yes | `ADVANCED`, `TIME_WIZARD`, `TRADITIONAL` |
| `allowed_play_types` | Allowed play types | `ENUM` | `CHECKBOX_GROUP` | Yes | `CASUAL`, `TOURNAMENT` |
| `tournament_format` | Tournament format | `ENUM` | `SELECT` | Conditional | `SWISS_TOP_CUT`, `DOUBLE_ELIMINATION` |
| `default_duration_minutes` | Default duration | `NUMBER` | `NUMBER` | Yes | Varies by event format |
| `minimum_players` | Minimum players | `NUMBER` | `NUMBER` | Yes | Template default, likely 2 |
| `maximum_players` | Maximum players | `NUMBER` | `NUMBER` | Yes | Template default, capped at 30 |

Notes:

- `ADVANCED` is the likely default organized-play format.
- `TIME_WIZARD` and `TRADITIONAL` are included as possible supported formats, but may be unnecessary for the first version.
- Ban-list, card-pool, and ruleset versions are deliberately excluded from this initial model.

## Conditional Behavior

The event-creation UI should apply these rules after a game is selected:

1. Load the selected game's active configuration options.
2. Render each option using its configured `ui_control`.
3. For `CHECKBOX_GROUP`, load all active child values and allow multiple selections.
4. For `SELECT`, load all active child values and allow one selection.
5. Show `tournament_format` only when the event's selected `play_type` is `TOURNAMENT`.
6. Validate required options and template constraints on the server before creating the event.

## Finalized V1 Decisions

- Event formats use the common organized-play examples listed for each game. MTG Commander remains a first-class format because it is a common casual event format.
- Pokemon `LIMITED` remains one v1 option; sealed and draft-specific configuration can be added later.
- Yu-Gi-Oh supports `ADVANCED`, `TIME_WIZARD`, and `TRADITIONAL` in the seeded template.
- Template defaults guide event creation and are copied into the event, but organizers cannot edit template definitions or event properties after creation in v1.
- Allowed play types remain configurable per game template, with both `CASUAL` and `TOURNAMENT` seeded for all three supported games.
- Structured deck, product, ban-list, rotation, and ruleset settings are out of scope for v1.
- Template creation and editing are out of scope for v1. The supported templates are seeded through the scripts in [the scripts folder](../scripts/README.md).
