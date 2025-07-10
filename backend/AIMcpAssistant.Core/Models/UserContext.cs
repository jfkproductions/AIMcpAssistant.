namespace AIMcpAssistant.Core.Models;

public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty; // "Google" or "Microsoft"
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiry { get; set; }
    public Dictionary<string, object> AdditionalClaims { get; set; } = new();
    public List<string> Scopes { get; set; } = new();
    
    public bool IsTokenExpired => DateTime.UtcNow >= TokenExpiry;
    
    public bool HasScope(string scope) => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
}