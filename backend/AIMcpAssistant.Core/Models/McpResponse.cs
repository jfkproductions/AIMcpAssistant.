namespace AIMcpAssistant.Core.Models;

public class McpResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public string? ErrorCode { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<McpAction> SuggestedActions { get; set; } = new();
    public bool RequiresFollowUp { get; set; }
    public string? FollowUpPrompt { get; set; }
    
    public static McpResponse CreateSuccess(string message, object? data = null)
    {
        return new McpResponse
        {
            Success = true,
            Message = message,
            Data = data
        };
    }
    
    public static McpResponse CreateError(string message, string? errorCode = null)
    {
        return new McpResponse
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode
        };
    }
}

public class McpAction
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class McpUpdate
{
    public string ModuleId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "notification", "data_change", "status"
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Priority { get; set; } = "normal"; // "low", "normal", "high", "urgent"
    public Dictionary<string, object> Metadata { get; set; } = new();
}