using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<TabletopEventManager.Api.EventRepository>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var frontendOrigin = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";
        policy.WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

TabletopEventManager.Api.DatabaseInitializer.Initialize(app.Configuration, app.Environment);

app.UseExceptionHandler();
app.UseCors("Frontend");

app.MapGet("/", () => Results.Ok(new
{
    service = "tabletop-event-manager-api",
    status = "ok"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestampUtc = DateTime.UtcNow
}));

app.MapGet("/api/games", async (TabletopEventManager.Api.EventRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetGamesAsync(cancellationToken)));

app.MapGet("/api/games/{gameId:long}/configuration", async (long gameId, TabletopEventManager.Api.EventRepository repository, CancellationToken cancellationToken) =>
{
    var configuration = await repository.GetConfigurationAsync(gameId, cancellationToken);
    return configuration is null ? Results.NotFound() : Results.Ok(configuration);
});

app.MapGet("/api/events", async (string? month, TabletopEventManager.Api.EventRepository repository, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(month) || !DateTime.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthStart))
    {
        return Results.BadRequest(new { error = "month must use the YYYY-MM format." });
    }

    return Results.Ok(await repository.GetEventsAsync(monthStart.Year, monthStart.Month, cancellationToken));
});

app.MapPost("/api/events", async (TabletopEventManager.Api.CreateEventRequest request, TabletopEventManager.Api.EventRepository repository, CancellationToken cancellationToken) =>
{
    var result = await repository.CreateEventAsync(request, cancellationToken);
    return result.IsSuccess ? Results.Created($"/api/events/{result.Event!.Id}", result.Event) : Results.BadRequest(new { error = result.Error });
});

app.Run();

public partial class Program;
