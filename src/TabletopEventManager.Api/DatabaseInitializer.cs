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
        ExecuteScript(connection, ResolveScriptPath(environment, "schema.sql"));
        ExecuteScript(connection, ResolveScriptPath(environment, "seed-game-templates.sql"));
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
}
