using Microsoft.EntityFrameworkCore;
using AIMcpAssistant.Data;

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlite("Data Source=C:\\1.Pvt\\1.Projects\\AI-MCP-Assistant\\backend\\AIMcpAssistant.Api\\aimcp.db")
    .Options;

using var context = new ApplicationDbContext(options);

Console.WriteLine("=== CHECKING USERS ===");
var users = context.Users.ToList();

Console.WriteLine($"Found {users.Count} users in database:");
if (users.Count == 0)
{
    Console.WriteLine("No users found in database!");
    Console.WriteLine("You need to create a user first.");
}
else
{
    foreach (var user in users)
    {
        Console.WriteLine($"UserId: {user.UserId}");
        Console.WriteLine($"Email: {user.Email}");
        Console.WriteLine($"Name: {user.Name}");
        Console.WriteLine($"Provider: {user.Provider}");
        Console.WriteLine($"HasAccessToken: {!string.IsNullOrEmpty(user.AccessToken)}");
        Console.WriteLine($"TokenExpiresAt: {user.TokenExpiresAt}");
        Console.WriteLine($"IsActive: {user.IsActive}");
        Console.WriteLine("---");
    }
}

Console.WriteLine("\nDone.");