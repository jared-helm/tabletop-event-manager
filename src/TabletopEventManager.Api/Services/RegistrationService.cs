namespace TabletopEventManager.Api.Services;

/// <summary>Registration service: normalization, duplicate/cutoff/capacity rules, and locked writes.</summary>
public sealed class RegistrationService
{
    private readonly EventRepository repository;
    private readonly EventRegistrationLock registrationLock;

    public RegistrationService(EventRepository repository, EventRegistrationLock registrationLock)
    {
        this.repository = repository;
        this.registrationLock = registrationLock;
    }

    public async Task<RegistrationPageContext?> GetRegistrationContextAsync(string slug, CancellationToken cancellationToken)
    {
        var row = await repository.GetRegistrationContextRowAsync(slug, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var endAtUtc = row.StartAtUtc.AddMinutes(row.DurationMinutes);
        var isClosed = DateTimeOffset.UtcNow >= row.StartAtUtc;
        return new RegistrationPageContext(row.EventName, row.GameName, row.StartAtUtc, endAtUtc, row.Location, row.Capacity, row.RegistrationCount, isClosed);
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

        var eventId = await repository.FindActiveEventIdBySlugAsync(slug, cancellationToken);
        if (eventId is null)
        {
            return RegistrationResult.Unavailable();
        }

        return await registrationLock.RunExclusiveAsync(eventId.Value, async () =>
        {
            await using var work = await repository.BeginRegistrationAsync(eventId.Value, cancellationToken);

            var snapshot = await work.GetEventSnapshotAsync(cancellationToken);
            if (snapshot is null || snapshot.IsDeleted)
            {
                return RegistrationResult.Unavailable();
            }

            if (DateTimeOffset.UtcNow >= snapshot.StartAtUtc)
            {
                return RegistrationResult.Closed();
            }

            if (await work.HasDuplicateAsync(normalizedFirstName, normalizedLastName, normalizedTag, cancellationToken))
            {
                return RegistrationResult.Duplicate();
            }

            var currentCount = await work.CountRegistrationsAsync(cancellationToken);
            if (currentCount >= snapshot.Capacity)
            {
                return RegistrationResult.Full();
            }

            var registeredAtUtc = DateTimeOffset.UtcNow;
            await work.InsertRegistrationAsync(normalizedFirstName, normalizedLastName, normalizedTag, registeredAtUtc, cancellationToken);
            await work.CommitAsync(cancellationToken);

            var confirmation = new RegistrationConfirmation(
                snapshot.EventName, snapshot.GameName, snapshot.StartAtUtc, snapshot.StartAtUtc.AddMinutes(snapshot.DurationMinutes),
                normalizedFirstName, normalizedLastName);
            return RegistrationResult.Success(confirmation);
        }, cancellationToken);
    }
}
