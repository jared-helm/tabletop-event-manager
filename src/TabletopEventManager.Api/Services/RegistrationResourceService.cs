using System.Text;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using QRCoder;
using IcsCalendar = Ical.Net.Calendar;

namespace TabletopEventManager.Api.Services;

public sealed record RegistrationResources(string RegistrationUrl, string QrCodeDataUri);
public sealed record CalendarInvite(byte[] Content, string FileName);

/// <summary>QR/ICS resource service: builds the shareable registration link, its QR code, and the .ics download.</summary>
public sealed class RegistrationResourceService
{
    private const string CalendarLocation = "Jareds card shop";
    private readonly EventRepository repository;
    private readonly IConfiguration configuration;

    public RegistrationResourceService(EventRepository repository, IConfiguration configuration)
    {
        this.repository = repository;
        this.configuration = configuration;
    }

    public async Task<RegistrationResources?> GetRegistrationResourcesAsync(long eventId, CancellationToken cancellationToken)
    {
        var detail = await repository.GetEventDetailAsync(eventId, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var frontendOrigin = (configuration["Frontend:Origin"] ?? "http://localhost:5173").TrimEnd('/');
        var registrationUrl = $"{frontendOrigin}/registration/{detail.RegistrationSlug}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(registrationUrl, QRCodeGenerator.ECCLevel.Q);
        var qrCodePng = new PngByteQRCode(qrCodeData).GetGraphic(10);

        return new RegistrationResources(registrationUrl, $"data:image/png;base64,{Convert.ToBase64String(qrCodePng)}");
    }

    public async Task<CalendarInvite?> GetCalendarInviteAsync(long eventId, CancellationToken cancellationToken)
    {
        var detail = await repository.GetEventDetailAsync(eventId, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var calendar = new IcsCalendar();
        calendar.Events.Add(new CalendarEvent
        {
            Summary = detail.Name,
            Start = new CalDateTime(detail.StartAtUtc.UtcDateTime, "UTC"),
            End = new CalDateTime(detail.EndAtUtc.UtcDateTime, "UTC"),
            Location = CalendarLocation,
        });

        var icsBytes = Encoding.UTF8.GetBytes(new CalendarSerializer().SerializeToString(calendar));
        return new CalendarInvite(icsBytes, $"{detail.RegistrationSlug}.ics");
    }
}
