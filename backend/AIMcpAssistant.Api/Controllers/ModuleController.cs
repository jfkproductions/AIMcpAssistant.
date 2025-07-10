using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Models;
using AIMcpAssistant.Data.Interfaces;
using AIMcpAssistant.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using AIMcpAssistant.Data.Repositories;
using System.Security.Claims;

namespace AIMcpAssistant.Api.Controllers;

public class UpdateModuleSettingsRequest
{
    public string ModuleId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class UpdateUserSubscriptionRequest
{
    public string ModuleId { get; set; } = string.Empty;
    public bool IsSubscribed { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ModuleController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ICommandDispatcher _dispatcher;
    private readonly ILogger<ModuleController> _logger;
    private readonly UserModuleSubscriptionRepository _subscriptionRepository;
    private readonly ModuleRepository _moduleRepository;
    private readonly IUserRepository _userRepository;

    public ModuleController(
        IConfiguration configuration,
        ICommandDispatcher dispatcher,
        ILogger<ModuleController> logger,
        UserModuleSubscriptionRepository subscriptionRepository,
        ModuleRepository moduleRepository,
        IUserRepository userRepository)
    {
        _configuration = configuration;
        _dispatcher = dispatcher;
        _logger = logger;
        _subscriptionRepository = subscriptionRepository;
        _moduleRepository = moduleRepository;
        _userRepository = userRepository;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetModuleSettings()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }
            
            // Ensure default modules exist in database
            await _moduleRepository.EnsureDefaultModulesExistAsync();

            var registeredModules = await _dispatcher.GetRegisteredModulesAsync();
            var userSubscriptions = await _subscriptionRepository.GetUserSubscriptionsAsync(userId);
            var dbModules = await _moduleRepository.GetAllModulesAsync();
            var moduleSettings = new Dictionary<string, object>();

            // Create mapping for frontend compatibility
            var frontendModuleMapping = new Dictionary<string, string>
            {
                { "EmailMcp", "EmailMcp" },
                { "CalendarMcp", "CalendarMcp" },
                { "chatgpt", "chatgpt" }
            };

            foreach (var dbModule in dbModules)
            {
                // Check configuration override for enabled status
                var configEnabled = _configuration.GetValue<bool?>($"McpModules:{dbModule.ModuleId}:Enabled");
                var isEnabled = configEnabled ?? dbModule.IsEnabled; // Use config if set, otherwise use database value
                
                var isRegistered = registeredModules.Any(m => m.Id == dbModule.ModuleId);
                var userSubscription = userSubscriptions.FirstOrDefault(s => s.ModuleId == dbModule.ModuleId);
                var isSubscribed = userSubscription?.IsSubscribed ?? false; // Default to not subscribed to avoid unwanted notifications
                
                // Use the backend module ID as the key for consistency
                var moduleKey = frontendModuleMapping.ContainsKey(dbModule.ModuleId) ? dbModule.ModuleId : dbModule.ModuleId;
                
                moduleSettings[moduleKey] = new
                {
                    Enabled = isEnabled,
                    Registered = isRegistered,
                    IsSubscribed = isSubscribed,
                    Name = dbModule.Name,
                    Description = dbModule.Description,
                    Version = dbModule.Version
                };
            }

            return Ok(new { modules = moduleSettings });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module settings");
            return StatusCode(500, new { error = "Failed to get module settings" });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetModuleStatus()
    {
        try
        {
            var modules = await _dispatcher.GetRegisteredModulesAsync();
            return Ok(new
            {
                status = "running",
                modulesCount = modules.Count,
                modules = modules.Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    description = m.Description,
                    priority = m.Priority,
                    enabled = true // If it's registered, it's enabled
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module status");
            return StatusCode(500, new { error = "Failed to get module status" });
        }
    }

    [HttpPost("settings")]
    public async Task<IActionResult> UpdateModuleSettings([FromBody] UpdateModuleSettingsRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ModuleId))
            {
                return BadRequest(new { error = "ModuleId is required" });
            }

            // Update the database module
            await _moduleRepository.UpdateModuleEnabledStatusAsync(request.ModuleId, request.Enabled);
            
            // Also update the configuration for runtime changes
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            var json = await System.IO.File.ReadAllTextAsync(configPath);
            var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            
            if (config == null)
            {
                return BadRequest(new { error = "Invalid configuration file" });
            }

            // Update the McpModules section
            if (!config.ContainsKey("McpModules"))
            {
                config["McpModules"] = new Dictionary<string, object>();
            }

            var mcpModules = config["McpModules"] as System.Text.Json.JsonElement?;
            var mcpModulesDict = new Dictionary<string, object>();
            
            if (mcpModules.HasValue)
            {
                mcpModulesDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(mcpModules.Value.GetRawText()) ?? new Dictionary<string, object>();
            }

            // Update the specific module setting
            if (!mcpModulesDict.ContainsKey(request.ModuleId))
            {
                mcpModulesDict[request.ModuleId] = new Dictionary<string, object>();
            }

            var moduleConfig = mcpModulesDict[request.ModuleId] as System.Text.Json.JsonElement?;
            var moduleConfigDict = new Dictionary<string, object>();
            
            if (moduleConfig.HasValue)
            {
                moduleConfigDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(moduleConfig.Value.GetRawText()) ?? new Dictionary<string, object>();
            }

            moduleConfigDict["Enabled"] = request.Enabled;
            mcpModulesDict[request.ModuleId] = moduleConfigDict;
            config["McpModules"] = mcpModulesDict;

            // Write back to file
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            };
            var updatedJson = System.Text.Json.JsonSerializer.Serialize(config, options);
            await System.IO.File.WriteAllTextAsync(configPath, updatedJson);

            _logger.LogInformation($"Updated module {request.ModuleId} enabled status to {request.Enabled}");

            return Ok(new { message = "Module settings updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating module settings");
            return StatusCode(500, new { error = "Failed to update module settings" });
        }
    }

    [HttpPost("subscription")]
    public async Task<IActionResult> UpdateUserSubscription([FromBody] UpdateUserSubscriptionRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            // Ensure user exists in database before creating subscription
            await EnsureUserExistsAsync(userId);

            // Validate module ID
            var validModuleIds = new[] { "email", "calendar", "chatgpt" };
            if (!validModuleIds.Contains(request.ModuleId))
            {
                return BadRequest(new { error = "Invalid module ID" });
            }

            // Check if the module is enabled in configuration
            var isEnabled = _configuration.GetValue<bool>($"McpModules:{request.ModuleId}:Enabled", true);
            if (!isEnabled)
            {
                return BadRequest(new { error = "Module is disabled in configuration" });
            }

            // Use the module ID directly for database operations
            var subscription = await _subscriptionRepository.UpsertSubscriptionAsync(userId, request.ModuleId, request.IsSubscribed);

            _logger.LogInformation($"User {userId} {(request.IsSubscribed ? "subscribed to" : "unsubscribed from")} module {request.ModuleId}");

            return Ok(new 
            { 
                success = true, 
                message = $"Successfully {(request.IsSubscribed ? "subscribed to" : "unsubscribed from")} {GetModuleName(request.ModuleId)}",
                subscription = new
                {
                    moduleId = subscription.ModuleId,
                    isSubscribed = subscription.IsSubscribed,
                    updatedAt = subscription.UpdatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user subscription");
            return StatusCode(500, new { error = "Failed to update subscription" });
        }
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetUserSubscriptions()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var subscriptions = await _subscriptionRepository.GetUserSubscriptionsAsync(userId);
            
            var result = subscriptions.Select(s => new
            {
                moduleId = s.ModuleId,
                isSubscribed = s.IsSubscribed,
                moduleName = GetModuleName(s.ModuleId),
                moduleDescription = GetModuleDescription(s.ModuleId),
                createdAt = s.CreatedAt,
                updatedAt = s.UpdatedAt
            });

            return Ok(new { subscriptions = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user subscriptions");
            return StatusCode(500, new { error = "Failed to get subscriptions" });
        }
    }

    private async Task EnsureUserExistsAsync(string userId)
    {
        var existingUser = await _userRepository.GetByUserIdAsync(userId);
        if (existingUser == null)
        {
            // Extract user information from claims
            var email = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var name = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var givenName = User.FindFirst(ClaimTypes.GivenName)?.Value ?? "";
            var surname = User.FindFirst(ClaimTypes.Surname)?.Value ?? "";

            // Create new user
            var newUser = new User
            {
                UserId = userId,
                Email = email,
                Name = !string.IsNullOrEmpty(name) ? name : $"{givenName} {surname}".Trim(),
                Provider = "OAuth",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(newUser);
            _logger.LogInformation($"Created new user: {userId} ({email})");
        }
        else
        {
            // Update last login time
            await _userRepository.UpdateLastLoginAsync(userId);
        }
    }

    private string GetModuleName(string moduleId)
    {
        return moduleId switch
        {
            "email" => "Email Management",
            "calendar" => "Calendar Integration",
            "chatgpt" => "General AI Assistant",
            _ => moduleId
        };
    }

    private string GetModuleDescription(string moduleId)
    {
        return moduleId switch
        {
            "email" => "Manage emails, send messages, and check inbox",
            "calendar" => "Schedule meetings, check calendar, and manage events",
            "chatgpt" => "General AI assistance for questions and conversations",
            _ => "MCP Module"
        };
    }

    private string GetModuleVersion(string moduleId)
    {
        return moduleId switch
        {
            "email" => "1.0.0",
            "calendar" => "1.0.0",
            "chatgpt" => "1.0.0",
            _ => "1.0.0"
        };
    }
}