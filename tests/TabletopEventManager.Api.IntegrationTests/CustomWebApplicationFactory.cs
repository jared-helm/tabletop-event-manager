using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TabletopEventManager.Api.IntegrationTests;

/// <summary>
/// Boots the real API pipeline (routing, DatabaseInitializer, seed scripts) against a
/// private temp SQLite file so each test class gets an isolated, fully-seeded database.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"tabletop-event-manager-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={DatabasePath}",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Pooled native SQLite handles can keep the file locked briefly after disposal.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var path in new[] { DatabasePath, $"{DatabasePath}-shm", $"{DatabasePath}-wal" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup; the OS will reclaim temp files eventually.
            }
        }
    }
}
