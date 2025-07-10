using AIMcpAssistant.Data.Entities;

namespace AIMcpAssistant.Data.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUserIdAsync(string userId);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetActiveUsersAsync();
    Task UpdateLastLoginAsync(string userId);
    Task<IEnumerable<User>> GetUsersWithActiveEmailSubscriptionsAsync();
}