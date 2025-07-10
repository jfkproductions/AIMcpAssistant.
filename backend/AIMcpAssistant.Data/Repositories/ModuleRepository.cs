using AIMcpAssistant.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIMcpAssistant.Data.Repositories;

public class ModuleRepository
{
    private readonly ApplicationDbContext _context;

    public ModuleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Module>> GetAllModulesAsync()
    {
        return await _context.Modules
            .OrderBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<Module?> GetModuleByIdAsync(string moduleId)
    {
        return await _context.Modules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId);
    }

    public async Task<Module> UpsertModuleAsync(string moduleId, string name, string description, string version, bool isEnabled = true, bool isRegistered = false)
    {
        var existing = await GetModuleByIdAsync(moduleId);
        
        if (existing != null)
        {
            existing.Name = name;
            existing.Description = description;
            existing.Version = version;
            existing.IsEnabled = isEnabled;
            existing.IsRegistered = isRegistered;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.Modules.Update(existing);
        }
        else
        {
            existing = new Module
            {
                ModuleId = moduleId,
                Name = name,
                Description = description,
                Version = version,
                IsEnabled = isEnabled,
                IsRegistered = isRegistered,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Modules.Add(existing);
        }
        
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task UpdateModuleRegistrationStatusAsync(string moduleId, bool isRegistered)
    {
        var module = await GetModuleByIdAsync(moduleId);
        if (module != null)
        {
            module.IsRegistered = isRegistered;
            module.UpdatedAt = DateTime.UtcNow;
            _context.Modules.Update(module);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateModuleEnabledStatusAsync(string moduleId, bool isEnabled)
    {
        var module = await GetModuleByIdAsync(moduleId);
        if (module != null)
        {
            module.IsEnabled = isEnabled;
            module.UpdatedAt = DateTime.UtcNow;
            _context.Modules.Update(module);
            await _context.SaveChangesAsync();
        }
    }
    
    public async Task RemoveOrphanedModulesAsync()
    {
        var defaultModuleIds = new[] { "EmailMcp", "CalendarMcp", "chatgpt" };
        var orphanedModules = await _context.Modules
            .Where(m => !defaultModuleIds.Contains(m.ModuleId))
            .ToListAsync();
            
        if (orphanedModules.Any())
        {
            _context.Modules.RemoveRange(orphanedModules);
            await _context.SaveChangesAsync();
        }
    }

    public async Task EnsureDefaultModulesExistAsync()
    {
        var defaultModules = new[]
        {
            new { Id = "EmailMcp", Name = "Email Management", Description = "Manage emails, send messages, and check inbox", Version = "1.0.0" },
            new { Id = "CalendarMcp", Name = "Calendar Integration", Description = "Schedule meetings, check calendar, and manage events", Version = "1.0.0" },
            new { Id = "chatgpt", Name = "General AI Assistant", Description = "General AI assistance for questions and conversations", Version = "1.0.0" }
        };

        var defaultModuleIds = defaultModules.Select(m => m.Id).ToHashSet();
        
        // Remove any modules that are not in the default list (cleanup orphaned modules)
        var allModules = await GetAllModulesAsync();
        var orphanedModules = allModules.Where(m => !defaultModuleIds.Contains(m.ModuleId)).ToList();
        
        foreach (var orphanedModule in orphanedModules)
        {
            _context.Modules.Remove(orphanedModule);
        }
        
        if (orphanedModules.Any())
        {
            await _context.SaveChangesAsync();
        }

        // Ensure default modules exist
        foreach (var defaultModule in defaultModules)
        {
            var existing = await GetModuleByIdAsync(defaultModule.Id);
            if (existing == null)
            {
                await UpsertModuleAsync(
                    defaultModule.Id,
                    defaultModule.Name,
                    defaultModule.Description,
                    defaultModule.Version,
                    isEnabled: true,
                    isRegistered: false
                );
            }
        }
    }
}