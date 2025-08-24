using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Note: Aspire service defaults are available during local dev via the servicedefaults project.
// For container builds, keep the API self-contained to avoid cross-project Docker context issues.

// Configure strongly-typed settings
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
var authSettings = builder.Configuration.GetSection(AuthenticationSettings.SectionName).Get<AuthenticationSettings>() ?? new AuthenticationSettings();
var corsSettings = builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new CorsSettings();
var dbSettings = builder.Configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>() ?? new DatabaseSettings();

// Register configuration objects for DI
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<AuthenticationSettings>(builder.Configuration.GetSection(AuthenticationSettings.SectionName));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection(CorsSettings.SectionName));
builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName));

// Configure CORS for dev; tighten in prod via env vars
if (!string.IsNullOrWhiteSpace(corsSettings.AllowedOrigins))
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(corsSettings.AllowedOrigins.Split(';', StringSplitOptions.RemoveEmptyEntries))
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

// EF Core with PostgreSQL
var connStr = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connStr))
{
    // Sensible default for local dev (docker-compose)
    connStr = "Host=localhost;Port=5432;Database=watertemp;Username=app;Password=appsecret";
}
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));

// JWT Auth
// Allow plain UTF-8 secrets or base64-encoded secrets using prefix: "base64:<b64>"
var jwtSecretRaw = jwtSettings.Secret;
if (string.IsNullOrWhiteSpace(jwtSecretRaw))
{
    throw new InvalidOperationException("JWT secret is not configured. Set 'JwtSettings:Secret' in configuration.");
}

byte[] keyBytes;
if (jwtSecretRaw.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
{
    var b64 = jwtSecretRaw.Substring("base64:".Length);
    try
    {
        keyBytes = Convert.FromBase64String(b64);
    }
    catch (FormatException)
    {
        throw new InvalidOperationException("JwtSettings:Secret is prefixed with 'base64:' but is not valid Base64.");
    }
}
else
{
    keyBytes = Encoding.UTF8.GetBytes(jwtSecretRaw);
}

// Enforce minimum size to avoid runtime IDX10720 for HS256
if (keyBytes.Length < 32) // 256 bits
{
    throw new InvalidOperationException($"JWT secret too short: {keyBytes.Length * 8} bits. Provide at least 256 bits (32 bytes). Update 'JwtSettings:Secret' or use 'base64:<secret>'.");
}

var key = new SymmetricSecurityKey(keyBytes);
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
            ClockSkew = TimeSpan.FromSeconds(jwtSettings.ClockSkewSeconds)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Ensure database exists only if enabled (with basic retry while Postgres starts)
if (dbSettings.AutoCreate)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var attempt = 0;
    var maxAttempts = 10;
    Exception? last = null;
    while (attempt < maxAttempts)
    {
        try
        {
            db.Database.EnsureCreated();
            last = null;
            break;
        }
        catch (Exception ex)
        {
            last = ex;
            await Task.Delay(TimeSpan.FromSeconds(1));
            attempt++;
        }
    }
    if (last is not null)
    {
        throw last;
    }
}

// Health endpoint returning JSON for tests (and readiness)
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

// Auth endpoints
app.MapGet("/api/auth/users/exists", async (AppDbContext db) =>
{
    var count = await db.Users.CountAsync();
    return Results.Ok(new { exists = count > 0 });
});

app.MapPost("/api/auth/register", async (AppDbContext db, RegisterRequest req) =>
{
    // Only allow when no user exists
    if (await db.Users.AnyAsync())
        return Results.Conflict(new { message = "User already exists" });
    
    // Enhanced validation
    if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { message = "Username and password required" });
    
    // Password strength validation
    if (req.Password.Length < authSettings.MinPasswordLength)
        return Results.BadRequest(new { message = $"Password must be at least {authSettings.MinPasswordLength} characters long" });
    
    // Username length validation
    var username = req.UserName.Trim();
    if (username.Length < 3 || username.Length > 50)
        return Results.BadRequest(new { message = "Username must be between 3-50 characters" });

    // Email validation (if provided)
    if (!string.IsNullOrWhiteSpace(req.Email) && !IsValidEmail(req.Email))
        return Results.BadRequest(new { message = "Invalid email format" });

    var hash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: authSettings.BcryptWorkFactor);
    var user = new User 
    { 
        UserName = username, 
        PasswordHash = hash,
        Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim(),
        FirstName = string.IsNullOrWhiteSpace(req.FirstName) ? null : req.FirstName.Trim(),
        LastName = string.IsNullOrWhiteSpace(req.LastName) ? null : req.LastName.Trim()
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/auth/users/{user.Id}", new { id = user.Id, userName = user.UserName });
});

app.MapPost("/api/auth/login", async (AppDbContext db, LoginRequest req) =>
{
    // Add basic rate limiting delay
    if (authSettings.LoginDelayMs > 0)
        await Task.Delay(authSettings.LoginDelayMs);
    
    var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == req.UserName);
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var expiry = DateTime.UtcNow.AddHours(jwtSettings.TokenLifetimeHours);
    var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
        claims: new[] { 
            new System.Security.Claims.Claim("sub", user.Id.ToString()), 
            new System.Security.Claims.Claim("name", user.UserName),
            new System.Security.Claims.Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), System.Security.Claims.ClaimValueTypes.Integer64)
        },
        expires: expiry,
        signingCredentials: creds
    );
    var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(jwt);
    var profile = new UserProfileResponse(user.Id, user.UserName, user.Email, user.FirstName, user.LastName, user.ProfilePicture, user.CreatedAt);
    return Results.Ok(new { token, expiresIn = jwtSettings.TokenLifetimeHours * 3600, profile });
});

// Profile management endpoints
app.MapGet("/api/profile", async (AppDbContext db, HttpContext context) =>
{
    var userId = GetUserIdFromClaims(context);
    if (userId == null) return Results.Unauthorized();

    var user = await db.Users.FindAsync(userId);
    if (user is null) return Results.NotFound();

    var profile = new UserProfileResponse(user.Id, user.UserName, user.Email, user.FirstName, user.LastName, user.ProfilePicture, user.CreatedAt);
    return Results.Ok(profile);
}).RequireAuthorization();

app.MapPut("/api/profile", async (AppDbContext db, HttpContext context, UpdateProfileRequest req) =>
{
    var userId = GetUserIdFromClaims(context);
    if (userId == null) return Results.Unauthorized();

    var user = await db.Users.FindAsync(userId);
    if (user is null) return Results.NotFound();

    // Email validation (if provided)
    if (!string.IsNullOrWhiteSpace(req.Email) && !IsValidEmail(req.Email))
        return Results.BadRequest(new { message = "Invalid email format" });

    // Update profile fields
    user.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
    user.FirstName = string.IsNullOrWhiteSpace(req.FirstName) ? null : req.FirstName.Trim();
    user.LastName = string.IsNullOrWhiteSpace(req.LastName) ? null : req.LastName.Trim();
    user.ProfilePicture = string.IsNullOrWhiteSpace(req.ProfilePicture) ? null : req.ProfilePicture;

    await db.SaveChangesAsync();
    
    var profile = new UserProfileResponse(user.Id, user.UserName, user.Email, user.FirstName, user.LastName, user.ProfilePicture, user.CreatedAt);
    return Results.Ok(profile);
}).RequireAuthorization();

app.MapPost("/api/profile/change-password", async (AppDbContext db, HttpContext context, ChangePasswordRequest req) =>
{
    var userId = GetUserIdFromClaims(context);
    if (userId == null) return Results.Unauthorized();

    var user = await db.Users.FindAsync(userId);
    if (user is null) return Results.NotFound();

    // Verify current password
    if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
        return Results.BadRequest(new { message = "Current password is incorrect" });

    // Validate new password
    if (req.NewPassword.Length < authSettings.MinPasswordLength)
        return Results.BadRequest(new { message = $"Password must be at least {authSettings.MinPasswordLength} characters long" });

    // Update password
    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: authSettings.BcryptWorkFactor);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Password changed successfully" });
}).RequireAuthorization();

// Prefer standard ASP.NET URLs via ASPNETCORE_URLS; default to 8080
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    app.Urls.Add($"http://0.0.0.0:{port}");
}

app.Run();

// Helper methods
static bool IsValidEmail(string email)
{
    const string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    return Regex.IsMatch(email, emailPattern);
}

static int? GetUserIdFromClaims(HttpContext context)
{
    var userIdClaim = context.User.FindFirst("sub")?.Value;
    return int.TryParse(userIdClaim, out var userId) ? userId : null;
}

// Expose Program for WebApplicationFactory in tests
public partial class Program { }

// Minimal request records
public record RegisterRequest(string UserName, string Password, string? Email = null, string? FirstName = null, string? LastName = null);
public record LoginRequest(string UserName, string Password);
public record UpdateProfileRequest(string? Email, string? FirstName, string? LastName, string? ProfilePicture = null);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// Response models
public record UserProfileResponse(int Id, string UserName, string? Email, string? FirstName, string? LastName, string? ProfilePicture, DateTime CreatedAt);
