using AIMcpAssistant.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIMcpAssistant.Core.Services;

public class TokenStorageService : ITokenStorageService
{
    private readonly IUserTokenService _userTokenService;
    private readonly ILogger<TokenStorageService> _logger;

    public TokenStorageService(IUserTokenService userTokenService, ILogger<TokenStorageService> logger)
    {
        _userTokenService = userTokenService;
        _logger = logger;
    }

    public async Task SaveTokensAsync(string userId, string accessToken, string? refreshToken = null, DateTime? expiresAt = null)
    {
        try
        {
            if (refreshToken != null && expiresAt.HasValue)
            {
                await _userTokenService.SaveUserTokensAsync(userId, accessToken, refreshToken, expiresAt.Value);
            }
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
            var tokens = await _userTokenService.GetUserTokensAsync(userId);
            if (tokens.accessToken == null)
            {
                _logger.LogDebug("No tokens found for user: {UserId}", userId);
                return null;
            }

            return new AuthTokens
            {
                AccessToken = tokens.accessToken,
                RefreshToken = tokens.refreshToken,
                ExpiresAt = tokens.expiresAt,
                Provider = "Unknown", // Provider info not available from IUserTokenService
                Scopes = new List<string>() // Scopes not available from IUserTokenService
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
            var existingTokens = await _userTokenService.GetUserTokensAsync(userId);
            if (existingTokens.accessToken != null)
            {
                var refreshToken = existingTokens.refreshToken ?? string.Empty;
                var expiry = expiresAt ?? existingTokens.expiresAt ?? DateTime.UtcNow.AddHours(1);
                await _userTokenService.SaveUserTokensAsync(userId, accessToken, refreshToken, expiry);
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
            await _userTokenService.SaveUserTokensAsync(userId, accessToken, refreshToken, expiresAt);
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
            await _userTokenService.ClearUserTokensAsync(userId);
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
            return await _userTokenService.IsTokenValidAsync(userId);
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