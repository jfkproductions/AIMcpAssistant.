using System.ComponentModel.DataAnnotations;

namespace AIMcpAssistant.Data.Entities;

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
    
    // Navigation properties
    public virtual ICollection<CommandHistory> CommandHistories { get; set; } = new List<CommandHistory>();
    public virtual ICollection<UserModuleSubscription> ModuleSubscriptions { get; set; } = new List<UserModuleSubscription>();
}