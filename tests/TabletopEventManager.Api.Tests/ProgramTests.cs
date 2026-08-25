using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TabletopEventManager.Api.Tests;

public sealed class ProgramTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public ProgramTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task RootEndpoint_ReturnsHealthyServiceResponse()
    {
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RootResponse>();

        Assert.NotNull(body);
        Assert.Equal("tabletop-event-manager-api", body.Service);
        Assert.Equal("ok", body.Status);
    }

    private sealed record RootResponse(string Service, string Status);
}
