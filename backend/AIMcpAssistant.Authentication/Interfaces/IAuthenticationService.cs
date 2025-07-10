using AIMcpAssistant.Authentication.Models;
using System.Security.Claims;

namespace AIMcpAssistant.Authentication.Interfaces;

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(string provider, string authorizationCode, string? redirectUri = null);
    Task<AuthenticationResult> RefreshTokenAsync(string refreshToken, AuthenticationProvider provider);
    Task<bool> ValidateTokenAsync(string accessToken, AuthenticationProvider provider);
    Task<AuthenticatedUser?> GetUserFromTokenAsync(string accessToken, AuthenticationProvider provider);
    Task<string> GenerateJwtTokenAsync(AuthenticatedUser user);
    Task<ClaimsPrincipal?> ValidateJwtTokenAsync(string jwtToken);
    Task RevokeTokenAsync(string accessToken, AuthenticationProvider provider);
}