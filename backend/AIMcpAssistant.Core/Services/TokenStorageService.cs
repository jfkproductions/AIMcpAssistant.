using AIMcpAssistant.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace AIMcpAssistant.Core.Services;

public class TokenStorageService : ITokenStorageService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<TokenStorageService> _logger;

    public TokenStorageService(IUserRepository userRepository, ILogger<TokenStorageService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task SaveTokensAsync(string userId, string accessToken, string? refreshToken = null, DateTime? expiresAt = null)
    {
        try
        {
            await _userRepository.UpdateOAuthTokensAsync(userId, accessToken, refreshToken, expiresAt);
            _logger.LogDebug("Saved OAuth tokens for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save OAuth tokens for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<AuthTokens?> GetTokensAsync(string userId)
    {
        try
        {
            var user = await _userRepository.GetByUserIdAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.AccessToken))
            {
                _logger.LogDebug("No tokens found for user: {UserId}", userId);
                return null;
            }

            return new AuthTokens
            {
                AccessToken = user.AccessToken,
                RefreshToken = user.RefreshToken,
                ExpiresAt = user.TokenExpiresAt,
                Provider = user.Provider ?? "Unknown",
                Scopes = GetScopesForProvider(user.Provider ?? "Unknown")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get OAuth tokens for user: {UserId}", userId);
            throw;
        }
    }

    public async Task UpdateAccessTokenAsync(string userId, string accessToken, DateTime? expiresAt = null)
    {
        try
        {
            var user = await _userRepository.GetByUserIdAsync(userId);
            if (user != null)
            {
                await _userRepository.UpdateOAuthTokensAsync(userId, accessToken, user.RefreshToken, expiresAt);
                _logger.LogDebug("Updated access token for user: {UserId}", userId);
            }
            else
            {
                _logger.LogWarning("Attempted to update access token for non-existent user: {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update access token for user: {UserId}", userId);
            throw;
        }
    }

    public async Task UpdateTokensAsync(string userId, string accessToken, string refreshToken, DateTime expiresAt)
    {
        try
        {
            await _userRepository.UpdateOAuthTokensAsync(userId, accessToken, refreshToken, expiresAt);
            _logger.LogDebug("Updated both access and refresh tokens for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tokens for user: {UserId}", userId);
            throw;
        }
    }

    public async Task RemoveTokensAsync(string userId)
    {
        try
        {
            await _userRepository.UpdateOAuthTokensAsync(userId, null, null, null);
            _logger.LogDebug("Removed OAuth tokens for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove OAuth tokens for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> HasValidTokensAsync(string userId)
    {
        try
        {
            var tokens = await GetTokensAsync(userId);
            return tokens != null && !string.IsNullOrEmpty(tokens.AccessToken) && !tokens.IsExpired;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check token validity for user: {UserId}", userId);
            return false;
        }
    }

    private static List<string> GetScopesForProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => new List<string>
            {
                "https://www.googleapis.com/auth/gmail.modify",
                "https://www.googleapis.com/auth/calendar.readonly"
            },
            "microsoft" => new List<string>
            {
                "https://graph.microsoft.com/Mail.Read",
                "https://graph.microsoft.com/Calendars.Read"
            },
            _ => new List<string>()
        };
    }
}