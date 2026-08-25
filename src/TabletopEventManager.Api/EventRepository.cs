using Microsoft.Data.Sqlite;

namespace TabletopEventManager.Api;

/// <summary>
/// Pure data-access layer: builds queries, binds parameters, and manages transactions.
/// All business rules (validation, decisions, locking) live in the Services layer.
/// </summary>
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
        var options = await ReadGameOptionsAsync(connection, null, gameId, cancellationToken);
        if (options.Count == 0)
        {
            return null;
        }

        return new GameConfigurationResponse(gameId, options);
    }

    public async Task<GameTemplate?> GetGameTemplateAsync(long gameId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        string? gameName;
        await using (var gameCommand = connection.CreateCommand())
        {
            gameCommand.CommandText = "SELECT display_name FROM GAME WHERE id = $gameId AND is_active = 1";
            gameCommand.Parameters.AddWithValue("$gameId", gameId);
            gameName = (string?)await gameCommand.ExecuteScalarAsync(cancellationToken);
        }

        if (string.IsNullOrEmpty(gameName))
        {
            return null;
        }

        var options = await ReadGameOptionsAsync(connection, null, gameId, cancellationToken);
        var defaultDuration = options.FirstOrDefault(item => item.Key == "default_duration_minutes")?.DefaultValue;
        var minimumPlayers = options.FirstOrDefault(item => item.Key == "minimum_players")?.DefaultValue;
        var maximumPlayers = options.FirstOrDefault(item => item.Key == "maximum_players")?.DefaultValue;

        return new GameTemplate(gameName, options, defaultDuration,
            minimumPlayers is null ? null : int.Parse(minimumPlayers),
            maximumPlayers is null ? null : int.Parse(maximumPlayers));
    }

    private static async Task<List<GameConfigurationOptionResponse>> ReadGameOptionsAsync(
        SqliteConnection connection, SqliteTransaction? transaction, long gameId, CancellationToken cancellationToken)
    {
        var options = new List<GameConfigurationOptionResponse>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
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

        for (var index = 0; index < options.Count; index++)
        {
            await using var valueCommand = connection.CreateCommand();
            valueCommand.Transaction = transaction;
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

        return options;
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

    public async Task<long> InsertEventAsync(EventInsertRow row, IReadOnlyList<(long OptionId, string Value)> configurationSelections, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var eventCommand = connection.CreateCommand();
        eventCommand.Transaction = transaction;
        eventCommand.CommandText = """
            INSERT INTO EVENT (game_id, name, start_at_utc, duration_minutes, capacity, location,
                               play_type, tournament_format, registration_slug, created_at_utc)
            VALUES ($gameId, $name, $startAtUtc, $duration, $capacity, $location,
                    $playType, $tournamentFormat, $slug, $createdAtUtc)
            RETURNING id
            """;
        eventCommand.Parameters.AddWithValue("$gameId", row.GameId);
        eventCommand.Parameters.AddWithValue("$name", row.Name);
        eventCommand.Parameters.AddWithValue("$startAtUtc", row.StartAtUtc.ToString("O"));
        eventCommand.Parameters.AddWithValue("$duration", row.DurationMinutes);
        eventCommand.Parameters.AddWithValue("$capacity", row.Capacity);
        eventCommand.Parameters.AddWithValue("$location", (object?)row.Location ?? DBNull.Value);
        eventCommand.Parameters.AddWithValue("$playType", row.PlayType);
        eventCommand.Parameters.AddWithValue("$tournamentFormat", (object?)row.TournamentFormat ?? DBNull.Value);
        eventCommand.Parameters.AddWithValue("$slug", row.RegistrationSlug);
        eventCommand.Parameters.AddWithValue("$createdAtUtc", row.CreatedAtUtc.ToString("O"));
        var eventId = (long)(await eventCommand.ExecuteScalarAsync(cancellationToken) ?? throw new InvalidOperationException("Event was not created."));

        foreach (var (optionId, value) in configurationSelections)
        {
            await using var selectionCommand = connection.CreateCommand();
            selectionCommand.Transaction = transaction;
            selectionCommand.CommandText = "INSERT INTO EVENT_CONFIGURATION_SELECTION (event_id, option_id, selected_value) VALUES ($eventId, $optionId, $value)";
            selectionCommand.Parameters.AddWithValue("$eventId", eventId);
            selectionCommand.Parameters.AddWithValue("$optionId", optionId);
            selectionCommand.Parameters.AddWithValue("$value", value);
            await selectionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return eventId;
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

    public async Task<RegistrationContextRow?> GetRegistrationContextRowAsync(string slug, CancellationToken cancellationToken)
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

        return new RegistrationContextRow(
            reader.GetString(0), reader.GetString(5), DateTimeOffset.Parse(reader.GetString(1)), reader.GetInt32(2),
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(3), reader.GetInt64(6));
    }

    public async Task<long?> FindActiveEventIdBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM EVENT WHERE registration_slug = $slug AND deleted_at_utc IS NULL";
        command.Parameters.AddWithValue("$slug", slug);
        return (long?)await command.ExecuteScalarAsync(cancellationToken);
    }

    public async Task<RegistrationUnitOfWork> BeginRegistrationAsync(long eventId, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        return new RegistrationUnitOfWork(connection, transaction, eventId);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

/// <summary>
/// Scopes the queries and insert needed to complete one registration attempt inside a single
/// transaction. Callers decide whether to commit; disposing without committing rolls back.
/// </summary>
public sealed class RegistrationUnitOfWork : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly SqliteTransaction transaction;
    private readonly long eventId;

    internal RegistrationUnitOfWork(SqliteConnection connection, SqliteTransaction transaction, long eventId)
    {
        this.connection = connection;
        this.transaction = transaction;
        this.eventId = eventId;
    }

    public async Task<EventRegistrationSnapshot?> GetEventSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT e.name, e.start_at_utc, e.duration_minutes, e.capacity, e.deleted_at_utc, g.display_name
            FROM EVENT e
            INNER JOIN GAME g ON g.id = e.game_id
            WHERE e.id = $eventId
            """;
        command.Parameters.AddWithValue("$eventId", eventId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new EventRegistrationSnapshot(
            !reader.IsDBNull(4), reader.GetString(0), reader.GetString(5),
            DateTimeOffset.Parse(reader.GetString(1)), reader.GetInt32(2), reader.GetInt32(3));
    }

    public async Task<bool> HasDuplicateAsync(string normalizedFirstName, string normalizedLastName, string? normalizedPlayerTag, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*) FROM EVENT_REGISTRATION
            WHERE event_id = $eventId
              AND (
                (lower(trim(first_name)) = lower($firstName) AND lower(trim(last_name)) = lower($lastName))
                OR ($playerTag IS NOT NULL AND lower(trim(player_tag)) = lower($playerTag))
              )
            """;
        command.Parameters.AddWithValue("$eventId", eventId);
        command.Parameters.AddWithValue("$firstName", normalizedFirstName);
        command.Parameters.AddWithValue("$lastName", normalizedLastName);
        command.Parameters.AddWithValue("$playerTag", (object?)normalizedPlayerTag ?? DBNull.Value);
        var count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        return count > 0;
    }

    public async Task<long> CountRegistrationsAsync(CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM EVENT_REGISTRATION WHERE event_id = $eventId";
        command.Parameters.AddWithValue("$eventId", eventId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    public async Task InsertRegistrationAsync(string normalizedFirstName, string normalizedLastName, string? normalizedPlayerTag, DateTimeOffset registeredAtUtc, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO EVENT_REGISTRATION (event_id, first_name, last_name, player_tag, registered_at_utc)
            VALUES ($eventId, $firstName, $lastName, $playerTag, $registeredAtUtc)
            """;
        command.Parameters.AddWithValue("$eventId", eventId);
        command.Parameters.AddWithValue("$firstName", normalizedFirstName);
        command.Parameters.AddWithValue("$lastName", normalizedLastName);
        command.Parameters.AddWithValue("$playerTag", (object?)normalizedPlayerTag ?? DBNull.Value);
        command.Parameters.AddWithValue("$registeredAtUtc", registeredAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        // Disposing an uncommitted transaction rolls it back; a no-op once already committed.
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }
}

public sealed record GameSummary(long Id, string Code, string DisplayName);
public sealed record GameConfigurationResponse(long GameId, IReadOnlyList<GameConfigurationOptionResponse> Options);
public sealed record GameConfigurationOptionResponse(long Id, string Key, string Label, string DataType, string UiControl, string? DefaultValue, bool IsRequired, int SortOrder, IReadOnlyList<GameConfigurationValueResponse> Values);
public sealed record GameConfigurationValueResponse(long Id, string Value, string Label, int SortOrder);
public sealed record GameTemplate(string GameName, IReadOnlyList<GameConfigurationOptionResponse> Options, string? DefaultDurationMinutes, int? MinimumPlayers, int? MaximumPlayers);
public sealed record EventSummary(long Id, string Name, DateTimeOffset StartAtUtc, int DurationMinutes, int Capacity, string? Location, string PlayType, string? TournamentFormat, string RegistrationSlug, string GameName, long RegistrationCount)
{
    public DateTimeOffset EndAtUtc => StartAtUtc.AddMinutes(DurationMinutes);
}
public sealed record EventInsertRow(long GameId, string Name, DateTimeOffset StartAtUtc, int DurationMinutes, int Capacity, string? Location, string PlayType, string? TournamentFormat, string RegistrationSlug, DateTimeOffset CreatedAtUtc);
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
public sealed record RegistrationContextRow(string EventName, string GameName, DateTimeOffset StartAtUtc, int DurationMinutes, string? Location, int Capacity, long RegistrationCount);
public sealed record RegistrationPageContext(string EventName, string GameName, DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc, string? Location, int Capacity, long RegistrationCount, bool IsClosed);
public sealed record RegisterPlayerRequest(string? FirstName, string? LastName, string? PlayerTag);
public sealed record EventRegistrationSnapshot(bool IsDeleted, string EventName, string GameName, DateTimeOffset StartAtUtc, int DurationMinutes, int Capacity);
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
