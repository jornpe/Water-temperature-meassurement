using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add common Aspire service defaults (telemetry, health, discovery)
builder.AddServiceDefaults();

// Configure CORS for dev; tighten in prod via env vars
var allowOrigins = builder.Configuration.GetValue<string>("Cors:AllowedOrigins");
if (!string.IsNullOrWhiteSpace(allowOrigins))
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(allowOrigins.Split(';', StringSplitOptions.RemoveEmptyEntries))
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });
}
else
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    });
}

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

app.UseCors();

// Health endpoint returning JSON for tests
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithName("Health");

// Dummy temperatures endpoint
app.MapGet("/api/temperatures", () =>
{
    var rnd = new Random();
    var data = Enumerable.Range(0, 5).Select(i => new
    {
        id = i + 1,
        sensor = $"sensor-{i + 1}",
        celsius = Math.Round(15 + rnd.NextDouble() * 10, 2),
        timestamp = DateTimeOffset.UtcNow
    });
    return Results.Ok(data);
});

// Prefer standard ASP.NET URLs via ASPNETCORE_URLS; default to 8080
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    app.Urls.Add($"http://0.0.0.0:{port}");
}

app.Run();

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
