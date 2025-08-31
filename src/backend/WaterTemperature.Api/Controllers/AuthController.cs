using System.Text.RegularExpressions;
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
    IJwtService jwtService)
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

        // Use the JWT service to create the token
        var token = jwtService.CreateToken(user);
        var profile = new UserProfileResponse(
            user.Id, 
            user.UserName, 
            user.Email, 
            user.FirstName, 
            user.LastName, 
            user.ProfilePicture, 
            user.CreatedAt);
        
        var response = new LoginResponse(token,  jwtSettings.Value.TokenLifetimeHours * 3600, profile);
        return Ok(response);
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
            user.ProfilePicture, 
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
        user.ProfilePicture = string.IsNullOrWhiteSpace(request.ProfilePicture) ? null : request.ProfilePicture;

        await dbContext.SaveChangesAsync();
        
        var profile = new UserProfileResponse(
            user.Id, 
            user.UserName, 
            user.Email, 
            user.FirstName, 
            user.LastName, 
            user.ProfilePicture, 
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
    
    private static bool IsValidEmail(string email)
    {
        const string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, emailPattern);
    }
}
