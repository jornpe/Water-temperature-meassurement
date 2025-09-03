namespace WaterTemperature.Api.Models.Auth;

/// <summary>
/// Request model for user registration.
/// </summary>
public record RegisterRequest(
    string UserName, 
    string Password, 
    string? Email = null, 
    string? FirstName = null, 
    string? LastName = null);

/// <summary>
/// Request model for user login.
/// </summary>
public record LoginRequest(string UserName, string Password);

/// <summary>
/// Request model for updating user profile information.
/// </summary>
public record UpdateProfileRequest(
    string? Email, 
    string? FirstName, 
    string? LastName);

/// <summary>
/// Request model for changing user password.
/// </summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Response model containing user profile information.
/// </summary>
public record UserProfileResponse(
    int Id, 
    string UserName, 
    string? Email, 
    string? FirstName, 
    string? LastName, 
    bool HasProfilePicture, 
    DateTime CreatedAt);

/// <summary>
/// Response model for successful login.
/// </summary>
public record LoginResponse(string Token, int ExpiresIn, UserProfileResponse Profile);

/// <summary>
/// Response model for user existence check.
/// </summary>
public record UserExistsResponse(bool Exists);

/// <summary>
/// Response model for successful user creation.
/// </summary>
public record UserCreatedResponse(int Id, string UserName);

/// <summary>
/// Standard message response.
/// </summary>
public record MessageResponse(string Message);
