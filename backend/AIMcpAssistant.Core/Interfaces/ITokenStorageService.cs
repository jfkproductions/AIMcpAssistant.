namespace AIMcpAssistant.Core.Interfaces;

public interface ITokenStorageService
{
    /// <summary>
    /// Store OAuth tokens for a user
    /// </summary>
    Task SaveTokensAsync(string userId, string accessToken, string? refreshToken = null, DateTime? expiresAt = null);
    
    /// <summary>
    /// Retrieve OAuth tokens for a user
    /// </summary>
    Task<AuthTokens?> GetTokensAsync(string userId);
    
    /// <summary>
    /// Update access token for a user (typically after refresh)
    /// </summary>
    Task UpdateAccessTokenAsync(string userId, string accessToken, DateTime? expiresAt = null);
    
    /// <summary>
    /// Update both access and refresh tokens for a user
    /// </summary>
    Task UpdateTokensAsync(string userId, string accessToken, string refreshToken, DateTime expiresAt);
    
    /// <summary>
    /// Remove tokens for a user (on logout or revocation)
    /// </summary>
    Task RemoveTokensAsync(string userId);
    
    /// <summary>
    /// Check if tokens exist and are valid for a user
    /// </summary>
    Task<bool> HasValidTokensAsync(string userId);
}

public class AuthTokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Provider { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;
    public bool IsExpiringSoon => ExpiresAt.HasValue && DateTime.UtcNow.AddMinutes(5) >= ExpiresAt.Value;
}