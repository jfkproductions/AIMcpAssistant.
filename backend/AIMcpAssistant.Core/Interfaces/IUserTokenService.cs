using AIMcpAssistant.Core.Models;

namespace AIMcpAssistant.Core.Interfaces;

public interface IUserTokenService
{
    Task SaveUserTokensAsync(string userId, string accessToken, string refreshToken, DateTime expiresAt);
    Task<(string? accessToken, string? refreshToken, DateTime? expiresAt)> GetUserTokensAsync(string userId);
    Task<bool> IsTokenValidAsync(string userId);
    Task ClearUserTokensAsync(string userId);
    void EncryptUserTokens(User user);
    void DecryptUserTokens(User user);
}