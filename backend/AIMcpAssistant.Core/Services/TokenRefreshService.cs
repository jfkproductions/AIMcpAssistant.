using AIMcpAssistant.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIMcpAssistant.Core.Services;

public class TokenRefreshService : ITokenRefreshService
{
    private readonly ITokenStorageService _tokenStorage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenRefreshService> _logger;
    private readonly HttpClient _httpClient;

    public TokenRefreshService(
        ITokenStorageService tokenStorage,
        IConfiguration configuration,
        ILogger<TokenRefreshService> logger,
        HttpClient httpClient)
    {
        _tokenStorage = tokenStorage;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Get valid access token for user, refreshing if necessary
    /// </summary>
    public async Task<string?> GetValidAccessTokenAsync(string userId)
    {
        var tokens = await _tokenStorage.GetTokensAsync(userId);
        if (tokens == null)
        {
            _logger.LogWarning("No tokens found for user: {UserId}", userId);
            return null;
        }

        // If token is not expired, return it
        if (!tokens.IsExpired && !tokens.IsExpiringSoon)
        {
            return tokens.AccessToken;
        }

        // Try to refresh the token
        if (!string.IsNullOrEmpty(tokens.RefreshToken))
        {
            var newTokens = await RefreshTokenAsync(tokens.RefreshToken, tokens.Provider);
            if (newTokens != null)
            {
                await _tokenStorage.UpdateAccessTokenAsync(userId, newTokens.AccessToken, newTokens.ExpiresAt);
                _logger.LogInformation("Successfully refreshed token for user: {UserId}", userId);
                return newTokens.AccessToken;
            }
        }

        _logger.LogWarning("Failed to refresh token for user: {UserId}", userId);
        return null;
    }
    
    public async Task<bool> RefreshTokenAsync(string userId)
    {
        try
        {
            var tokens = await _tokenStorage.GetTokensAsync(userId);
            if (tokens == null || string.IsNullOrEmpty(tokens.RefreshToken))
            {
                _logger.LogWarning("No refresh token found for user {UserId}", userId);
                return false;
            }

            string? newAccessToken = null;
            string? newRefreshToken = null;
            DateTime? newExpiresAt = null;

            if (tokens.Provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                var result = await RefreshGoogleTokenAsync(tokens.RefreshToken);
                if (result != null)
                {
                    newAccessToken = result.AccessToken;
                    newRefreshToken = result.RefreshToken ?? tokens.RefreshToken; // Google may not return new refresh token
                    newExpiresAt = result.ExpiresAt;
                }
            }
            else if (tokens.Provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                var result = await RefreshMicrosoftTokenAsync(tokens.RefreshToken);
                if (result != null)
                {
                    newAccessToken = result.AccessToken;
                    newRefreshToken = result.RefreshToken ?? tokens.RefreshToken; // Microsoft may not return new refresh token
                    newExpiresAt = result.ExpiresAt;
                }
            }

            if (!string.IsNullOrEmpty(newAccessToken))
            {
                await _tokenStorage.UpdateTokensAsync(userId, newAccessToken, newRefreshToken!, newExpiresAt!.Value);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Refresh OAuth token using refresh token
    /// </summary>
    private async Task<AuthTokens?> RefreshTokenAsync(string refreshToken, string provider)
    {
        try
        {
            return provider.ToLowerInvariant() switch
            {
                "google" => await RefreshGoogleTokenAsync(refreshToken),
                "microsoft" => await RefreshMicrosoftTokenAsync(refreshToken),
                _ => throw new NotSupportedException($"Token refresh not supported for provider: {provider}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh {Provider} token", provider);
            return null;
        }
    }

    private async Task<AuthTokens?> RefreshGoogleTokenAsync(string refreshToken)
    {
        var clientId = _configuration["Authentication:Google:ClientId"];
        var clientSecret = _configuration["Authentication:Google:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException("Google OAuth configuration is missing");
        }

        var requestData = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        };

        var response = await _httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(requestData));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Google token refresh failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(content);

        if (tokenResponse?.AccessToken == null)
        {
            _logger.LogError("Invalid Google token response: {Content}", content);
            return null;
        }

        return new AuthTokens
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = refreshToken, // Keep the same refresh token
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn ?? 3600),
            Provider = "Google",
            Scopes = new List<string>
            {
                "https://www.googleapis.com/auth/gmail.modify",
                "https://www.googleapis.com/auth/calendar.readonly"
            }
        };
    }

    private async Task<AuthTokens?> RefreshMicrosoftTokenAsync(string refreshToken)
    {
        var clientId = _configuration["Authentication:Microsoft:ClientId"];
        var clientSecret = _configuration["Authentication:Microsoft:ClientSecret"];
        var tenantId = _configuration["Authentication:Microsoft:TenantId"] ?? "common";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException("Microsoft OAuth configuration is missing");
        }

        var requestData = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = "https://graph.microsoft.com/Mail.Read https://graph.microsoft.com/Calendars.Read"
        };

        var response = await _httpClient.PostAsync(
            $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token",
            new FormUrlEncodedContent(requestData));

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Microsoft token refresh failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<MicrosoftTokenResponse>(content);

        if (tokenResponse?.AccessToken == null)
        {
            _logger.LogError("Invalid Microsoft token response: {Content}", content);
            return null;
        }

        return new AuthTokens
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken ?? refreshToken, // Use new refresh token if provided
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn ?? 3600),
            Provider = "Microsoft",
            Scopes = new List<string>
            {
                "https://graph.microsoft.com/Mail.Read",
                "https://graph.microsoft.com/Calendars.Read"
            }
        };
    }

    private class GoogleTokenResponse
    {
        public string? AccessToken { get; set; }
        public int? ExpiresIn { get; set; }
        public string? Scope { get; set; }
        public string? TokenType { get; set; }
    }

    private class MicrosoftTokenResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public int? ExpiresIn { get; set; }
        public string? Scope { get; set; }
        public string? TokenType { get; set; }
    }
}