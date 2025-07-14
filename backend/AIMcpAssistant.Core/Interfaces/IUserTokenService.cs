using AIMcpAssistant.Data.Entities;

namespace AIMcpAssistant.Core.Interfaces;

public interface IUserTokenService
{
    // OAuth token methods
    Task SaveUserTokensAsync(string userId, string accessToken, string refreshToken, DateTime expiresAt);
    Task<(string? accessToken, string? refreshToken, DateTime? expiresAt)> GetUserTokensAsync(string userId);
    Task<bool> IsTokenValidAsync(string userId);
    Task ClearUserTokensAsync(string userId);
    void EncryptUserTokens(User user);
    void DecryptUserTokens(User user);
    
    // JWT token methods
    Task SaveJwtTokenAsync(string userId, string jwtToken, DateTime expiresAt);
    Task<(string? jwtToken, DateTime? expiresAt)> GetJwtTokenAsync(string userId);
    Task<bool> IsJwtTokenValidAsync(string userId);
    Task ClearJwtTokenAsync(string userId);
}