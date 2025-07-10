using Microsoft.EntityFrameworkCore;
using AIMcpAssistant.Data;
using AIMcpAssistant.Data.Entities;

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlite("Data Source=c:\\1.Pvt\\1.Projects\\AI-MCP-Assistant\\aimcp.db")
    .Options;

using var context = new ApplicationDbContext(options);

Console.WriteLine("Creating database...");
await context.Database.EnsureCreatedAsync();

Console.WriteLine("Adding initial data...");

// Add a test user
var user = new User
{
    Id = "112766686483169100008",
    Email = "test@example.com",
    Name = "Test User",
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
context.Users.Add(user);

// Add modules with old IDs
var modules = new[]
{
    new Module
    {
        ModuleId = "EmailMcp",
        Name = "Email Management",
        Description = "Manage emails, send messages, and check inbox",
        IsEnabled = true,
        IsRegistered = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    },
    new Module
    {
        ModuleId = "CalendarMcp",
        Name = "Calendar Integration",
        Description = "Schedule meetings, check calendar, and manage events",
        IsEnabled = true,
        IsRegistered = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    },
    new Module
    {
        ModuleId = "chatgpt",
        Name = "General AI Assistant",
        Description = "General AI assistance for questions and conversations",
        IsEnabled = true,
        IsRegistered = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    }
};

context.Modules.AddRange(modules);

// Add user subscriptions with old IDs
var subscriptions = new[]
{
    new UserModuleSubscription
    {
        UserId = user.Id,
        ModuleId = "EmailMcp",
        IsSubscribed = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    },
    new UserModuleSubscription
    {
        UserId = user.Id,
        ModuleId = "CalendarMcp",
        IsSubscribed = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    },
    new UserModuleSubscription
    {
        UserId = user.Id,
        ModuleId = "chatgpt",
        IsSubscribed = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    }
};

context.UserModuleSubscriptions.AddRange(subscriptions);

await context.SaveChangesAsync();

Console.WriteLine("Database initialized successfully!");
Console.WriteLine("\nCurrent state:");
Console.WriteLine("Modules:");
foreach (var module in modules)
{
    Console.WriteLine($"  - {module.ModuleId}: {module.Name}");
}
Console.WriteLine("\nSubscriptions:");
foreach (var sub in subscriptions)
{
    Console.WriteLine($"  - User {sub.UserId} -> {sub.ModuleId}");
}