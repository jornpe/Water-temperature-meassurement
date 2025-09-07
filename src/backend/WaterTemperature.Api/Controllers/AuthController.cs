using System.Text.RegularExpressions;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterTemperature.Api.Data;
using WaterTemperature.Api.Models.Auth;
using WaterTemperature.Api.Configuration;
using WaterTemperature.Api.Services;
using Microsoft.Extensions.Options;

namespace WaterTemperature.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AuthController(
    AppDbContext dbContext,
    IOptions<AuthenticationSettings> authSettings,
    IOptions<JwtSettings> jwtSettings,
    IJwtService jwtService,
    IWebHostEnvironment environment)
    : ApiControllerBase
{
    [HttpGet("users/exists")]
    public async Task<ActionResult<UserExistsResponse>> CheckUsersExist()
    {
        var count = await dbContext.Users.CountAsync();
        return Ok(new UserExistsResponse(count > 0));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Only allow when no user exists
        if (await dbContext.Users.AnyAsync())
        {
            return Conflict(new MessageResponse("Only one user can exist"));
        }
        
        // Enhanced validation
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new MessageResponse("Username and password required"));
        }
        
        // Password strength validation
        if (request.Password.Length < authSettings.Value.MinPasswordLength)
        {
            return BadRequest(new MessageResponse($"Password must be at least {authSettings.Value.MinPasswordLength} characters long"));
        }
        
        // Username length validation
        var username = request.UserName.Trim();
        if (username.Length is < 3 or > 50)
        {
            return BadRequest(new MessageResponse("Username must be between 3-50 characters"));
        }

        // Email validation (if provided)
        if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
        {
            return BadRequest(new MessageResponse("Invalid email format"));
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = new User 
        { 
            UserName = username, 
            PasswordHash = hash,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim()
        };
        
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        
        var response = new UserCreatedResponse(user.Id, user.UserName);
        return CreatedAtAction(nameof(GetProfile), null, response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized();
        }

        // Generate both access and refresh tokens
        var accessToken = jwtService.CreateToken(user);
        var refreshToken = jwtService.GenerateRefreshToken();
        
        // Store refresh token in database
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30); // 30 days as requested
        await dbContext.SaveChangesAsync();
        
        var cookieOptions = GetCookieOptions(user);
        
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

        // Debug logging
        Console.WriteLine($"Setting refresh token cookie: {refreshToken.Substring(0, 10)}...");
        Console.WriteLine($"Cookie expires: {user.RefreshTokenExpiry}");
        Console.WriteLine($"Request host: {HttpContext.Request.Host}");

        var profile = new UserProfileResponse(
            user.Id, 
            user.UserName, 
            user.Email, 
            user.FirstName, 
            user.LastName, 
            user.ProfilePicture != null && user.ProfilePicture.Length > 0, 
            user.CreatedAt);
        
        var response = new LoginResponse(accessToken, jwtSettings.Value.TokenLifetimeHours * 3600, profile);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        // Get refresh token from HttpOnly cookie
        if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new MessageResponse("Refresh token not found"));
        }

        // Find user by refresh token
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        if (user == null)
        {
            return Unauthorized(new MessageResponse("Invalid refresh token"));
        }

        // Check if refresh token is expired
        if (user.RefreshTokenExpiry <= DateTime.UtcNow)
        {
            return Unauthorized(new MessageResponse("Refresh token expired"));
        }
        
        // Generate new access token
        var newAccessToken = jwtService.CreateToken(user);
        
        // Rotate the refresh token
        var newRefreshToken = jwtService.GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
        await dbContext.SaveChangesAsync();
        
        // Update the refresh token cookie
        var cookieOptions = GetCookieOptions(user);
        
        Response.Cookies.Append("refreshToken", newRefreshToken, cookieOptions);

        Console.WriteLine("New access token generated and refresh token rotated");

        var response = new RefreshTokenResponse(newAccessToken, jwtSettings.Value.TokenLifetimeHours * 3600);
        return Ok(response);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = GetCurrentUserId();
        if (userId != null)
        {
            var user = await dbContext.Users.FindAsync(userId);
            if (user != null)
            {
                // Clear refresh token from database
                user.RefreshToken = null;
                user.RefreshTokenExpiry = null;
                await dbContext.SaveChangesAsync();
            }
        }

        // Clear the refresh token cookie
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // Set to false for development (localhost)
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Domain = null
        });

        return Ok(new MessageResponse("Logged out successfully"));
    }

    [HttpGet("debug/cookies")]
    public IActionResult DebugCookies()
    {
        var cookies = Request.Cookies.Select(c => new { c.Key, c.Value }).ToList();
        return Ok(new { 
            CookieCount = cookies.Count,
            Cookies = cookies,
            Host = HttpContext.Request.Host.ToString(),
            Scheme = HttpContext.Request.Scheme
        });
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null) 
            return Unauthorized();

        var user = await dbContext.Users.FindAsync(userId);
        if (user is null) 
            return NotFound();

        var profile = new UserProfileResponse(
            user.Id, 
            user.UserName, 
            user.Email, 
            user.FirstName, 
            user.LastName, 
            user.ProfilePicture != null && user.ProfilePicture.Length > 0, 
            user.CreatedAt);
        
        return Ok(profile);
    }
    
    
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) 
            return Unauthorized();

        var user = await dbContext.Users.FindAsync(userId);
        if (user is null) 
            return NotFound();

        // Email validation (if provided)
        if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
        {
            return BadRequest(new MessageResponse("Invalid email format"));
        }

        // Update profile fields
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim();
        user.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();

        await dbContext.SaveChangesAsync();
        
        var profile = new UserProfileResponse(
            user.Id, 
            user.UserName, 
            user.Email, 
            user.FirstName, 
            user.LastName, 
            user.ProfilePicture != null && user.ProfilePicture.Length > 0, 
            user.CreatedAt);
        
        return Ok(profile);
    }

    [HttpPost("profile/change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) 
            return Unauthorized();

        var user = await dbContext.Users.FindAsync(userId);
        if (user is null) 
            return NotFound();

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new MessageResponse("Current password is incorrect"));
        }

        // Validate new password
        if (request.NewPassword.Length < authSettings.Value.MinPasswordLength)
        {
            return BadRequest(new MessageResponse($"Password must be at least {authSettings.Value.MinPasswordLength} characters long"));
        }

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await dbContext.SaveChangesAsync();

        return Ok(new MessageResponse("Password changed successfully"));
    }

    [HttpPost("profile/picture")]
    [Authorize]
    public async Task<IActionResult> UploadProfilePicture(IFormFile picture)
    {
        var userId = GetCurrentUserId();
        if (userId == null) 
            return Unauthorized();

        if (picture == null || picture.Length == 0)
            return BadRequest(new MessageResponse("No image file provided"));

        // Validate file size (5MB limit)
        if (picture.Length > 5 * 1024 * 1024)
            return BadRequest(new MessageResponse("Image file size cannot exceed 5MB"));

        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(picture.ContentType.ToLowerInvariant()))
            return BadRequest(new MessageResponse("Only JPEG, PNG, GIF, and WebP images are allowed"));

        var user = await dbContext.Users.FindAsync(userId);
        if (user is null) 
            return NotFound();

        // Read image data
        using var memoryStream = new MemoryStream();
        await picture.CopyToAsync(memoryStream);
        
        user.ProfilePicture = memoryStream.ToArray();
        user.ProfilePictureContentType = picture.ContentType;

        await dbContext.SaveChangesAsync();

        var profile = new UserProfileResponse(
            user.Id, 
            user.UserName, 
            user.Email, 
            user.FirstName, 
            user.LastName, 
            user.ProfilePicture != null && user.ProfilePicture.Length > 0, 
            user.CreatedAt);

        return Ok(profile);
    }

    [HttpGet("profile/picture/{userId:int}")]
    public async Task<IActionResult> GetProfilePicture(int userId)
    {
        var user = await dbContext.Users.FindAsync(userId);
        if (user?.ProfilePicture is null || user.ProfilePicture.Length == 0)
            return NotFound();

        return File(user.ProfilePicture, user.ProfilePictureContentType ?? "image/jpeg");
    }

    [HttpDelete("profile/picture")]
    [Authorize]
    public async Task<IActionResult> DeleteProfilePicture()
    {
        var userId = GetCurrentUserId();
        if (userId == null) 
            return Unauthorized();

        var user = await dbContext.Users.FindAsync(userId);
        if (user is null) 
            return NotFound();

        user.ProfilePicture = null;
        user.ProfilePictureContentType = null;

        await dbContext.SaveChangesAsync();

        var profile = new UserProfileResponse(
            user.Id, 
            user.UserName, 
            user.Email, 
            user.FirstName, 
            user.LastName, 
            user.ProfilePicture != null && user.ProfilePicture.Length > 0, 
            user.CreatedAt);

        return Ok(profile);
    }
    
    private static bool IsValidEmail(string email)
    {
        const string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, emailPattern);
    }

    private CookieOptions GetCookieOptions(User user)
    {
        if(environment.IsDevelopment())
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = false, 
                SameSite = SameSiteMode.Lax,
                Expires = user.RefreshTokenExpiry,
                Path = "/", 
                Domain = null 
            };
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = user.RefreshTokenExpiry,
            Path = "/",
            Domain = null
        };
    }
    
}
