using System.Text;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Configuration;
using WaterTemperature.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure strongly-typed settings
var corsSettings = builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new CorsSettings();

// Register configuration objects for DI
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<AuthenticationSettings>(builder.Configuration.GetSection(AuthenticationSettings.SectionName));

// Configure CORS for dev; tighten in prod via env vars
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(corsSettings.AllowedOrigin)
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });
}
else
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });
}

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Add controllers
builder.Services.AddControllers();

// Register JWT service
builder.Services.AddSingleton<IJwtService, JwtService>();

// EF Core with PostgreSQL
var connStr = builder.Configuration.GetConnectionString("watertemp");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));

// Retrieve the secret from configuration
var jwtSecret = builder.Configuration["JwtSettings:Secret"];

if (string.IsNullOrEmpty(jwtSecret))
{
    throw new InvalidOperationException("JWT secret is not configured.");
}
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

// JWT Auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

await app.ConfigureDatabaseAsync();

// Health endpoint returning JSON for tests (and readiness)
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithName("Health");

// Use controllers for API endpoints
app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
