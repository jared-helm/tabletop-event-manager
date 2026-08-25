using System.Text.Json;
using System.Text.Json.Serialization;
using TabletopEventManager.Api;
using TabletopEventManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<EventRepository>();
builder.Services.AddSingleton<GameTemplateService>();
builder.Services.AddSingleton<EventService>();
builder.Services.AddSingleton<RegistrationService>();
builder.Services.AddSingleton<RegistrationResourceService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var frontendOrigin = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";
        policy.WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

TabletopEventManager.Api.DatabaseInitializer.Initialize(app.Configuration, app.Environment);

app.UseExceptionHandler();
app.UseCors("Frontend");

app.MapControllers();

app.Run();

public partial class Program;
