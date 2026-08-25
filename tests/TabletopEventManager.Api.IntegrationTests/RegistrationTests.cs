using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Xunit;

namespace TabletopEventManager.Api.IntegrationTests;

public sealed class RegistrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory factory;
    private readonly HttpClient client;

    public RegistrationTests(CustomWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    private async Task<EventSummary> CreateEventAsync(int capacity, DateTimeOffset startAtUtc)
    {
        var games = await client.GetFromJsonAsync<List<GameSummary>>("/api/games");
        var gameId = games!.Single(game => game.Code == "mtg").Id;

        var request = new
        {
            name = $"Registration Test Event {Guid.NewGuid():N}",
            gameId,
            startAtUtc,
            capacity,
            playType = "CASUAL",
            tournamentFormat = "",
            configurationSelections = new Dictionary<string, string[]> { ["event_format"] = ["STANDARD"] },
        };

        var response = await client.PostAsJsonAsync("/api/events", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventSummary>())!;
    }

    /// <summary>
    /// Bypasses event-creation's template minimum-player validation to produce a
    /// genuine capacity-1 event, needed to exercise the last-seat race condition.
    /// </summary>
    private async Task<string> CreateCapacityOneEventDirectlyAsync()
    {
        var slug = $"capacity-one-{Guid.NewGuid():N}";
        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Event (GameId, Name, StartAtUtc, DurationMinutes, Capacity, PlayType, TournamentFormat, RegistrationSlug, CreatedAtUtc)
            SELECT Id, 'Capacity One Event', $startAtUtc, 60, 1, 'CASUAL', NULL, $slug, $createdAtUtc
            FROM Game WHERE Code = 'mtg'
            """;
        command.Parameters.AddWithValue("$startAtUtc", DateTimeOffset.UtcNow.AddDays(2).ToString("O"));
        command.Parameters.AddWithValue("$slug", slug);
        command.Parameters.AddWithValue("$createdAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
        return slug;
    }

    [Fact]
    public async Task Register_FirstPlayer_Succeeds_AndAppearsInPlayersList()
    {
        var created = await CreateEventAsync(capacity: 6, startAtUtc: DateTimeOffset.UtcNow.AddDays(3));

        var response = await client.PostAsJsonAsync($"/api/registration/{created.RegistrationSlug}",
            new { firstName = "Ada", lastName = "Lovelace", playerTag = "AdaL" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var players = await client.GetFromJsonAsync<RegistrationsResponse>($"/api/events/{created.Id}/registrations");
        Assert.Equal(1, players!.TotalCount);
        Assert.Contains(players.Players, player => player.FirstName == "Ada" && player.LastName == "Lovelace");
    }

    [Fact]
    public async Task Register_DuplicateName_IsRejected_CaseInsensitiveAndTrimmed()
    {
        var created = await CreateEventAsync(capacity: 6, startAtUtc: DateTimeOffset.UtcNow.AddDays(3));

        await client.PostAsJsonAsync($"/api/registration/{created.RegistrationSlug}",
            new { firstName = "  Bob ", lastName = " SMITH", playerTag = "" });

        var response = await client.PostAsJsonAsync($"/api/registration/{created.RegistrationSlug}",
            new { firstName = "BOB", lastName = "smith", playerTag = "" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("Someone with that registration info has already registered.", body!.Error);
    }

    [Fact]
    public async Task Register_DuplicatePlayerTag_IsRejected_EvenWithDifferentName()
    {
        var created = await CreateEventAsync(capacity: 6, startAtUtc: DateTimeOffset.UtcNow.AddDays(3));

        await client.PostAsJsonAsync($"/api/registration/{created.RegistrationSlug}",
            new { firstName = "Carl", lastName = "Jones", playerTag = "SharedTag" });

        var response = await client.PostAsJsonAsync($"/api/registration/{created.RegistrationSlug}",
            new { firstName = "Dana", lastName = "Lee", playerTag = "sharedtag" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WhenEventIsFull_ReturnsConflict()
    {
        var created = await CreateEventAsync(capacity: 2, startAtUtc: DateTimeOffset.UtcNow.AddDays(3));

        await client.PostAsJsonAsync($"/api/registration/{created.RegistrationSlug}", new { firstName = "Player1", lastName = "Test", playerTag = "" });
        await client.PostAsJsonAsync($"/api/registration/{created.RegistrationSlug}", new { firstName = "Player2", lastName = "Test", playerTag = "" });

        var response = await client.PostAsJsonAsync($"/api/registration/{created.RegistrationSlug}",
            new { firstName = "Player3", lastName = "Test", playerTag = "" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("This event is full.", body!.Error);
    }

    [Fact]
    public async Task Register_AfterEventHasStarted_IsRejected()
    {
        var created = await CreateEventAsync(capacity: 6, startAtUtc: DateTimeOffset.UtcNow.AddMinutes(-5));

        var response = await client.PostAsJsonAsync($"/api/registration/{created.RegistrationSlug}",
            new { firstName = "Late", lastName = "Comer", playerTag = "" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("Registration has closed for this event.", body!.Error);
    }

    [Fact]
    public async Task Register_UnknownSlug_ReturnsNotFound()
    {
        var response = await client.PostAsJsonAsync("/api/registration/does-not-exist",
            new { firstName = "Nobody", lastName = "Here", playerTag = "" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_ConcurrentRequestsForLastSeat_OnlyOneSucceeds()
    {
        var slug = await CreateCapacityOneEventDirectlyAsync();

        var tasks = Enumerable.Range(0, 10).Select(i => client.PostAsJsonAsync(
            $"/api/registration/{slug}",
            new { firstName = $"Player{i}", lastName = "Concurrent", playerTag = "" }));

        var responses = await Task.WhenAll(tasks);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(9, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
    }

    private sealed record ErrorBody(string Error);
}
