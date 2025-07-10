using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AIMcpAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommandController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ILogger<CommandController> _logger;

    public CommandController(ICommandDispatcher commandDispatcher, ILogger<CommandController> logger)
    {
        _commandDispatcher = commandDispatcher;
        _logger = logger;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessCommand([FromBody] ProcessCommandRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Input))
            {
                return BadRequest(new { error = "Input cannot be empty" });
            }

            var userContext = CreateUserContext();

            // Get last used module from session if no preferred module is specified
            var preferredModule = request.PreferredModule;
            if (string.IsNullOrEmpty(preferredModule))
            {
                preferredModule = HttpContext.Session.GetString("LastUsedModuleId");
            }

            var response = await _commandDispatcher.ProcessCommandAsync(request.Input, userContext, preferredModule);

            // Store the module ID in the session for context
            if (response.Success && response.Metadata.TryGetValue("moduleId", out var moduleId))
            {
                HttpContext.Session.SetString("LastUsedModuleId", moduleId.ToString());
            }

            return Ok(new
            {
                success = response.Success,
                message = response.Message,
                data = response.Data,
                errorCode = response.ErrorCode,
                metadata = response.Metadata,
                suggestedActions = response.SuggestedActions,
                requiresFollowUp = response.RequiresFollowUp,
                followUpPrompt = response.FollowUpPrompt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command: {Input}", request.Input);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("modules")]
    public async Task<IActionResult> GetModules()
    {
        try
        {
            var modules = await _commandDispatcher.GetRegisteredModulesAsync();
            var moduleInfo = modules.Select(m => new
            {
                id = m.Id,
                name = m.Name,
                description = m.Description,
                supportedCommands = m.SupportedCommands,
                priority = m.Priority
            });

            return Ok(new { modules = moduleInfo });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting modules");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeCommand([FromBody] AnalyzeCommandRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Input))
            {
                return BadRequest(new { error = "Input cannot be empty" });
            }

            var userContext = CreateUserContext();
            var (module, confidence) = await _commandDispatcher.FindBestModuleAsync(request.Input, userContext);

            return Ok(new
            {
                input = request.Input,
                bestMatch = module != null ? new
                {
                    moduleId = module.Id,
                    moduleName = module.Name,
                    confidence = confidence
                } : null,
                canHandle = confidence >= 0.3
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing command: {Input}", request.Input);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private UserContext CreateUserContext()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var name = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
        var provider = User.FindFirst("provider")?.Value ?? "unknown";
        var accessToken = User.FindFirst("access_token")?.Value ?? "";
        var refreshToken = User.FindFirst("refresh_token")?.Value ?? "";
        
        // Parse token expiry
        var tokenExpiryStr = User.FindFirst("token_expiry")?.Value;
        var tokenExpiry = DateTime.TryParse(tokenExpiryStr, out var expiry) ? expiry : DateTime.UtcNow.AddHours(1);
        
        // Parse scopes
        var scopesStr = User.FindFirst("scopes")?.Value ?? "";
        var scopes = string.IsNullOrEmpty(scopesStr) ? new List<string>() : scopesStr.Split(',').ToList();
        
        // Additional claims
        var additionalClaims = new Dictionary<string, object>();
        foreach (var claim in User.Claims.Where(c => !IsStandardClaim(c.Type)))
        {
            additionalClaims[claim.Type] = claim.Value;
        }

        return new UserContext
        {
            UserId = userId,
            Email = email,
            Name = name,
            Provider = provider,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenExpiry = tokenExpiry,
            Scopes = scopes,
            AdditionalClaims = additionalClaims
        };
    }

    private static bool IsStandardClaim(string claimType)
    {
        var standardClaims = new[]
        {
            ClaimTypes.NameIdentifier,
            ClaimTypes.Email,
            ClaimTypes.Name,
            "provider",
            "access_token",
            "refresh_token",
            "token_expiry",
            "scopes"
        };
        
        return standardClaims.Contains(claimType);
    }
}

public class ProcessCommandRequest
{
    public string Input { get; set; } = string.Empty;
    public string? PreferredModule { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

public class AnalyzeCommandRequest
{
    public string Input { get; set; } = string.Empty;
}