using AIMcpAssistant.Authentication.Models;

namespace AIMcpAssistant.Authentication.Interfaces;

public interface IGoogleAuthenticationProvider
{
    Task<AuthenticationResult> AuthenticateAsync(string authorizationCode, string? redirectUri = null);
    Task<AuthenticationResult> RefreshTokenAsync(string refreshToken);
    Task<bool> ValidateTokenAsync(string accessToken);
    Task<AuthenticatedUser?> GetUserInfoAsync(string accessToken);
    Task RevokeTokenAsync(string accessToken);
}