using Microsoft.AspNetCore.Mvc;

namespace TabletopEventManager.Api.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult GetRoot() => Ok(new { service = "tabletop-event-manager-api", status = "ok" });

    [HttpGet("/health")]
    public IActionResult GetHealth() => Ok(new { status = "healthy", timestampUtc = DateTime.UtcNow });
}
