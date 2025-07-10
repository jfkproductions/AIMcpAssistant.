using AIMcpAssistant.Data;
using AIMcpAssistant.Data.Entities;
using AIMcpAssistant.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AIMcpAssistant.Tests.Data;

public class CommandHistoryRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CommandHistoryRepository _repository;

    public CommandHistoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new CommandHistoryRepository(_context);
    }

    [Fact]
    public async Task GetUserHistoryAsync_ShouldReturnUserCommandsOrderedByDate()
    {
        // Arrange
        var userId = "test-user-123";
        var commands = new[]
        {
            new CommandHistory
            {
                UserId = userId,
                Command = "send-email",
                ModuleId = "email",
                ModuleName = "Email",
                IsSuccess = true,
                ExecutedAt = DateTime.UtcNow.AddHours(-2),
                ExecutionDuration = TimeSpan.FromSeconds(1.5)
            },
            new CommandHistory
            {
                UserId = userId,
                Command = "get-weather",
                ModuleId = "weather",
                ModuleName = "Weather",
                IsSuccess = true,
                ExecutedAt = DateTime.UtcNow.AddHours(-1),
                ExecutionDuration = TimeSpan.FromSeconds(0.8)
            },
            new CommandHistory
            {
                UserId = "other-user",
                Command = "create-event",
                ModuleId = "calendar",
                ModuleName = "Calendar",
                IsSuccess = true,
                ExecutedAt = DateTime.UtcNow,
                ExecutionDuration = TimeSpan.FromSeconds(2.1)
            }
        };

        await _context.CommandHistories.AddRangeAsync(commands);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserHistoryAsync(userId, 10);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Equal("get-weather", result.First().Command); // Most recent first
        Assert.Equal("send-email", result.Last().Command);
    }

    [Fact]
    public async Task GetUserHistoryByModuleAsync_ShouldReturnCommandsForSpecificModule()
    {
        // Arrange
        var userId = "test-user-123";
        var commands = new[]
        {
            new CommandHistory
            {
                UserId = userId,
                Command = "send-email",
                ModuleId = "email",
                ModuleName = "Email",
                IsSuccess = true,
                ExecutedAt = DateTime.UtcNow.AddHours(-2),
                ExecutionDuration = TimeSpan.FromSeconds(1.5)
            },
            new CommandHistory
            {
                UserId = userId,
                Command = "read-emails",
                ModuleId = "email",
                ModuleName = "Email",
                IsSuccess = true,
                ExecutedAt = DateTime.UtcNow.AddHours(-1),
                ExecutionDuration = TimeSpan.FromSeconds(0.8)
            },
            new CommandHistory
            {
                UserId = userId,
                Command = "get-weather",
                ModuleId = "weather",
                ModuleName = "Weather",
                IsSuccess = true,
                ExecutedAt = DateTime.UtcNow,
                ExecutionDuration = TimeSpan.FromSeconds(2.1)
            }
        };

        await _context.CommandHistories.AddRangeAsync(commands);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserHistoryByModuleAsync(userId, "email", 10);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, cmd => Assert.Equal("email", cmd.ModuleId));
        Assert.Equal("read-emails", result.First().Command); // Most recent first
    }

    [Fact]
    public async Task GetCommandStatsAsync_ShouldReturnCorrectStatistics()
    {
        // Arrange
        var userId = "test-user-123";
        var commands = new[]
        {
            new CommandHistory { UserId = userId, ModuleId = "email", ExecutedAt = DateTime.UtcNow },
            new CommandHistory { UserId = userId, ModuleId = "email", ExecutedAt = DateTime.UtcNow },
            new CommandHistory { UserId = userId, ModuleId = "weather", ExecutedAt = DateTime.UtcNow },
            new CommandHistory { UserId = "other-user", ModuleId = "email", ExecutedAt = DateTime.UtcNow }
        };

        await _context.CommandHistories.AddRangeAsync(commands);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetCommandStatsAsync(userId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result["email"]);
        Assert.Equal(1, result["weather"]);
    }

    [Fact]
    public async Task GetFailedCommandsAsync_ShouldReturnOnlyFailedCommands()
    {
        // Arrange
        var userId = "test-user-123";
        var commands = new[]
        {
            new CommandHistory
            {
                UserId = userId,
                Command = "failed-command",
                ModuleId = "email",
                IsSuccess = false,
                ErrorMessage = "Authentication failed",
                ExecutedAt = DateTime.UtcNow.AddHours(-1)
            },
            new CommandHistory
            {
                UserId = userId,
                Command = "successful-command",
                ModuleId = "email",
                IsSuccess = true,
                ExecutedAt = DateTime.UtcNow
            }
        };

        await _context.CommandHistories.AddRangeAsync(commands);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetFailedCommandsAsync(userId, 10);

        // Assert
        Assert.Single(result);
        Assert.Equal("failed-command", result.First().Command);
        Assert.False(result.First().IsSuccess);
    }

    [Fact]
    public async Task CleanupOldHistoryAsync_ShouldDeleteOldRecords()
    {
        // Arrange
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        var commands = new[]
        {
            new CommandHistory
            {
                UserId = "user1",
                Command = "old-command",
                ModuleId = "email",
                ExecutedAt = cutoffDate.AddDays(-1) // Older than cutoff
            },
            new CommandHistory
            {
                UserId = "user1",
                Command = "recent-command",
                ModuleId = "email",
                ExecutedAt = cutoffDate.AddDays(1) // Newer than cutoff
            }
        };

        await _context.CommandHistories.AddRangeAsync(commands);
        await _context.SaveChangesAsync();

        // Act
        await _repository.CleanupOldHistoryAsync(cutoffDate);
        await _context.SaveChangesAsync();

        // Assert
        var remainingCommands = await _context.CommandHistories.ToListAsync();
        Assert.Single(remainingCommands);
        Assert.Equal("recent-command", remainingCommands.First().Command);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}