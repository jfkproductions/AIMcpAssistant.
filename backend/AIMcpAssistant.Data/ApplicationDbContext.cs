using AIMcpAssistant.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIMcpAssistant.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<CommandHistory> CommandHistories { get; set; }
    public DbSet<UserModuleSubscription> UserModuleSubscriptions { get; set; }
    public DbSet<Module> Modules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            
            entity.HasMany(e => e.CommandHistories)
                  .WithOne()
                  .HasForeignKey("UserId")
                  .HasPrincipalKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasMany(e => e.ModuleSubscriptions)
                  .WithOne(s => s.User)
                  .HasForeignKey(s => s.UserId)
                  .HasPrincipalKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure CommandHistory entity
        modelBuilder.Entity<CommandHistory>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ExecutedAt });
            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.ExecutedAt);
        });
        
        // Configure UserModuleSubscription entity
        modelBuilder.Entity<UserModuleSubscription>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ModuleId }).IsUnique();
            entity.HasIndex(e => e.ModuleId);
        });
        
        // Configure Module entity
        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasIndex(e => e.ModuleId).IsUnique();
            entity.HasMany(e => e.UserSubscriptions)
                  .WithOne()
                  .HasForeignKey(e => e.ModuleId)
                  .HasPrincipalKey(e => e.ModuleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}