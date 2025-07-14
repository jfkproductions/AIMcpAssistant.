using AIMcpAssistant.Core.Models;

namespace AIMcpAssistant.Core.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUserIdAsync(string userId);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetActiveUsersAsync();
    Task UpdateLastLoginAsync(string userId);
    Task<IEnumerable<User>> GetUsersWithActiveEmailSubscriptionsAsync();
    Task UpdateOAuthTokensAsync(string userId, string accessToken, string? refreshToken, DateTime? expiresAt);
    Task<User?> GetUserWithValidTokenAsync(string userId);
}