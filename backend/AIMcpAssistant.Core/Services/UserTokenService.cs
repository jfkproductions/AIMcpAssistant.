using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Data.Entities;
using AIMcpAssistant.Data.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIMcpAssistant.Core.Services;

public class UserTokenService : IUserTokenService
{
    private readonly Data.Interfaces.IUnitOfWork _unitOfWork;
    private readonly Data.Interfaces.IEncryptionService _encryptionService;
    private readonly ILogger<UserTokenService> _logger;

    public UserTokenService(
        Data.Interfaces.IUnitOfWork unitOfWork,
        Data.Interfaces.IEncryptionService encryptionService,
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
        
        if (!string.IsNullOrEmpty(user.JwtTokenEncrypted))
        {
            user.JwtToken = _encryptionService.Decrypt(user.JwtTokenEncrypted);
        }
    }

    // JWT Token Methods
    public async Task SaveJwtTokenAsync(string userId, string jwtToken, DateTime expiresAt)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByUserIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found when saving JWT token: {UserId}", userId);
                return;
            }

            // Set plain text JWT token
            user.JwtToken = jwtToken;
            user.JwtTokenExpiresAt = expiresAt;

            // Encrypt JWT token before saving
            EncryptJwtToken(user);

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("JWT token saved successfully for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving JWT token for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<(string? jwtToken, DateTime? expiresAt)> GetJwtTokenAsync(string userId)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByUserIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found when getting JWT token: {UserId}", userId);
                return (null, null);
            }

            // Decrypt JWT token after loading
            DecryptJwtToken(user);

            return (user.JwtToken, user.JwtTokenExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting JWT token for user: {UserId}", userId);
            return (null, null);
        }
    }

    public async Task<bool> IsJwtTokenValidAsync(string userId)
    {
        try
        {
            var (jwtToken, expiresAt) = await GetJwtTokenAsync(userId);
            
            if (string.IsNullOrEmpty(jwtToken) || !expiresAt.HasValue)
                return false;

            // Check if JWT token is expired (with 5 minute buffer)
            return expiresAt.Value > DateTime.UtcNow.AddMinutes(5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking JWT token validity for user: {UserId}", userId);
            return false;
        }
    }

    public async Task ClearJwtTokenAsync(string userId)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByUserIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found when clearing JWT token: {UserId}", userId);
                return;
            }

            user.JwtTokenEncrypted = null;
            user.JwtTokenExpiresAt = null;
            user.JwtToken = null;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("JWT token cleared for user: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing JWT token for user: {UserId}", userId);
            throw;
        }
    }

    private void EncryptJwtToken(User user)
    {
        if (!string.IsNullOrEmpty(user.JwtToken))
        {
            user.JwtTokenEncrypted = _encryptionService.Encrypt(user.JwtToken);
        }

        // Clear plain text JWT token after encryption
        user.JwtToken = null;
    }

    private void DecryptJwtToken(User user)
    {
        if (!string.IsNullOrEmpty(user.JwtTokenEncrypted))
        {
            user.JwtToken = _encryptionService.Decrypt(user.JwtTokenEncrypted);
        }
    }
}