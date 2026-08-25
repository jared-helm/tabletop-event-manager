using Microsoft.Data.Sqlite;

namespace TabletopEventManager.Api;

public sealed class EventRepository
{
    private readonly string connectionString;

    public EventRepository(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
    }

    public async Task<IReadOnlyList<GameSummary>> GetGamesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, code, display_name FROM GAME WHERE is_active = 1 ORDER BY display_name";

        var games = new List<GameSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            games.Add(new GameSummary(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
        }

        return games;
    }

    public async Task<GameConfigurationResponse?> GetConfigurationAsync(long gameId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = new List<GameConfigurationOptionResponse>();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, key, label, data_type, ui_control, default_value, is_required, sort_order
                FROM GAME_CONFIGURATION_OPTION
                WHERE game_id = $gameId AND is_active = 1
                ORDER BY sort_order, id
                """;
            command.Parameters.AddWithValue("$gameId", gameId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                options.Add(new GameConfigurationOptionResponse(
                    reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetBoolean(6), reader.GetInt32(7), []));
            }
        }

        if (options.Count == 0)
        {
            return null;
        }

        for (var index = 0; index < options.Count; index++)
        {
            await using var valueCommand = connection.CreateCommand();
            valueCommand.CommandText = """
                SELECT id, value, label, sort_order
                FROM GAME_CONFIGURATION_OPTION_VALUE
                WHERE option_id = $optionId AND is_active = 1
                ORDER BY sort_order, id
                """;
            valueCommand.Parameters.AddWithValue("$optionId", options[index].Id);
            var values = new List<GameConfigurationValueResponse>();
            await using var reader = await valueCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add(new GameConfigurationValueResponse(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
            }

            options[index] = options[index] with { Values = values };
        }

        return new GameConfigurationResponse(gameId, options);
    }

    public async Task<IReadOnlyList<EventSummary>> GetEventsAsync(int year, int month, CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.id, e.name, e.start_at_utc, e.duration_minutes, e.capacity, e.location,
                   e.play_type, e.tournament_format, e.registration_slug, g.display_name,
                   (SELECT COUNT(*) FROM EVENT_REGISTRATION r WHERE r.event_id = e.id)
            FROM EVENT e
            INNER JOIN GAME g ON g.id = e.game_id
            WHERE e.deleted_at_utc IS NULL
              AND e.start_at_utc >= $startUtc
              AND e.start_at_utc < $endUtc
            ORDER BY e.start_at_utc, e.name
            """;
        command.Parameters.AddWithValue("$startUtc", start.ToString("O"));
        command.Parameters.AddWithValue("$endUtc", end.ToString("O"));

        var events = new List<EventSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var startAtUtc = DateTimeOffset.Parse(reader.GetString(2));
            events.Add(new EventSummary(
                reader.GetInt64(0), reader.GetString(1), startAtUtc, reader.GetInt32(3), reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetInt64(10)));
        }

        return events;
    }

    public async Task<CreateEventResult> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var template = await LoadTemplateAsync(connection, transaction, request.GameId, cancellationToken);
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
        var createdAtUtc = DateTimeOffset.UtcNow;
        var slug = Guid.NewGuid().ToString("N");

        await using var eventCommand = connection.CreateCommand();
        eventCommand.Transaction = transaction;
        eventCommand.CommandText = """
            INSERT INTO EVENT (game_id, name, start_at_utc, duration_minutes, capacity, location,
                               play_type, tournament_format, registration_slug, created_at_utc)
            VALUES ($gameId, $name, $startAtUtc, $duration, $capacity, $location,
                    $playType, $tournamentFormat, $slug, $createdAtUtc)
            RETURNING id
            """;
        eventCommand.Parameters.AddWithValue("$gameId", request.GameId);
        eventCommand.Parameters.AddWithValue("$name", name);
        eventCommand.Parameters.AddWithValue("$startAtUtc", request.StartAtUtc.ToUniversalTime().ToString("O"));
        eventCommand.Parameters.AddWithValue("$duration", duration);
        eventCommand.Parameters.AddWithValue("$capacity", request.Capacity);
        eventCommand.Parameters.AddWithValue("$location", string.IsNullOrWhiteSpace(request.Location) ? DBNull.Value : request.Location.Trim());
        eventCommand.Parameters.AddWithValue("$playType", request.PlayType);
        eventCommand.Parameters.AddWithValue("$tournamentFormat", string.IsNullOrWhiteSpace(request.TournamentFormat) ? DBNull.Value : request.TournamentFormat);
        eventCommand.Parameters.AddWithValue("$slug", slug);
        eventCommand.Parameters.AddWithValue("$createdAtUtc", createdAtUtc.ToString("O"));
        var eventId = (long)(await eventCommand.ExecuteScalarAsync(cancellationToken) ?? throw new InvalidOperationException("Event was not created."));

        foreach (var selection in request.ConfigurationSelections ?? [])
        {
            var option = template.Options.FirstOrDefault(item => item.Key == selection.Key);
            if (option is null || selection.Value is null)
            {
                continue;
            }

            foreach (var value in selection.Value.Distinct(StringComparer.Ordinal))
            {
                await using var selectionCommand = connection.CreateCommand();
                selectionCommand.Transaction = transaction;
                selectionCommand.CommandText = "INSERT INTO EVENT_CONFIGURATION_SELECTION (event_id, option_id, selected_value) VALUES ($eventId, $optionId, $value)";
                selectionCommand.Parameters.AddWithValue("$eventId", eventId);
                selectionCommand.Parameters.AddWithValue("$optionId", option.Id);
                selectionCommand.Parameters.AddWithValue("$value", value);
                await selectionCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        var summary = new EventSummary(eventId, name, request.StartAtUtc.ToUniversalTime(), duration, request.Capacity,
            string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(), request.PlayType, request.TournamentFormat, slug,
            template.GameName, 0);
        return CreateEventResult.Success(summary);
    }

    private static async Task<TemplateData?> LoadTemplateAsync(SqliteConnection connection, SqliteTransaction transaction, long gameId, CancellationToken cancellationToken)
    {
        var gameName = string.Empty;
        await using (var gameCommand = connection.CreateCommand())
        {
            gameCommand.Transaction = transaction;
            gameCommand.CommandText = "SELECT display_name FROM GAME WHERE id = $gameId AND is_active = 1";
            gameCommand.Parameters.AddWithValue("$gameId", gameId);
            gameName = (string?)await gameCommand.ExecuteScalarAsync(cancellationToken) ?? string.Empty;
        }

        if (string.IsNullOrEmpty(gameName))
        {
            return null;
        }

        var options = new List<GameConfigurationOptionResponse>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id, key, label, data_type, ui_control, default_value, is_required, sort_order FROM GAME_CONFIGURATION_OPTION WHERE game_id = $gameId AND is_active = 1";
        command.Parameters.AddWithValue("$gameId", gameId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new GameConfigurationOptionResponse(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetBoolean(6), reader.GetInt32(7), []));
        }

        for (var index = 0; index < options.Count; index++)
        {
            await using var valueCommand = connection.CreateCommand();
            valueCommand.Transaction = transaction;
            valueCommand.CommandText = "SELECT id, value, label, sort_order FROM GAME_CONFIGURATION_OPTION_VALUE WHERE option_id = $optionId AND is_active = 1 ORDER BY sort_order, id";
            valueCommand.Parameters.AddWithValue("$optionId", options[index].Id);
            var values = new List<GameConfigurationValueResponse>();
            await using var valueReader = await valueCommand.ExecuteReaderAsync(cancellationToken);
            while (await valueReader.ReadAsync(cancellationToken))
            {
                values.Add(new GameConfigurationValueResponse(valueReader.GetInt64(0), valueReader.GetString(1), valueReader.GetString(2), valueReader.GetInt32(3)));
            }

            options[index] = options[index] with { Values = values };
        }

        return new TemplateData(gameName, options, options.FirstOrDefault(item => item.Key == "default_duration_minutes")?.DefaultValue,
            int.Parse(options.First(item => item.Key == "minimum_players").DefaultValue!), int.Parse(options.First(item => item.Key == "maximum_players").DefaultValue!));
    }

    private static List<string> ValidateRequest(CreateEventRequest request, TemplateData template)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120) errors.Add("Event name is required and must be 120 characters or fewer.");
        if (request.Capacity < template.MinimumPlayers || request.Capacity > Math.Min(template.MaximumPlayers, 30)) errors.Add($"Capacity must be between {template.MinimumPlayers} and {Math.Min(template.MaximumPlayers, 30)} players.");
        if (request.StartAtUtc == default) errors.Add("A valid start time is required.");
        if (request.PlayType is not ("CASUAL" or "TOURNAMENT")) errors.Add("Play type is invalid.");
        if (request.PlayType == "TOURNAMENT" && request.TournamentFormat is not ("SWISS_TOP_CUT" or "DOUBLE_ELIMINATION")) errors.Add("A valid tournament format is required.");
        if (request.PlayType == "CASUAL" && !string.IsNullOrWhiteSpace(request.TournamentFormat)) errors.Add("Tournament format is only valid for tournament events.");
        var format = request.ConfigurationSelections?.GetValueOrDefault("event_format")?.SingleOrDefault();
        var formatOption = template.Options.FirstOrDefault(item => item.Key == "event_format");
        if (formatOption is null || string.IsNullOrWhiteSpace(format) || !formatOption.Values.Any(value => value.Value == format)) errors.Add("A valid event format is required.");
        return errors;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private sealed record TemplateData(string GameName, List<GameConfigurationOptionResponse> Options, string? DefaultDurationMinutes, int MinimumPlayers, int MaximumPlayers);
}

public sealed record GameSummary(long Id, string Code, string DisplayName);
public sealed record GameConfigurationResponse(long GameId, IReadOnlyList<GameConfigurationOptionResponse> Options);
public sealed record GameConfigurationOptionResponse(long Id, string Key, string Label, string DataType, string UiControl, string? DefaultValue, bool IsRequired, int SortOrder, IReadOnlyList<GameConfigurationValueResponse> Values);
public sealed record GameConfigurationValueResponse(long Id, string Value, string Label, int SortOrder);
public sealed record EventSummary(long Id, string Name, DateTimeOffset StartAtUtc, int DurationMinutes, int Capacity, string? Location, string PlayType, string? TournamentFormat, string RegistrationSlug, string GameName, long RegistrationCount)
{
    public DateTimeOffset EndAtUtc => StartAtUtc.AddMinutes(DurationMinutes);
}
public sealed record CreateEventRequest(string? Name, long GameId, DateTimeOffset StartAtUtc, int Capacity, string? Location, string PlayType, string? TournamentFormat, Dictionary<string, string[]>? ConfigurationSelections);
public sealed record CreateEventResult(bool IsSuccess, string? Error, EventSummary? Event)
{
    public static CreateEventResult Invalid(string error) => new(false, error, null);
    public static CreateEventResult Success(EventSummary @event) => new(true, null, @event);
}
