using AIMcpAssistant.Data.Entities;
using AIMcpAssistant.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AIMcpAssistant.Data.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByUserIdAsync(string userId)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync()
    {
        return await _dbSet.Where(u => u.IsActive).ToListAsync();
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
        return await _dbSet
            .Include(u => u.ModuleSubscriptions)
            .Where(u => u.ModuleSubscriptions.Any(s => s.ModuleId == "email" && s.IsSubscribed) && 
                       !string.IsNullOrEmpty(u.AccessToken) &&
                       (u.TokenExpiresAt == null || u.TokenExpiresAt > DateTime.UtcNow))
            .ToListAsync();
    }

    public async Task UpdateOAuthTokensAsync(string userId, string accessToken, string? refreshToken, DateTime? expiresAt)
    {
        var user = await GetByUserIdAsync(userId);
        if (user != null)
        {
            user.AccessToken = accessToken;
            user.RefreshToken = refreshToken;
            user.TokenExpiresAt = expiresAt;
            await UpdateAsync(user);
        }
    }

    public async Task<User?> GetUserWithValidTokenAsync(string userId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.UserId == userId && 
                                    !string.IsNullOrEmpty(u.AccessToken) &&
                                    (u.TokenExpiresAt == null || u.TokenExpiresAt > DateTime.UtcNow));
    }
}