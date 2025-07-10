using System.ComponentModel.DataAnnotations;

namespace AIMcpAssistant.Data.Entities;

public class Module
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string ModuleId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";
    
    public bool IsEnabled { get; set; } = true;
    
    public bool IsRegistered { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<UserModuleSubscription> UserSubscriptions { get; set; } = new List<UserModuleSubscription>();
}