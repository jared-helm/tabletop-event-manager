using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;

namespace TabletopEventManager.Api;

public sealed class EventRepository
{
    private readonly string connectionString;

    // In-process locking is sufficient for a single API instance; a multi-instance
    // deployment would need a database-backed lock instead.
    private readonly ConcurrentDictionary<long, SemaphoreSlim> registrationLocks = new();

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

    public async Task<IReadOnlyList<EventSummary>> GetEventsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
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

    public async Task<EventDetail?> GetEventDetailAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        EventDetail? detail = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT e.id, e.name, e.start_at_utc, e.duration_minutes, e.capacity, e.location,
                       e.play_type, e.tournament_format, e.registration_slug, g.display_name, g.code,
                       (SELECT COUNT(*) FROM EVENT_REGISTRATION r WHERE r.event_id = e.id)
                FROM EVENT e
                INNER JOIN GAME g ON g.id = e.game_id
                WHERE e.id = $eventId AND e.deleted_at_utc IS NULL
                """;
            command.Parameters.AddWithValue("$eventId", eventId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var startAtUtc = DateTimeOffset.Parse(reader.GetString(2));
                detail = new EventDetail(
                    reader.GetInt64(0), reader.GetString(1), startAtUtc, reader.GetInt32(3), reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), reader.GetString(9),
                    reader.GetInt64(11), reader.GetString(10), []);
            }
        }

        if (detail is null)
        {
            return null;
        }

        var selections = new Dictionary<string, (string Label, List<string> Values)>();
        await using (var selectionCommand = connection.CreateCommand())
        {
            selectionCommand.CommandText = """
                SELECT option.key, option.label, selection.selected_value
                FROM EVENT_CONFIGURATION_SELECTION selection
                INNER JOIN GAME_CONFIGURATION_OPTION option ON option.id = selection.option_id
                WHERE selection.event_id = $eventId
                ORDER BY option.sort_order, selection.selected_value
                """;
            selectionCommand.Parameters.AddWithValue("$eventId", eventId);
            await using var reader = await selectionCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = reader.GetString(0);
                if (!selections.TryGetValue(key, out var entry))
                {
                    entry = (reader.GetString(1), []);
                    selections[key] = entry;
                }

                entry.Values.Add(reader.GetString(2));
            }
        }

        var configurationSelections = selections
            .Select(pair => new EventConfigurationSelectionResponse(pair.Key, pair.Value.Label, pair.Value.Values))
            .ToList();
        return detail with { ConfigurationSelections = configurationSelections };
    }

    public async Task<bool> DeleteEventAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE EVENT SET deleted_at_utc = $deletedAtUtc WHERE id = $eventId AND deleted_at_utc IS NULL";
        command.Parameters.AddWithValue("$deletedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$eventId", eventId);
        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    public async Task<RegistrationsResponse> GetRegistrationsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT first_name, last_name, player_tag
            FROM EVENT_REGISTRATION
            WHERE event_id = $eventId
            ORDER BY registered_at_utc
            """;
        command.Parameters.AddWithValue("$eventId", eventId);

        var players = new List<PlayerRegistration>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            players.Add(new PlayerRegistration(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return new RegistrationsResponse(players.Count, players);
    }

    public async Task<RegistrationPageContext?> GetRegistrationContextAsync(string slug, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.name, e.start_at_utc, e.duration_minutes, e.capacity, e.location, g.display_name,
                   (SELECT COUNT(*) FROM EVENT_REGISTRATION r WHERE r.event_id = e.id)
            FROM EVENT e
            INNER JOIN GAME g ON g.id = e.game_id
            WHERE e.registration_slug = $slug AND e.deleted_at_utc IS NULL
            """;
        command.Parameters.AddWithValue("$slug", slug);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var startAtUtc = DateTimeOffset.Parse(reader.GetString(1));
        var durationMinutes = reader.GetInt32(2);
        return new RegistrationPageContext(
            reader.GetString(0), reader.GetString(5), startAtUtc, startAtUtc.AddMinutes(durationMinutes),
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(3), reader.GetInt64(6),
            DateTimeOffset.UtcNow >= startAtUtc);
    }

    public async Task<RegistrationResult> RegisterPlayerAsync(string slug, string? firstName, string? lastName, string? playerTag, CancellationToken cancellationToken)
    {
        var normalizedFirstName = (firstName ?? string.Empty).Trim();
        var normalizedLastName = (lastName ?? string.Empty).Trim();
        var normalizedTag = string.IsNullOrWhiteSpace(playerTag) ? null : playerTag.Trim();

        if (normalizedFirstName.Length is 0 or > 60 || normalizedLastName.Length is 0 or > 60 || normalizedTag is { Length: > 60 })
        {
            return RegistrationResult.Invalid("First name and last name are required and must be 60 characters or fewer.");
        }

        long eventId;
        await using (var lookupConnection = await OpenConnectionAsync(cancellationToken))
        await using (var lookupCommand = lookupConnection.CreateCommand())
        {
            lookupCommand.CommandText = "SELECT id FROM EVENT WHERE registration_slug = $slug AND deleted_at_utc IS NULL";
            lookupCommand.Parameters.AddWithValue("$slug", slug);
            var value = await lookupCommand.ExecuteScalarAsync(cancellationToken);
            if (value is null)
            {
                return RegistrationResult.Unavailable();
            }

            eventId = (long)value;
        }

        var eventLock = registrationLocks.GetOrAdd(eventId, _ => new SemaphoreSlim(1, 1));
        await eventLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            string eventName;
            string gameName;
            DateTimeOffset startAtUtc;
            int durationMinutes;
            int capacity;
            await using (var eventCommand = connection.CreateCommand())
            {
                eventCommand.Transaction = transaction;
                eventCommand.CommandText = """
                    SELECT e.name, e.start_at_utc, e.duration_minutes, e.capacity, e.deleted_at_utc, g.display_name
                    FROM EVENT e
                    INNER JOIN GAME g ON g.id = e.game_id
                    WHERE e.id = $eventId
                    """;
                eventCommand.Parameters.AddWithValue("$eventId", eventId);
                await using var reader = await eventCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken) || !reader.IsDBNull(4))
                {
                    return RegistrationResult.Unavailable();
                }

                eventName = reader.GetString(0);
                startAtUtc = DateTimeOffset.Parse(reader.GetString(1));
                durationMinutes = reader.GetInt32(2);
                capacity = reader.GetInt32(3);
                gameName = reader.GetString(5);
            }

            if (DateTimeOffset.UtcNow >= startAtUtc)
            {
                return RegistrationResult.Closed();
            }

            await using (var duplicateCommand = connection.CreateCommand())
            {
                duplicateCommand.Transaction = transaction;
                duplicateCommand.CommandText = """
                    SELECT COUNT(*) FROM EVENT_REGISTRATION
                    WHERE event_id = $eventId
                      AND (
                        (lower(trim(first_name)) = lower($firstName) AND lower(trim(last_name)) = lower($lastName))
                        OR ($playerTag IS NOT NULL AND lower(trim(player_tag)) = lower($playerTag))
                      )
                    """;
                duplicateCommand.Parameters.AddWithValue("$eventId", eventId);
                duplicateCommand.Parameters.AddWithValue("$firstName", normalizedFirstName);
                duplicateCommand.Parameters.AddWithValue("$lastName", normalizedLastName);
                duplicateCommand.Parameters.AddWithValue("$playerTag", (object?)normalizedTag ?? DBNull.Value);
                var duplicateCount = (long)(await duplicateCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
                if (duplicateCount > 0)
                {
                    return RegistrationResult.Duplicate();
                }
            }

            await using (var countCommand = connection.CreateCommand())
            {
                countCommand.Transaction = transaction;
                countCommand.CommandText = "SELECT COUNT(*) FROM EVENT_REGISTRATION WHERE event_id = $eventId";
                countCommand.Parameters.AddWithValue("$eventId", eventId);
                var currentCount = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
                if (currentCount >= capacity)
                {
                    return RegistrationResult.Full();
                }
            }

            await using (var insertCommand = connection.CreateCommand())
            {
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = """
                    INSERT INTO EVENT_REGISTRATION (event_id, first_name, last_name, player_tag, registered_at_utc)
                    VALUES ($eventId, $firstName, $lastName, $playerTag, $registeredAtUtc)
                    """;
                insertCommand.Parameters.AddWithValue("$eventId", eventId);
                insertCommand.Parameters.AddWithValue("$firstName", normalizedFirstName);
                insertCommand.Parameters.AddWithValue("$lastName", normalizedLastName);
                insertCommand.Parameters.AddWithValue("$playerTag", (object?)normalizedTag ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("$registeredAtUtc", DateTimeOffset.UtcNow.ToString("O"));
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return RegistrationResult.Success(new RegistrationConfirmation(eventName, gameName, startAtUtc, startAtUtc.AddMinutes(durationMinutes), normalizedFirstName, normalizedLastName));
        }
        finally
        {
            eventLock.Release();
        }
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
public sealed record EventConfigurationSelectionResponse(string Key, string Label, IReadOnlyList<string> Values);
public sealed record EventDetail(long Id, string Name, DateTimeOffset StartAtUtc, int DurationMinutes, int Capacity, string? Location, string PlayType, string? TournamentFormat, string RegistrationSlug, string GameName, long RegistrationCount, string GameCode, IReadOnlyList<EventConfigurationSelectionResponse> ConfigurationSelections)
{
    public DateTimeOffset EndAtUtc => StartAtUtc.AddMinutes(DurationMinutes);
}
public sealed record CreateEventRequest(string? Name, long GameId, DateTimeOffset StartAtUtc, int Capacity, string? Location, string PlayType, string? TournamentFormat, Dictionary<string, string[]>? ConfigurationSelections);
public sealed record CreateEventResult(bool IsSuccess, string? Error, EventSummary? Event)
{
    public static CreateEventResult Invalid(string error) => new(false, error, null);
    public static CreateEventResult Success(EventSummary @event) => new(true, null, @event);
}
public sealed record PlayerRegistration(string FirstName, string LastName, string? PlayerTag);
public sealed record RegistrationsResponse(long TotalCount, IReadOnlyList<PlayerRegistration> Players);
public sealed record RegistrationPageContext(string EventName, string GameName, DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc, string? Location, int Capacity, long RegistrationCount, bool IsClosed);
public sealed record RegisterPlayerRequest(string? FirstName, string? LastName, string? PlayerTag);
public sealed record RegistrationConfirmation(string EventName, string GameName, DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc, string FirstName, string LastName);
public enum RegistrationOutcome { Success, Invalid, Unavailable, Closed, Duplicate, Full }
public sealed record RegistrationResult(RegistrationOutcome Outcome, string? Error, RegistrationConfirmation? Confirmation)
{
    public static RegistrationResult Success(RegistrationConfirmation confirmation) => new(RegistrationOutcome.Success, null, confirmation);
    public static RegistrationResult Invalid(string error) => new(RegistrationOutcome.Invalid, error, null);
    public static RegistrationResult Unavailable() => new(RegistrationOutcome.Unavailable, "This event is no longer available.", null);
    public static RegistrationResult Closed() => new(RegistrationOutcome.Closed, "Registration has closed for this event.", null);
    public static RegistrationResult Duplicate() => new(RegistrationOutcome.Duplicate, "Someone with that registration info has already registered.", null);
    public static RegistrationResult Full() => new(RegistrationOutcome.Full, "This event is full.", null);
}
