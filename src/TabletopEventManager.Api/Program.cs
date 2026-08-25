using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Text;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using QRCoder;
using IcsCalendar = Ical.Net.Calendar;

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

app.MapGet("/api/events", async (string? startUtc, string? endUtc, TabletopEventManager.Api.EventRepository repository, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(startUtc) || string.IsNullOrWhiteSpace(endUtc)
        || !DateTimeOffset.TryParse(startUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
        || !DateTimeOffset.TryParse(endUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
        || end <= start)
    {
        return Results.BadRequest(new { error = "startUtc and endUtc must be valid ISO 8601 timestamps with startUtc before endUtc." });
    }

    return Results.Ok(await repository.GetEventsAsync(start, end, cancellationToken));
});

app.MapPost("/api/events", async (TabletopEventManager.Api.CreateEventRequest request, TabletopEventManager.Api.EventRepository repository, CancellationToken cancellationToken) =>
{
    var result = await repository.CreateEventAsync(request, cancellationToken);
    return result.IsSuccess ? Results.Created($"/api/events/{result.Event!.Id}", result.Event) : Results.BadRequest(new { error = result.Error });
});

app.MapGet("/api/events/{eventId:long}", async (long eventId, TabletopEventManager.Api.EventRepository repository, CancellationToken cancellationToken) =>
{
    var detail = await repository.GetEventDetailAsync(eventId, cancellationToken);
    return detail is null ? Results.NotFound() : Results.Ok(detail);
});

app.MapDelete("/api/events/{eventId:long}", async (long eventId, TabletopEventManager.Api.EventRepository repository, CancellationToken cancellationToken) =>
{
    var deleted = await repository.DeleteEventAsync(eventId, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

app.MapGet("/api/events/{eventId:long}/registration-resources", async (long eventId, TabletopEventManager.Api.EventRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var detail = await repository.GetEventDetailAsync(eventId, cancellationToken);
    if (detail is null)
    {
        return Results.NotFound();
    }

    var frontendOrigin = (configuration["Frontend:Origin"] ?? "http://localhost:5173").TrimEnd('/');
    var registrationUrl = $"{frontendOrigin}/registration/{detail.RegistrationSlug}";

    using var qrGenerator = new QRCodeGenerator();
    using var qrCodeData = qrGenerator.CreateQrCode(registrationUrl, QRCodeGenerator.ECCLevel.Q);
    var qrCodePng = new PngByteQRCode(qrCodeData).GetGraphic(10);

    return Results.Ok(new
    {
        registrationUrl,
        qrCodeDataUri = $"data:image/png;base64,{Convert.ToBase64String(qrCodePng)}",
    });
});

app.MapGet("/api/events/{eventId:long}/calendar-invite", async (long eventId, TabletopEventManager.Api.EventRepository repository, CancellationToken cancellationToken) =>
{
    var detail = await repository.GetEventDetailAsync(eventId, cancellationToken);
    if (detail is null)
    {
        return Results.NotFound();
    }

    var calendar = new IcsCalendar();
    calendar.Events.Add(new CalendarEvent
    {
        Summary = detail.Name,
        Start = new CalDateTime(detail.StartAtUtc.UtcDateTime, "UTC"),
        End = new CalDateTime(detail.EndAtUtc.UtcDateTime, "UTC"),
        Location = detail.Location,
    });

    var icsBytes = Encoding.UTF8.GetBytes(new CalendarSerializer().SerializeToString(calendar));
    return Results.File(icsBytes, "text/calendar", $"{detail.RegistrationSlug}.ics");
});

app.Run();

public partial class Program;
