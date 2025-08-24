namespace WaterTemperature.Api.Data;

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? ProfilePicture { get; set; } // Base64 encoded image or file path
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
