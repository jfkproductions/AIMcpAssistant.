using System.ComponentModel.DataAnnotations;

namespace AIMcpAssistant.Core.Models;

public class CommandHistory
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string Command { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string ModuleId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string ModuleName { get; set; } = string.Empty;
    
    public bool IsSuccess { get; set; }
    
    [MaxLength(2000)]
    public string? Response { get; set; }
    
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }
    
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    
    public TimeSpan ExecutionDuration { get; set; }
    
    [MaxLength(500)]
    public string? AdditionalData { get; set; }
}