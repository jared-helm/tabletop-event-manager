namespace TabletopEventManager.Api.Services;

/// <summary>Event service: calendar queries, event-creation rules, detail lookup, and soft deletion.</summary>
public sealed class EventService
{
    private readonly EventRepository repository;

    public EventService(EventRepository repository)
    {
        this.repository = repository;
    }

    public Task<IReadOnlyList<EventSummary>> GetEventsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken) =>
        repository.GetEventsAsync(start, end, cancellationToken);

    public Task<EventDetail?> GetEventDetailAsync(long eventId, CancellationToken cancellationToken) =>
        repository.GetEventDetailAsync(eventId, cancellationToken);

    public Task<bool> DeleteEventAsync(long eventId, CancellationToken cancellationToken) =>
        repository.DeleteEventAsync(eventId, cancellationToken);

    public Task<RegistrationsResponse> GetRegistrationsAsync(long eventId, CancellationToken cancellationToken) =>
        repository.GetRegistrationsAsync(eventId, cancellationToken);

    public async Task<CreateEventResult> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken)
    {
        var template = await repository.GetGameTemplateAsync(request.GameId, cancellationToken);
        if (template is null)
        {
            return CreateEventResult.Invalid("The selected game is not available.");
        }

        var errors = ValidateRequest(request, template);
        if (errors.Count > 0)
        {
            return CreateEventResult.Invalid(string.Join(" ", errors));
        }

        var duration = int.Parse(template.DefaultDurationMinutes ?? throw new InvalidOperationException("Template duration is required."));
        var name = request.Name?.Trim() ?? string.Empty;
        var location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        var tournamentFormat = string.IsNullOrWhiteSpace(request.TournamentFormat) ? null : request.TournamentFormat;
        var slug = Guid.NewGuid().ToString("N");
        var createdAtUtc = DateTimeOffset.UtcNow;
        var startAtUtc = request.StartAtUtc.ToUniversalTime();

        var selections = new List<(long OptionId, string Value)>();
        foreach (var selection in request.ConfigurationSelections ?? [])
        {
            var option = template.Options.FirstOrDefault(item => item.Key == selection.Key);
            if (option is null || selection.Value is null)
            {
                continue;
            }

            foreach (var value in selection.Value.Distinct(StringComparer.Ordinal))
            {
                selections.Add((option.Id, value));
            }
        }

        var row = new EventInsertRow(request.GameId, name, startAtUtc, duration, request.Capacity, location, request.PlayType, tournamentFormat, slug, createdAtUtc);
        var eventId = await repository.InsertEventAsync(row, selections, cancellationToken);

        var summary = new EventSummary(eventId, name, startAtUtc, duration, request.Capacity, location, request.PlayType, tournamentFormat, slug, template.GameName, 0);
        return CreateEventResult.Success(summary);
    }

    private static List<string> ValidateRequest(CreateEventRequest request, GameTemplate template)
    {
        var errors = new List<string>();
        var minimumPlayers = template.MinimumPlayers ?? 0;
        var maximumPlayers = Math.Min(template.MaximumPlayers ?? 30, 30);

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120) errors.Add("Event name is required and must be 120 characters or fewer.");
        if (request.Capacity < minimumPlayers || request.Capacity > maximumPlayers) errors.Add($"Capacity must be between {minimumPlayers} and {maximumPlayers} players.");
        if (request.StartAtUtc == default) errors.Add("A valid start time is required.");
        if (request.PlayType is not ("CASUAL" or "TOURNAMENT")) errors.Add("Play type is invalid.");
        if (request.PlayType == "TOURNAMENT" && request.TournamentFormat is not ("SWISS_TOP_CUT" or "DOUBLE_ELIMINATION")) errors.Add("A valid tournament format is required.");
        if (request.PlayType == "CASUAL" && !string.IsNullOrWhiteSpace(request.TournamentFormat)) errors.Add("Tournament format is only valid for tournament events.");

        var format = request.ConfigurationSelections?.GetValueOrDefault("event_format")?.SingleOrDefault();
        var formatOption = template.Options.FirstOrDefault(item => item.Key == "event_format");
        if (formatOption is null || string.IsNullOrWhiteSpace(format) || !formatOption.Values.Any(value => value.Value == format)) errors.Add("A valid event format is required.");

        return errors;
    }
}
