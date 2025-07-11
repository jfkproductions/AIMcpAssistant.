using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

// Simple entities matching the database schema
public class User
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
    public bool IsActive { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}

public class Module
{
    public int Id { get; set; }
    public string ModuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserModuleSubscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public bool IsSubscribed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TestDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Module> Modules { get; set; }
    public DbSet<UserModuleSubscription> UserModuleSubscriptions { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=backend/AIMcpAssistant.Api/aimcp.db");
    }
}

// Main program
using var context = new TestDbContext();

Console.WriteLine("🔧 Adding test data for email notifications...");

// Add test user
var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
if (existingUser == null)
{
    var testUser = new User
    {
        UserId = "test-user-123",
        Email = "test@example.com",
        Name = "Test User",
        Provider = "Google",
        CreatedAt = DateTime.UtcNow,
        LastLoginAt = DateTime.UtcNow,
        IsActive = true,
        AccessToken = "ya29.test-access-token-12345",
        RefreshToken = "1//test-refresh-token-67890",
        TokenExpiresAt = DateTime.UtcNow.AddHours(1)
    };
    
    context.Users.Add(testUser);
    await context.SaveChangesAsync();
    Console.WriteLine("✅ Test user added");
}
else
{
    existingUser.AccessToken = "ya29.test-access-token-12345";
    existingUser.RefreshToken = "1//test-refresh-token-67890";
    existingUser.TokenExpiresAt = DateTime.UtcNow.AddHours(1);
    existingUser.IsActive = true;
    await context.SaveChangesAsync();
    Console.WriteLine("✅ Test user updated");
}

// Add email module
var emailModule = await context.Modules.FirstOrDefaultAsync(m => m.ModuleId == "email");
if (emailModule == null)
{
    emailModule = new Module
    {
        ModuleId = "email",
        Name = "Email",
        Description = "Email management and notifications",
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow
    };
    
    context.Modules.Add(emailModule);
    await context.SaveChangesAsync();
    Console.WriteLine("✅ Email module added");
}

// Add subscription
var user = await context.Users.FirstAsync(u => u.Email == "test@example.com");
var subscription = await context.UserModuleSubscriptions
    .FirstOrDefaultAsync(s => s.UserId == user.UserId && s.ModuleId == "email");
    
if (subscription == null)
{
    subscription = new UserModuleSubscription
    {
        UserId = user.UserId,
        ModuleId = "email",
        IsSubscribed = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    
    context.UserModuleSubscriptions.Add(subscription);
    await context.SaveChangesAsync();
    Console.WriteLine("✅ Email subscription added");
}

// Verify
var usersWithTokens = await context.Users
    .Where(u => !string.IsNullOrEmpty(u.AccessToken) && u.IsActive)
    .ToListAsync();
    
var subscriptions = await context.UserModuleSubscriptions
    .Where(s => s.ModuleId == "email" && s.IsSubscribed)
    .ToListAsync();

Console.WriteLine($"\n📊 Database Status:");
Console.WriteLine($"  Users with tokens: {usersWithTokens.Count}");
Console.WriteLine($"  Email subscriptions: {subscriptions.Count}");

foreach (var u in usersWithTokens)
{
    var hasEmailSub = subscriptions.Any(s => s.UserId == u.UserId);
    Console.WriteLine($"  - {u.Name} ({u.Email}) - Email subscription: {hasEmailSub}");
}

Console.WriteLine("\n🎉 Test data setup completed!");