using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using TabletopEventManager.Api.Services;

namespace TabletopEventManager.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    private readonly EventService eventService;
    private readonly RegistrationResourceService registrationResourceService;

    public EventsController(EventService eventService, RegistrationResourceService registrationResourceService)
    {
        this.eventService = eventService;
        this.registrationResourceService = registrationResourceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetEvents(string? startUtc, string? endUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(startUtc) || string.IsNullOrWhiteSpace(endUtc)
            || !DateTimeOffset.TryParse(startUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !DateTimeOffset.TryParse(endUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
            || end <= start)
        {
            return BadRequest(new { error = "startUtc and endUtc must be valid ISO 8601 timestamps with startUtc before endUtc." });
        }

        return Ok(await eventService.GetEventsAsync(start, end, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> CreateEvent(CreateEventRequest request, CancellationToken cancellationToken)
    {
        var result = await eventService.CreateEventAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetEvent), new { eventId = result.Event!.Id }, result.Event)
            : BadRequest(new { error = result.Error });
    }

    [HttpGet("{eventId:long}")]
    public async Task<IActionResult> GetEvent(long eventId, CancellationToken cancellationToken)
    {
        var detail = await eventService.GetEventDetailAsync(eventId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpDelete("{eventId:long}")]
    public async Task<IActionResult> DeleteEvent(long eventId, CancellationToken cancellationToken)
    {
        var deleted = await eventService.DeleteEventAsync(eventId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{eventId:long}/registration-resources")]
    public async Task<IActionResult> GetRegistrationResources(long eventId, CancellationToken cancellationToken)
    {
        var resources = await registrationResourceService.GetRegistrationResourcesAsync(eventId, cancellationToken);
        return resources is null ? NotFound() : Ok(resources);
    }

    [HttpGet("{eventId:long}/calendar-invite")]
    public async Task<IActionResult> GetCalendarInvite(long eventId, CancellationToken cancellationToken)
    {
        var invite = await registrationResourceService.GetCalendarInviteAsync(eventId, cancellationToken);
        return invite is null ? NotFound() : File(invite.Content, "text/calendar", invite.FileName);
    }

    [HttpGet("{eventId:long}/registrations")]
    public async Task<IActionResult> GetRegistrations(long eventId, CancellationToken cancellationToken)
    {
        return Ok(await eventService.GetRegistrationsAsync(eventId, cancellationToken));
    }
}
