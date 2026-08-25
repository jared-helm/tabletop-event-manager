namespace TabletopEventManager.Api.Services;

/// <summary>Template service: exposes seeded games and their per-game configuration.</summary>
public sealed class GameTemplateService
{
    private readonly EventRepository repository;

    public GameTemplateService(EventRepository repository)
    {
        this.repository = repository;
    }

    public Task<IReadOnlyList<GameSummary>> GetGamesAsync(CancellationToken cancellationToken) =>
        repository.GetGamesAsync(cancellationToken);

    public Task<GameConfigurationResponse?> GetConfigurationAsync(long gameId, CancellationToken cancellationToken) =>
        repository.GetConfigurationAsync(gameId, cancellationToken);
}
