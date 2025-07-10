using AIMcpAssistant.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIMcpAssistant.Data.Repositories;

public class UserModuleSubscriptionRepository
{
    private readonly ApplicationDbContext _context;

    public UserModuleSubscriptionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserModuleSubscription>> GetUserSubscriptionsAsync(string userId)
    {
        return await _context.UserModuleSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync();
    }

    public async Task<UserModuleSubscription?> GetUserSubscriptionAsync(string userId, string moduleId)
    {
        return await _context.UserModuleSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ModuleId == moduleId);
    }

    public async Task<UserModuleSubscription> UpsertSubscriptionAsync(string userId, string moduleId, bool isSubscribed)
    {
        var existing = await GetUserSubscriptionAsync(userId, moduleId);
        
        if (existing != null)
        {
            existing.IsSubscribed = isSubscribed;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.UserModuleSubscriptions.Update(existing);
        }
        else
        {
            existing = new UserModuleSubscription
            {
                UserId = userId,
                ModuleId = moduleId,
                IsSubscribed = isSubscribed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserModuleSubscriptions.Add(existing);
        }
        
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteUserSubscriptionsAsync(string userId)
    {
        var subscriptions = await _context.UserModuleSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync();
            
        _context.UserModuleSubscriptions.RemoveRange(subscriptions);
        await _context.SaveChangesAsync();
    }
    
    public async Task CleanupOrphanedSubscriptionsAsync()
    {
        var validModuleIds = new[] { "EmailMcp", "CalendarMcp", "chatgpt" };
        var orphanedSubscriptions = await _context.UserModuleSubscriptions
            .Where(s => !validModuleIds.Contains(s.ModuleId))
            .ToListAsync();
            
        if (orphanedSubscriptions.Any())
        {
            _context.UserModuleSubscriptions.RemoveRange(orphanedSubscriptions);
            await _context.SaveChangesAsync();
        }
    }
}