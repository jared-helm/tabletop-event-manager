# Database Seed Scripts

This folder contains the initial SQLite seed data for the three supported game templates:

- Magic: The Gathering (`mtg`)
- Pokemon TCG (`pokemon-tcg`)
- Yu-Gi-Oh TCG (`yugioh-tcg`)

Template creation and editing are out of scope for v1. The application should run [seed-game-templates.sql](seed-game-templates.sql) during database initialization or expose it as a one-time setup command.

## Expected Tables

The script targets the tables described in [docs/er-diagram.md](../docs/er-diagram.md):

- `Game`
- `GameConfigurationOption`
- `GameConfigurationOptionValue`

It assumes those tables have already been created and that their identifiers are integer primary keys. The script is safe to run more than once because it uses `INSERT OR IGNORE` and stable natural keys.

## Manual Execution

From the repository root, run:

```text
sqlite3 app.db ".read scripts/seed-game-templates.sql"
```

The exact database filename may differ once the application is implemented.
