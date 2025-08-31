using Microsoft.AspNetCore.Mvc;

namespace WaterTemperature.Api.Controllers;

/// <summary>
/// Base class for all API controllers in the Water Temperature API.
/// Provides common functionality and enforces consistent behavior across all controllers.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Gets the user ID from the current HTTP context claims.
    /// </summary>
    /// <returns>The user ID if found and valid; otherwise null.</returns>
    protected int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Creates a standardized error response.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The HTTP status code (default: 400).</param>
    /// <returns>An IActionResult with the error response.</returns>
    protected IActionResult ErrorResponse(string message, int statusCode = 400)
    {
        return StatusCode(statusCode, new { message });
    }
}
