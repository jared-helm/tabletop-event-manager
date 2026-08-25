using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace TabletopEventManager.Api.IntegrationTests;

public sealed class GameTemplateTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient client;

    public GameTemplateTests(CustomWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetGames_ReturnsTheThreeSeededGames()
    {
        var response = await client.GetAsync("/api/games");
        response.EnsureSuccessStatusCode();

        var games = await response.Content.ReadFromJsonAsync<List<GameSummary>>();

        Assert.NotNull(games);
        Assert.Equal(3, games!.Count);
        Assert.Contains(games, game => game.Code == "mtg");
        Assert.Contains(games, game => game.Code == "pokemon-tcg");
        Assert.Contains(games, game => game.Code == "yugioh-tcg");
    }

    [Fact]
    public async Task GetConfiguration_ForMagicTheGathering_IncludesEventFormatAndTemplateDefaults()
    {
        var games = await client.GetFromJsonAsync<List<GameSummary>>("/api/games");
        var mtg = games!.Single(game => game.Code == "mtg");

        var response = await client.GetAsync($"/api/games/{mtg.Id}/configuration");
        response.EnsureSuccessStatusCode();

        var configuration = await response.Content.ReadFromJsonAsync<GameConfigurationResponse>();

        Assert.NotNull(configuration);
        var formatOption = configuration!.Options.Single(option => option.Key == "event_format");
        Assert.Equal("SELECT", formatOption.UiControl);
        Assert.Contains(formatOption.Values, value => value.Value == "STANDARD");

        var playTypeOption = configuration.Options.Single(option => option.Key == "allowed_play_types");
        Assert.Equal("CHECKBOX_GROUP", playTypeOption.UiControl);

        var durationOption = configuration.Options.Single(option => option.Key == "default_duration_minutes");
        Assert.Equal("120", durationOption.DefaultValue);
    }

    [Fact]
    public async Task GetConfiguration_ForUnknownGame_ReturnsNotFound()
    {
        var response = await client.GetAsync("/api/games/999999/configuration");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
