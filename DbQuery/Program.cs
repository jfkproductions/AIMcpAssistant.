using Microsoft.EntityFrameworkCore;
using AIMcpAssistant.Data;

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlite("Data Source=C:\\1.Pvt\\1.Projects\\AI-MCP-Assistant\\backend\\AIMcpAssistant.Api\\aimcp.db")
    .Options;

using var context = new ApplicationDbContext(options);

Console.WriteLine("=== CURRENT STATE ===");
// Show current data using Entity Framework
var subscriptions = context.UserModuleSubscriptions.ToList();
var modules = context.Modules.ToList();

Console.WriteLine("Current UserModuleSubscriptions:");
foreach (var sub in subscriptions)
{
    Console.WriteLine($"  Id: {sub.Id}, UserId: {sub.UserId}, ModuleId: {sub.ModuleId}, IsSubscribed: {sub.IsSubscribed}");
}

Console.WriteLine("\nCurrent Modules:");
foreach (var module in modules)
{
    Console.WriteLine($"  Id: {module.Id}, ModuleId: {module.ModuleId}, Name: {module.Name}");
}

// Get template user ID
var userIdResult = subscriptions.FirstOrDefault(s => s.ModuleId == "chatgpt")?.UserId;
if (userIdResult == null)
{
    Console.WriteLine("\nERROR: No chatgpt subscription found");
    return;
}

Console.WriteLine($"\nUsing UserId: {userIdResult} as template");

Console.WriteLine("\n=== STEP 1: REMOVE PROBLEMATIC SUBSCRIPTIONS ===");
var deletedEmail = await context.Database.ExecuteSqlRawAsync("DELETE FROM UserModuleSubscriptions WHERE ModuleId = 'EmailMcp'");
Console.WriteLine($"Deleted {deletedEmail} EmailMcp subscriptions");

var deletedCalendar = await context.Database.ExecuteSqlRawAsync("DELETE FROM UserModuleSubscriptions WHERE ModuleId = 'CalendarMcp'");
Console.WriteLine($"Deleted {deletedCalendar} CalendarMcp subscriptions");

Console.WriteLine("\n=== STEP 2: UPDATE MODULE IDS ===");
var updatedEmailModule = await context.Database.ExecuteSqlRawAsync("UPDATE Modules SET ModuleId = 'email', UpdatedAt = datetime('now') WHERE ModuleId = 'EmailMcp'");
Console.WriteLine($"Updated {updatedEmailModule} EmailMcp module records to 'email'");

var updatedCalendarModule = await context.Database.ExecuteSqlRawAsync("UPDATE Modules SET ModuleId = 'calendar', UpdatedAt = datetime('now') WHERE ModuleId = 'CalendarMcp'");
Console.WriteLine($"Updated {updatedCalendarModule} CalendarMcp module records to 'calendar'");

Console.WriteLine("\n=== STEP 3: ADD BACK SUBSCRIPTIONS WITH CORRECT IDS ===");
var insertedEmail = await context.Database.ExecuteSqlRawAsync($"INSERT INTO UserModuleSubscriptions (UserId, ModuleId, IsSubscribed, CreatedAt, UpdatedAt) VALUES ('{userIdResult}', 'email', 1, datetime('now'), datetime('now'))");
Console.WriteLine($"Added {insertedEmail} email subscription");

var insertedCalendar = await context.Database.ExecuteSqlRawAsync($"INSERT INTO UserModuleSubscriptions (UserId, ModuleId, IsSubscribed, CreatedAt, UpdatedAt) VALUES ('{userIdResult}', 'calendar', 1, datetime('now'), datetime('now'))");
Console.WriteLine($"Added {insertedCalendar} calendar subscription");

Console.WriteLine("\n=== FINAL STATE ===");
var finalSubscriptions = context.UserModuleSubscriptions.ToList();
var finalModules = context.Modules.ToList();

Console.WriteLine("Final UserModuleSubscriptions:");
foreach (var sub in finalSubscriptions)
{
    Console.WriteLine($"  Id: {sub.Id}, UserId: {sub.UserId}, ModuleId: {sub.ModuleId}, IsSubscribed: {sub.IsSubscribed}");
}

Console.WriteLine("\nFinal Modules:");
foreach (var module in finalModules)
{
    Console.WriteLine($"  Id: {module.Id}, ModuleId: {module.ModuleId}, Name: {module.Name}");
}

Console.WriteLine("\n✅ Database update completed successfully!");
