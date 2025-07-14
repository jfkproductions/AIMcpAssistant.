using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Models;
using AIMcpAssistant.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AIMcpAssistant.Data.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    private readonly IEncryptionService _encryptionService;

    public UserRepository(ApplicationDbContext context, IEncryptionService encryptionService) : base(context)
    {
        _encryptionService = encryptionService;
    }

    public async Task<User?> GetByUserIdAsync(string userId)
    {
        var user = await _dbSet.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user != null)
        {
            user.AccessToken = _encryptionService.Decrypt(user.AccessTokenEncrypted);
            user.RefreshToken = _encryptionService.Decrypt(user.RefreshTokenEncrypted);
        }
        return user;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var user = await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            user.AccessToken = _encryptionService.Decrypt(user.AccessTokenEncrypted);
            user.RefreshToken = _encryptionService.Decrypt(user.RefreshTokenEncrypted);
        }
        return user;
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync()
    {
        var users = await _dbSet.Where(u => u.IsActive).ToListAsync();
        foreach (var user in users)
        {
            user.AccessToken = _encryptionService.Decrypt(user.AccessTokenEncrypted);
            user.RefreshToken = _encryptionService.Decrypt(user.RefreshTokenEncrypted);
        }
        return users;
    }

    public async Task UpdateLastLoginAsync(string userId)
    {
        var user = await GetByUserIdAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await UpdateAsync(user);
        }
    }

    public async Task<IEnumerable<User>> GetUsersWithActiveEmailSubscriptionsAsync()
    {
        var users = await _dbSet
            .Include(u => u.ModuleSubscriptions)
            .Where(u => u.ModuleSubscriptions.Any(s => s.ModuleId == "email" && s.IsSubscribed) && 
                       !string.IsNullOrEmpty(u.AccessTokenEncrypted) &&
                       (u.TokenExpiresAt == null || u.TokenExpiresAt > DateTime.UtcNow))
            .ToListAsync();

        foreach (var user in users)
        {
            user.AccessToken = _encryptionService.Decrypt(user.AccessTokenEncrypted);
            user.RefreshToken = _encryptionService.Decrypt(user.RefreshTokenEncrypted);
        }

        return users;
    }

    public async Task UpdateOAuthTokensAsync(string userId, string accessToken, string? refreshToken, DateTime? expiresAt)
    {
        var user = await _dbSet.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user != null)
        {
            user.AccessTokenEncrypted = _encryptionService.Encrypt(accessToken);
            user.RefreshTokenEncrypted = _encryptionService.Encrypt(refreshToken);
            user.TokenExpiresAt = expiresAt;
            await UpdateAsync(user);
        }
    }

    public async Task<User?> GetUserWithValidTokenAsync(string userId)
    {
        var user = await _dbSet
            .FirstOrDefaultAsync(u => u.UserId == userId && 
                                    !string.IsNullOrEmpty(u.AccessTokenEncrypted) &&
                                    (u.TokenExpiresAt == null || u.TokenExpiresAt > DateTime.UtcNow));

        if (user != null)
        {
            user.AccessToken = _encryptionService.Decrypt(user.AccessTokenEncrypted);
            user.RefreshToken = _encryptionService.Decrypt(user.RefreshTokenEncrypted);
        }

        return user;
    }
}