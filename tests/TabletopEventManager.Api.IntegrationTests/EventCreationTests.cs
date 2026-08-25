using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace TabletopEventManager.Api.IntegrationTests;

public sealed class EventCreationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient client;

    public EventCreationTests(CustomWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    private async Task<long> GetGameIdAsync(string code)
    {
        var games = await client.GetFromJsonAsync<List<GameSummary>>("/api/games");
        return games!.Single(game => game.Code == code).Id;
    }

    [Theory]
    [InlineData("mtg", "STANDARD", 120)]
    [InlineData("pokemon-tcg", "STANDARD", 90)]
    [InlineData("yugioh-tcg", "ADVANCED", 90)]
    public async Task CreateEvent_ForEachSeededGame_SnapshotsTemplateDuration(string gameCode, string eventFormat, int expectedDurationMinutes)
    {
        var gameId = await GetGameIdAsync(gameCode);
        var startAtUtc = DateTimeOffset.UtcNow.AddDays(3);

        var request = new
        {
            name = $"Integration Test Event ({gameCode})",
            gameId,
            startAtUtc,
            capacity = 4,
            playType = "CASUAL",
            tournamentFormat = "",
            configurationSelections = new Dictionary<string, string[]> { ["event_format"] = [eventFormat] },
        };

        var response = await client.PostAsJsonAsync("/api/events", request);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<EventSummary>();

        Assert.NotNull(created);
        Assert.Equal(expectedDurationMinutes, created!.DurationMinutes);
        Assert.Equal(startAtUtc.ToUniversalTime(), created.StartAtUtc.ToUniversalTime());
        Assert.Equal(startAtUtc.ToUniversalTime().AddMinutes(expectedDurationMinutes), created.EndAtUtc.ToUniversalTime());
    }

    [Fact]
    public async Task CreateEvent_WithCapacityBelowTemplateMinimum_ReturnsBadRequest()
    {
        var gameId = await GetGameIdAsync("mtg");
        var request = new
        {
            name = "Too Small",
            gameId,
            startAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            capacity = 1,
            playType = "CASUAL",
            tournamentFormat = "",
            configurationSelections = new Dictionary<string, string[]> { ["event_format"] = ["STANDARD"] },
        };

        var response = await client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithDurationOverride_UsesRequestedDuration()
    {
        var gameId = await GetGameIdAsync("mtg");
        var request = new
        {
            name = "Custom Duration Event",
            gameId,
            startAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            capacity = 8,
            durationMinutes = 75,
            playType = "CASUAL",
            tournamentFormat = "",
            configurationSelections = new Dictionary<string, string[]> { ["event_format"] = ["STANDARD"] },
        };

        var response = await client.PostAsJsonAsync("/api/events", request);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<EventSummary>();

        Assert.NotNull(created);
        Assert.Equal(75, created!.DurationMinutes);
    }

    [Fact]
    public async Task CreateEvent_TournamentWithoutFormat_ReturnsBadRequest()
    {
        var gameId = await GetGameIdAsync("mtg");
        var request = new
        {
            name = "Missing Tournament Format",
            gameId,
            startAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            capacity = 8,
            playType = "TOURNAMENT",
            tournamentFormat = "",
            configurationSelections = new Dictionary<string, string[]> { ["event_format"] = ["STANDARD"] },
        };

        var response = await client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidEventFormat_ReturnsBadRequest()
    {
        var gameId = await GetGameIdAsync("mtg");
        var request = new
        {
            name = "Bad Format",
            gameId,
            startAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            capacity = 8,
            playType = "CASUAL",
            tournamentFormat = "",
            configurationSelections = new Dictionary<string, string[]> { ["event_format"] = ["NOT_A_REAL_FORMAT"] },
        };

        var response = await client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
