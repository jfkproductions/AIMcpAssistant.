using AIMcpAssistant.Data.Entities;

namespace AIMcpAssistant.Core.Interfaces;

public interface ICommandHistoryRepository : IRepository<CommandHistory>
{
    Task<IEnumerable<CommandHistory>> GetUserHistoryAsync(string userId, int limit = 50);
    Task<IEnumerable<CommandHistory>> GetUserHistoryByModuleAsync(string userId, string moduleId, int limit = 50);
    Task<IEnumerable<CommandHistory>> GetRecentHistoryAsync(int limit = 100);
    Task<Dictionary<string, int>> GetCommandStatsAsync(string userId, DateTime? fromDate = null);
    Task<IEnumerable<CommandHistory>> GetFailedCommandsAsync(string userId, int limit = 20);
    Task CleanupOldHistoryAsync(DateTime cutoffDate);
}