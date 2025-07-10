using AIMcpAssistant.Data.Entities;
using AIMcpAssistant.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AIMcpAssistant.Data.Repositories;

public class CommandHistoryRepository : Repository<CommandHistory>, ICommandHistoryRepository
{
    public CommandHistoryRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<CommandHistory>> GetUserHistoryAsync(string userId, int limit = 50)
    {
        return await _dbSet
            .Where(ch => ch.UserId == userId)
            .OrderByDescending(ch => ch.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<CommandHistory>> GetUserHistoryByModuleAsync(string userId, string moduleId, int limit = 50)
    {
        return await _dbSet
            .Where(ch => ch.UserId == userId && ch.ModuleId == moduleId)
            .OrderByDescending(ch => ch.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<CommandHistory>> GetRecentHistoryAsync(int limit = 100)
    {
        return await _dbSet
            .OrderByDescending(ch => ch.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetCommandStatsAsync(string userId, DateTime? fromDate = null)
    {
        var query = _dbSet.Where(ch => ch.UserId == userId);
        
        if (fromDate.HasValue)
        {
            query = query.Where(ch => ch.ExecutedAt >= fromDate.Value);
        }

        return await query
            .GroupBy(ch => ch.ModuleId)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ModuleId, x => x.Count);
    }

    public async Task<IEnumerable<CommandHistory>> GetFailedCommandsAsync(string userId, int limit = 20)
    {
        return await _dbSet
            .Where(ch => ch.UserId == userId && !ch.IsSuccess)
            .OrderByDescending(ch => ch.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task CleanupOldHistoryAsync(DateTime cutoffDate)
    {
        var oldRecords = await _dbSet
            .Where(ch => ch.ExecutedAt < cutoffDate)
            .ToListAsync();

        if (oldRecords.Any())
        {
            await DeleteRangeAsync(oldRecords);
        }
    }
}