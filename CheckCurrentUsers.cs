using Microsoft.EntityFrameworkCore;
using AIMcpAssistant.Data;
using AIMcpAssistant.Data.Entities;

var connectionString = "Data Source=C:\\1.Pvt\\1.Projects\\AI-MCP-Assistant\\backend\\AIMcpAssistant.Api\\aimcp.db";

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlite(connectionString)
    .Options;

using var context = new ApplicationDbContext(options);

Console.WriteLine("=== Current Users in Database ===");
var users = await context.Users.ToListAsync();

if (!users.Any())
{
    Console.WriteLine("No users found in database.");
}
else
{
    foreach (var user in users)
    {
        Console.WriteLine($"User ID: {user.UserId}");
        Console.WriteLine($"Email: {user.Email}");
        Console.WriteLine($"Name: {user.Name}");
        Console.WriteLine($"Provider: {user.Provider}");
        Console.WriteLine($"Is Active: {user.IsActive}");
        Console.WriteLine($"Has Access Token: {!string.IsNullOrEmpty(user.AccessToken)}");
        Console.WriteLine($"Token Expires At: {user.TokenExpiresAt}");
        Console.WriteLine($"Created At: {user.CreatedAt}");
        Console.WriteLine($"Last Login: {user.LastLoginAt}");
        Console.WriteLine("---");
    }
}

Console.WriteLine("\n=== User Module Subscriptions ===");
var subscriptions = await context.UserModuleSubscriptions
    .Include(s => s.User)
    .Include(s => s.Module)
    .ToListAsync();

if (!subscriptions.Any())
{
    Console.WriteLine("No subscriptions found.");
}
else
{
    foreach (var sub in subscriptions)
    {
        Console.WriteLine($"User: {sub.User?.Email} -> Module: {sub.Module?.Name} (Subscribed: {sub.IsSubscribed})");
    }
}

Console.WriteLine("\n=== Available Modules ===");
var modules = await context.Modules.ToListAsync();
foreach (var module in modules)
{
    Console.WriteLine($"Module: {module.Name} (ID: {module.Id})");
}