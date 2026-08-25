using FluentValidation;

namespace TabletopEventManager.Api.Services;

public sealed class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator(GameTemplate template)
    {
        var minimumPlayers = template.MinimumPlayers ?? 0;
        var maximumPlayers = Math.Min(template.MaximumPlayers ?? 30, 30);
        var allowedPlayTypes = GetOptionValues(template, "allowed_play_types");
        var tournamentFormats = GetOptionValues(template, "tournament_format");
        var eventFormats = GetOptionValues(template, "event_format");

        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 120)
            .WithMessage("Event name is required and must be 120 characters or fewer.");

        RuleFor(request => request.Capacity)
            .InclusiveBetween(minimumPlayers, maximumPlayers)
            .WithMessage($"Capacity must be between {minimumPlayers} and {maximumPlayers} players.");

        RuleFor(request => request.DurationMinutes)
            .GreaterThan(0)
            .When(request => request.DurationMinutes.HasValue)
            .WithMessage("Duration must be a positive number of minutes.");

        RuleFor(request => request.StartAtUtc)
            .NotEqual(default(DateTimeOffset))
            .WithMessage("A valid start time is required.");

        RuleFor(request => request.PlayType)
            .Must(playType => allowedPlayTypes.Contains(playType, StringComparer.Ordinal))
            .WithMessage("Play type is invalid.");

        RuleFor(request => request.TournamentFormat)
            .Must((request, tournamentFormat) => request.PlayType != "TOURNAMENT"
                || tournamentFormats.Contains(tournamentFormat, StringComparer.Ordinal))
            .WithMessage("A valid tournament format is required.");

        RuleFor(request => request.TournamentFormat)
            .Must((request, tournamentFormat) => request.PlayType == "TOURNAMENT"
                || string.IsNullOrWhiteSpace(tournamentFormat))
            .WithMessage("Tournament format is only valid for tournament events.");

        RuleFor(request => request.ConfigurationSelections)
            .Must(selections => HasSingleAllowedValue(selections, "event_format", eventFormats))
            .WithMessage("A valid event format is required.");
    }

    private static IReadOnlyList<string> GetOptionValues(GameTemplate template, string key) =>
        template.Options.FirstOrDefault(option => option.Key == key)?.Values
            .Select(value => value.Value)
            .ToArray() ?? [];

    private static bool HasSingleAllowedValue(Dictionary<string, string[]>? selections, string key, IReadOnlyList<string> allowedValues)
    {
        var values = selections?.GetValueOrDefault(key);
        return values is { Length: 1 } && allowedValues.Contains(values[0], StringComparer.Ordinal);
    }
}