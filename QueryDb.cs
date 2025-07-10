using Microsoft.EntityFrameworkCore;
using AIMcpAssistant.Data;
using AIMcpAssistant.Data.Entities;

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlite("Data Source=aimcp.db")
    .Options;

using var context = new ApplicationDbContext(options);

Console.WriteLine("=== MODULES ===");
var modules = await context.Modules.ToListAsync();
foreach (var module in modules)
{
    Console.WriteLine($"{module.ModuleId} | {module.Name} | Enabled: {module.IsEnabled} | Registered: {module.IsRegistered}");
}

Console.WriteLine("\n=== USER SUBSCRIPTIONS ===");
var subscriptions = await context.UserModuleSubscriptions.ToListAsync();
foreach (var sub in subscriptions)
{
    Console.WriteLine($"{sub.UserId} | {sub.ModuleId} | Subscribed: {sub.IsSubscribed}");
}

Console.WriteLine("\n=== USERS ===");
var users = await context.Users.ToListAsync();
foreach (var user in users)
{
    Console.WriteLine($"{user.UserId} | {user.Email} | {user.Name}");
}

Console.WriteLine("\nDone.");