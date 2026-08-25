using Microsoft.Data.Sqlite;

namespace TabletopEventManager.Api;

public static class DatabaseInitializer
{
    public static void Initialize(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        var connectionBuilder = new SqliteConnectionStringBuilder(connectionString);
        var databasePath = connectionBuilder.DataSource;
        if (!string.IsNullOrWhiteSpace(databasePath) && databasePath != ":memory:")
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        MigrateLegacySchema(connection);
        ExecuteScript(connection, ResolveScriptPath(environment, "schema.sql"));
        ExecuteScript(connection, ResolveScriptPath(environment, "seed-game-templates.sql"));
        ExecuteScript(connection, ResolveScriptPath(environment, "seed-sample-events.sql"));
    }

    private static string ResolveScriptPath(IWebHostEnvironment environment, string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "scripts", fileName),
            Path.Combine(environment.ContentRootPath, "scripts", fileName)
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Database script was not found.", fileName);
    }

    private static void ExecuteScript(SqliteConnection connection, string path)
    {
        using var command = connection.CreateCommand();
        command.CommandText = File.ReadAllText(path);
        command.ExecuteNonQuery();
    }

    private static void MigrateLegacySchema(SqliteConnection connection)
    {
        RenameColumnIfPresent(connection, "Game", "display_name", "DisplayName");
        RenameColumnIfPresent(connection, "Game", "is_active", "IsActive");
        RenameColumnIfPresent(connection, "Game", "created_at_utc", "CreatedAtUtc");

        RenameColumnIfPresent(connection, "Event", "game_id", "GameId");
        RenameColumnIfPresent(connection, "Event", "start_at_utc", "StartAtUtc");
        RenameColumnIfPresent(connection, "Event", "duration_minutes", "DurationMinutes");
        RenameColumnIfPresent(connection, "Event", "play_type", "PlayType");
        RenameColumnIfPresent(connection, "Event", "tournament_format", "TournamentFormat");
        RenameColumnIfPresent(connection, "Event", "registration_slug", "RegistrationSlug");
        RenameColumnIfPresent(connection, "Event", "created_at_utc", "CreatedAtUtc");
        RenameColumnIfPresent(connection, "Event", "deleted_at_utc", "DeletedAtUtc");

        RenameTableIfPresent(connection, "GAME_CONFIGURATION_OPTION", "GameConfigurationOption");
        RenameTableIfPresent(connection, "GAME_CONFIGURATION_OPTION_VALUE", "GameConfigurationOptionValue");
        RenameTableIfPresent(connection, "EVENT_CONFIGURATION_SELECTION", "EventConfigurationSelection");
        RenameTableIfPresent(connection, "EVENT_REGISTRATION", "EventRegistration");

        RenameColumnIfPresent(connection, "GameConfigurationOption", "game_id", "GameId");
        RenameColumnIfPresent(connection, "GameConfigurationOption", "data_type", "DataType");
        RenameColumnIfPresent(connection, "GameConfigurationOption", "ui_control", "UiControl");
        RenameColumnIfPresent(connection, "GameConfigurationOption", "default_value", "DefaultValue");
        RenameColumnIfPresent(connection, "GameConfigurationOption", "is_required", "IsRequired");
        RenameColumnIfPresent(connection, "GameConfigurationOption", "sort_order", "SortOrder");
        RenameColumnIfPresent(connection, "GameConfigurationOption", "is_active", "IsActive");
        RenameColumnIfPresent(connection, "GameConfigurationOptionValue", "option_id", "OptionId");
        RenameColumnIfPresent(connection, "GameConfigurationOptionValue", "sort_order", "SortOrder");
        RenameColumnIfPresent(connection, "GameConfigurationOptionValue", "is_active", "IsActive");
        RenameColumnIfPresent(connection, "EventConfigurationSelection", "event_id", "EventId");
        RenameColumnIfPresent(connection, "EventConfigurationSelection", "option_id", "OptionId");
        RenameColumnIfPresent(connection, "EventConfigurationSelection", "selected_value", "SelectedValue");
        RenameColumnIfPresent(connection, "EventRegistration", "event_id", "EventId");
        RenameColumnIfPresent(connection, "EventRegistration", "first_name", "FirstName");
        RenameColumnIfPresent(connection, "EventRegistration", "last_name", "LastName");
        RenameColumnIfPresent(connection, "EventRegistration", "player_tag", "PlayerTag");
        RenameColumnIfPresent(connection, "EventRegistration", "registered_at_utc", "RegisteredAtUtc");
    }

    private static void RenameTableIfPresent(SqliteConnection connection, string oldName, string newName)
    {
        if (!TableExists(connection, oldName))
        {
            return;
        }

        if (TableExists(connection, newName))
        {
            using var count = connection.CreateCommand();
            count.CommandText = $"SELECT COUNT(*) FROM \"{newName}\"";
            if (Convert.ToInt64(count.ExecuteScalar()) != 0)
            {
                return;
            }

            using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP TABLE \"{newName}\"";
            drop.ExecuteNonQuery();
        }

        using var rename = connection.CreateCommand();
        rename.CommandText = $"ALTER TABLE \"{oldName}\" RENAME TO \"{newName}\"";
        rename.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection, string name)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        check.Parameters.AddWithValue("$name", name);
        return check.ExecuteScalar() is not null;
    }

    private static void RenameColumnIfPresent(SqliteConnection connection, string tableName, string oldName, string newName)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        using var reader = check.ExecuteReader();
        var present = false;
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), oldName, StringComparison.OrdinalIgnoreCase))
            {
                present = true;
                break;
            }
        }

        if (present)
        {
            using var rename = connection.CreateCommand();
            rename.CommandText = $"ALTER TABLE \"{tableName}\" RENAME COLUMN \"{oldName}\" TO \"{newName}\"";
            rename.ExecuteNonQuery();
        }
    }
}
