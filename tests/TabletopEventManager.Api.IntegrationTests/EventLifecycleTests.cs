using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Xunit;

namespace TabletopEventManager.Api.IntegrationTests;

public sealed class EventLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory factory;
    private readonly HttpClient client;

    public EventLifecycleTests(CustomWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    private async Task<EventSummary> CreateEventAsync()
    {
        var games = await client.GetFromJsonAsync<List<GameSummary>>("/api/games");
        var gameId = games!.Single(game => game.Code == "mtg").Id;

        var request = new
        {
            name = "Lifecycle Test Event",
            gameId,
            startAtUtc = DateTimeOffset.UtcNow.AddDays(5),
            capacity = 6,
            playType = "CASUAL",
            tournamentFormat = "",
            configurationSelections = new Dictionary<string, string[]> { ["event_format"] = ["STANDARD"] },
        };

        var response = await client.PostAsJsonAsync("/api/events", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventSummary>())!;
    }

    [Fact]
    public async Task DeleteEvent_SoftDeletes_AndExcludesFromActiveViews_ButRetainsRow()
    {
        var created = await CreateEventAsync();

        var deleteResponse = await client.DeleteAsync($"/api/events/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var repeatDelete = await client.DeleteAsync($"/api/events/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, repeatDelete.StatusCode);

        var getDetail = await client.GetAsync($"/api/events/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDetail.StatusCode);

        var start = created.StartAtUtc.AddDays(-1);
        var end = created.StartAtUtc.AddDays(1);
        var activeEvents = await client.GetFromJsonAsync<List<EventSummary>>(
            $"/api/events?startUtc={Uri.EscapeDataString(start.ToString("O"))}&endUtc={Uri.EscapeDataString(end.ToString("O"))}");
        Assert.DoesNotContain(activeEvents!, e => e.Id == created.Id);

        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT deleted_at_utc FROM EVENT WHERE id = $id";
        command.Parameters.AddWithValue("$id", created.Id);
        var deletedAtUtc = await command.ExecuteScalarAsync();

        Assert.NotNull(deletedAtUtc);
        Assert.IsNotType<DBNull>(deletedAtUtc);
    }

    [Fact]
    public async Task CalendarInvite_UsesStaticCardShopLocation()
    {
        var created = await CreateEventAsync();

        var response = await client.GetAsync($"/api/events/{created.Id}/calendar-invite");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ics = await response.Content.ReadAsStringAsync();
        Assert.Contains("LOCATION:Jareds card shop", ics);
    }
}
