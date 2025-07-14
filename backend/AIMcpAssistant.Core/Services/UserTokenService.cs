using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIMcpAssistant.Core.Services;

public class UserTokenService : IUserTokenService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<UserTokenService> _logger;

    public UserTokenService(
        IUnitOfWork unitOfWork,
        IEncryptionService encryptionService,
        ILogger<UserTokenService> logger)
    {
        _unitOfWork = unitOfWork;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task SaveUserTokensAsync(string userId, string accessToken, string refreshToken, DateTime expiresAt)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByUserIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found when saving tokens: {UserId}", userId);
                return;
            }

            // Set plain text tokens
            user.AccessToken = accessToken;
            user.RefreshToken = refreshToken;
            user.TokenExpiresAt = expiresAt;

            // Encrypt tokens before saving
            EncryptUserTokens(user);

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Tokens saved successfully for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving tokens for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<(string? accessToken, string? refreshToken, DateTime? expiresAt)> GetUserTokensAsync(string userId)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByUserIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found when getting tokens: {UserId}", userId);
                return (null, null, null);
            }

            // Decrypt tokens after loading
            DecryptUserTokens(user);

            return (user.AccessToken, user.RefreshToken, user.TokenExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tokens for user: {UserId}", userId);
            return (null, null, null);
        }
    }

    public async Task<bool> IsTokenValidAsync(string userId)
    {
        try
        {
            var (accessToken, _, expiresAt) = await GetUserTokensAsync(userId);
            
            if (string.IsNullOrEmpty(accessToken) || !expiresAt.HasValue)
                return false;

            // Check if token is expired (with 5 minute buffer)
            return expiresAt.Value > DateTime.UtcNow.AddMinutes(5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking token validity for user: {UserId}", userId);
            return false;
        }
    }

    public async Task ClearUserTokensAsync(string userId)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByUserIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found when clearing tokens: {UserId}", userId);
                return;
            }

            user.AccessTokenEncrypted = null;
            user.RefreshTokenEncrypted = null;
            user.TokenExpiresAt = null;
            user.AccessToken = null;
            user.RefreshToken = null;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Tokens cleared for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing tokens for user: {UserId}", userId);
            throw;
        }
    }

    public void EncryptUserTokens(User user)
    {
        if (!string.IsNullOrEmpty(user.AccessToken))
        {
            user.AccessTokenEncrypted = _encryptionService.Encrypt(user.AccessToken);
        }

        if (!string.IsNullOrEmpty(user.RefreshToken))
        {
            user.RefreshTokenEncrypted = _encryptionService.Encrypt(user.RefreshToken);
        }

        // Clear plain text tokens after encryption
        user.AccessToken = null;
        user.RefreshToken = null;
    }

    public void DecryptUserTokens(User user)
    {
        if (!string.IsNullOrEmpty(user.AccessTokenEncrypted))
        {
            user.AccessToken = _encryptionService.Decrypt(user.AccessTokenEncrypted);
        }

        if (!string.IsNullOrEmpty(user.RefreshTokenEncrypted))
        {
            user.RefreshToken = _encryptionService.Decrypt(user.RefreshTokenEncrypted);
        }
    }
}