using System.ComponentModel.DataAnnotations;

namespace AIMcpAssistant.Data.Entities;

public class UserModuleSubscription
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string ModuleId { get; set; } = string.Empty;
    
    public bool IsSubscribed { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public virtual User User { get; set; } = null!;
}