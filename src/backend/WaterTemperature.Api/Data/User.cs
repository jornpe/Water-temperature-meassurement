using System.ComponentModel.DataAnnotations;

namespace WaterTemperature.Api.Data;

public class User
{
    [Key]
    public int Id { get; init; }
    
    [Required]
    [MaxLength(50)]
    public string UserName { get; init; } = string.Empty;
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    [MaxLength(50)]
    public string? FirstName { get; set; }
    
    [MaxLength(50)]
    public string? LastName { get; set; }
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    public byte[]? ProfilePicture { get; set; }
    public string? ProfilePictureContentType { get; set; }
    
    public bool IsAdmin { get; init; }
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
