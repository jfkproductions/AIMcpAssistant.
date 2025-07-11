#r "nuget: Microsoft.EntityFrameworkCore.Sqlite, 8.0.0"
#r "nuget: Microsoft.EntityFrameworkCore.Design, 8.0.0"

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

// Define entities
public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
    
    // OAuth token storage for background services
    [MaxLength(2000)]
    public string? AccessToken { get; set; }
    
    [MaxLength(2000)]
    public string? RefreshToken { get; set; }
    
    public DateTime? TokenExpiresAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<UserModuleSubscription> ModuleSubscriptions { get; set; } = new List<UserModuleSubscription>();
}

public class UserModuleSubscription
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string ModuleId { get; set; } = string.Empty;
    
    public bool IsSubscribed { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public virtual User User { get; set; } = null!;
}

public class Module
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string ModuleId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public bool IsEnabled { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserModuleSubscription> UserModuleSubscriptions { get; set; }
    public DbSet<Module> Modules { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=backend/AIMcpAssistant.Api/aimcp.db");
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure UserModuleSubscription entity
        modelBuilder.Entity<UserModuleSubscription>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ModuleId }).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ModuleId);
        });
        
        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });
        
        // Configure Module entity
        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasIndex(e => e.ModuleId).IsUnique();
        });
    }
}

// Main execution
using var context = new ApplicationDbContext();

console.WriteLine("🔧 Ensuring database is created and up to date...");
await context.Database.EnsureCreatedAsync();

console.WriteLine("📊 Checking current database state...");

// Check if users exist
var userCount = await context.Users.CountAsync();
console.WriteLine($"👥 Users in database: {userCount}");

if (userCount == 0)
{
    console.WriteLine("➕ Adding test user...");
    
    var testUser = new User
    {
        UserId = "test-user-123",
        Email = "test@example.com",
        Name = "Test User",
        Provider = "Google",
        CreatedAt = DateTime.UtcNow,
        LastLoginAt = DateTime.UtcNow,
        IsActive = true,
        AccessToken = "test-access-token",
        RefreshToken = "test-refresh-token",
        TokenExpiresAt = DateTime.UtcNow.AddHours(1)
    };
    
    context.Users.Add(testUser);
    await context.SaveChangesAsync();
    
    console.WriteLine("✅ Test user added successfully");
}

// Check modules
var moduleCount = await context.Modules.CountAsync();
console.WriteLine($"📦 Modules in database: {moduleCount}");

if (moduleCount == 0)
{
    console.WriteLine("➕ Adding default modules...");
    
    var modules = new[]
    {
        new Module { ModuleId = "email", Name = "Email", Description = "Email management and notifications", IsEnabled = true },
        new Module { ModuleId = "calendar", Name = "Calendar", Description = "Calendar integration", IsEnabled = true },
        new Module { ModuleId = "chatgpt", Name = "ChatGPT", Description = "AI assistant integration", IsEnabled = true }
    };
    
    context.Modules.AddRange(modules);
    await context.SaveChangesAsync();
    
    console.WriteLine("✅ Default modules added successfully");
}

// Check subscriptions
var subscriptionCount = await context.UserModuleSubscriptions.CountAsync();
console.WriteLine($"🔗 User subscriptions in database: {subscriptionCount}");

if (subscriptionCount == 0 && userCount > 0)
{
    console.WriteLine("➕ Adding default subscriptions...");
    
    var user = await context.Users.FirstAsync();
    var modules = await context.Modules.ToListAsync();
    
    foreach (var module in modules)
    {
        var subscription = new UserModuleSubscription
        {
            UserId = user.UserId,
            ModuleId = module.ModuleId,
            IsSubscribed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        context.UserModuleSubscriptions.Add(subscription);
    }
    
    await context.SaveChangesAsync();
    console.WriteLine("✅ Default subscriptions added successfully");
}

// Final check
var usersWithSubscriptions = await context.Users
    .Include(u => u.ModuleSubscriptions)
    .Where(u => u.ModuleSubscriptions.Any(s => s.ModuleId == "email" && s.IsSubscribed))
    .Where(u => u.AccessToken != null && u.AccessToken != "")
    .ToListAsync();

console.WriteLine($"\n📧 Users with email subscriptions and valid tokens: {usersWithSubscriptions.Count}");

foreach (var user in usersWithSubscriptions)
{
    console.WriteLine($"  - {user.Name} ({user.Email}) - Provider: {user.Provider}");
    console.WriteLine($"    Token expires: {user.TokenExpiresAt}");
    console.WriteLine($"    Subscriptions: {user.ModuleSubscriptions.Count(s => s.IsSubscribed)}");
}

console.WriteLine("\n🎉 Database check and fix completed!");