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

Console.WriteLine("🔧 Setting up test data for email notifications...");

// Check if test user already exists
var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");

if (existingUser == null)
{
    Console.WriteLine("➕ Adding test user...");
    
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
    
    Console.WriteLine("✅ Test user added successfully");
}
else
{
    Console.WriteLine("👤 Test user already exists, updating tokens...");
    
    existingUser.AccessToken = "ya29.test-access-token-12345";
    existingUser.RefreshToken = "1//test-refresh-token-67890";
    existingUser.TokenExpiresAt = DateTime.UtcNow.AddHours(1);
    existingUser.IsActive = true;
    
    await context.SaveChangesAsync();
    
    Console.WriteLine("✅ Test user tokens updated");
}

// Ensure email module exists
var emailModule = await context.Modules.FirstOrDefaultAsync(m => m.ModuleId == "email");
if (emailModule == null)
{
    Console.WriteLine("➕ Adding email module...");
    
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

// Ensure user has email subscription
var user = await context.Users.FirstAsync(u => u.Email == "test@example.com");
var subscription = await context.UserModuleSubscriptions
    .FirstOrDefaultAsync(s => s.UserId == user.UserId && s.ModuleId == "email");
    
if (subscription == null)
{
    Console.WriteLine("➕ Adding email subscription...");
    
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
else if (!subscription.IsSubscribed)
{
    Console.WriteLine("🔄 Enabling email subscription...");
    
    subscription.IsSubscribed = true;
    subscription.UpdatedAt = DateTime.UtcNow;
    
    await context.SaveChangesAsync();
    
    Console.WriteLine("✅ Email subscription enabled");
}

// Final verification
var usersWithEmailSubscriptions = await context.Users
    .Include(u => u.ModuleSubscriptions)
    .Where(u => u.ModuleSubscriptions.Any(s => s.ModuleId == "email" && s.IsSubscribed))
    .Where(u => !string.IsNullOrEmpty(u.AccessToken))
    .Where(u => u.IsActive)
    .ToListAsync();

Console.WriteLine($"\n📧 Users with active email subscriptions and valid tokens: {usersWithEmailSubscriptions.Count}");

foreach (var userWithSub in usersWithEmailSubscriptions)
{
    Console.WriteLine($"  - {userWithSub.Name} ({userWithSub.Email})");
    Console.WriteLine($"    Provider: {userWithSub.Provider}");
    Console.WriteLine($"    Token expires: {userWithSub.TokenExpiresAt}");
    Console.WriteLine($"    Active subscriptions: {userWithSub.ModuleSubscriptions.Count(s => s.IsSubscribed)}");
}

Console.WriteLine("\n🎉 Database setup completed! Email notifications should now work.");
Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();