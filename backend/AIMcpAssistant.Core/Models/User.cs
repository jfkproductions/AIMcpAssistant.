using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIMcpAssistant.Core.Models;

public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
    
    // OAuth token storage for background services - encrypted in database
    [MaxLength(4000)] // Increased size for encrypted data
    [Column("AccessTokenEncrypted")]
    public string? AccessTokenEncrypted { get; set; }
    
    [MaxLength(4000)] // Increased size for encrypted data
    [Column("RefreshTokenEncrypted")]
    public string? RefreshTokenEncrypted { get; set; }
    
    public DateTime? TokenExpiresAt { get; set; }
    
    // Non-mapped properties for plain text access
    [NotMapped]
    public string? AccessToken { get; set; }
    
    [NotMapped]
    public string? RefreshToken { get; set; }
    
    // Navigation properties
    public virtual ICollection<CommandHistory> CommandHistories { get; set; } = new List<CommandHistory>();
    public virtual ICollection<UserModuleSubscription> ModuleSubscriptions { get; set; } = new List<UserModuleSubscription>();
}