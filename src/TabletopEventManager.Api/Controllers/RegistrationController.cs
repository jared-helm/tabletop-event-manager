using Microsoft.AspNetCore.Mvc;
using TabletopEventManager.Api.Services;

namespace TabletopEventManager.Api.Controllers;

[ApiController]
[Route("api/registration")]
public sealed class RegistrationController : ControllerBase
{
    private readonly RegistrationService registrationService;

    public RegistrationController(RegistrationService registrationService)
    {
        this.registrationService = registrationService;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetRegistrationContext(string slug, CancellationToken cancellationToken)
    {
        var context = await registrationService.GetRegistrationContextAsync(slug, cancellationToken);
        return context is null ? NotFound(new { error = "This event is no longer available." }) : Ok(context);
    }

    [HttpPost("{slug}")]
    public async Task<IActionResult> Register(string slug, RegisterPlayerRequest request, CancellationToken cancellationToken)
    {
        var result = await registrationService.RegisterPlayerAsync(slug, request.FirstName, request.LastName, request.PlayerTag, cancellationToken);
        return result.Outcome switch
        {
            RegistrationOutcome.Success => CreatedAtAction(nameof(GetRegistrationContext), new { slug }, result.Confirmation),
            RegistrationOutcome.Invalid => BadRequest(new { error = result.Error }),
            RegistrationOutcome.Unavailable => NotFound(new { error = result.Error }),
            _ => Conflict(new { error = result.Error }),
        };
    }
}
