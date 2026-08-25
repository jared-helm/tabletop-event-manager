namespace TabletopEventManager.Api.Services;

/// <summary>Event service: calendar queries, event creation, detail lookup, and soft deletion.</summary>
public sealed class EventService
{
    private readonly EventRepository repository;

    public EventService(EventRepository repository)
    {
        this.repository = repository;
    }

    public Task<IReadOnlyList<EventSummary>> GetEventsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken) =>
        repository.GetEventsAsync(start, end, cancellationToken);

    public Task<CreateEventResult> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken) =>
        repository.CreateEventAsync(request, cancellationToken);

    public Task<EventDetail?> GetEventDetailAsync(long eventId, CancellationToken cancellationToken) =>
        repository.GetEventDetailAsync(eventId, cancellationToken);

    public Task<bool> DeleteEventAsync(long eventId, CancellationToken cancellationToken) =>
        repository.DeleteEventAsync(eventId, cancellationToken);

    public Task<RegistrationsResponse> GetRegistrationsAsync(long eventId, CancellationToken cancellationToken) =>
        repository.GetRegistrationsAsync(eventId, cancellationToken);
}
