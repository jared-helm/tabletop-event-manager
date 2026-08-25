using Microsoft.AspNetCore.Mvc;
using TabletopEventManager.Api.Services;

namespace TabletopEventManager.Api.Controllers;

[ApiController]
[Route("api/games")]
public sealed class GamesController : ControllerBase
{
    private readonly GameTemplateService gameTemplateService;

    public GamesController(GameTemplateService gameTemplateService)
    {
        this.gameTemplateService = gameTemplateService;
    }

    [HttpGet]
    public async Task<IActionResult> GetGames(CancellationToken cancellationToken)
    {
        return Ok(await gameTemplateService.GetGamesAsync(cancellationToken));
    }

    [HttpGet("{gameId:long}/configuration")]
    public async Task<IActionResult> GetConfiguration(long gameId, CancellationToken cancellationToken)
    {
        var configuration = await gameTemplateService.GetConfigurationAsync(gameId, cancellationToken);
        return configuration is null ? NotFound() : Ok(configuration);
    }
}
