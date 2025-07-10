using AIMcpAssistant.Data;
using AIMcpAssistant.Data.Entities;
using AIMcpAssistant.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AIMcpAssistant.Tests.Data;

public class UserRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserRepository _repository;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new UserRepository(_context);
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var user = new User
        {
            UserId = "test-user-123",
            Email = "test@example.com",
            Name = "Test User",
            Provider = "Google",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync("test-user-123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-user-123", result.UserId);
        Assert.Equal("test@example.com", result.Email);
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Act
        var result = await _repository.GetByUserIdAsync("non-existent-user");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var user = new User
        {
            UserId = "test-user-123",
            Email = "test@example.com",
            Name = "Test User",
            Provider = "Google",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByEmailAsync("test@example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("test-user-123", result.UserId);
    }

    [Fact]
    public async Task GetActiveUsersAsync_ShouldReturnOnlyActiveUsers()
    {
        // Arrange
        var activeUser = new User
        {
            UserId = "active-user",
            Email = "active@example.com",
            Name = "Active User",
            Provider = "Google",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var inactiveUser = new User
        {
            UserId = "inactive-user",
            Email = "inactive@example.com",
            Name = "Inactive User",
            Provider = "Microsoft",
            CreatedAt = DateTime.UtcNow,
            IsActive = false
        };

        await _context.Users.AddRangeAsync(activeUser, inactiveUser);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveUsersAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("active-user", result.First().UserId);
    }

    [Fact]
    public async Task UpdateLastLoginAsync_ShouldUpdateLastLoginTime()
    {
        // Arrange
        var user = new User
        {
            UserId = "test-user-123",
            Email = "test@example.com",
            Name = "Test User",
            Provider = "Google",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            LastLoginAt = DateTime.UtcNow.AddDays(-1)
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var originalLastLogin = user.LastLoginAt;

        // Act
        await _repository.UpdateLastLoginAsync("test-user-123");
        await _context.SaveChangesAsync();

        // Assert
        var updatedUser = await _repository.GetByUserIdAsync("test-user-123");
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.LastLoginAt > originalLastLogin);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}