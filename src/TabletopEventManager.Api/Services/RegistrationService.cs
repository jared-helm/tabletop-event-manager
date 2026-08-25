namespace TabletopEventManager.Api.Services;

/// <summary>Registration service: the public event-context lookup and locked registration write.</summary>
public sealed class RegistrationService
{
    private readonly EventRepository repository;

    public RegistrationService(EventRepository repository)
    {
        this.repository = repository;
    }

    public Task<RegistrationPageContext?> GetRegistrationContextAsync(string slug, CancellationToken cancellationToken) =>
        repository.GetRegistrationContextAsync(slug, cancellationToken);

    public Task<RegistrationResult> RegisterPlayerAsync(string slug, string? firstName, string? lastName, string? playerTag, CancellationToken cancellationToken) =>
        repository.RegisterPlayerAsync(slug, firstName, lastName, playerTag, cancellationToken);
}
