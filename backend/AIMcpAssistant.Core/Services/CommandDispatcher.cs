using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIMcpAssistant.Core.Services;

public class CommandDispatcher : ICommandDispatcher
{
    private readonly ILogger<CommandDispatcher> _logger;
    private readonly List<IMcpModule> _modules = new();
    private readonly object _lock = new();

    public CommandDispatcher(ILogger<CommandDispatcher> logger)
    {
        _logger = logger;
    }

    public async Task<McpResponse> ProcessCommandAsync(string input, UserContext context)
    {
        return await ProcessCommandAsync(input, context, null);
    }
    
    public async Task<McpResponse> ProcessCommandAsync(string input, UserContext context, string? preferredModuleId)
    {
        try
        {
            _logger.LogInformation("Processing command: {Input} for user: {UserId} with preferred module: {PreferredModule}", input, context.UserId, preferredModuleId ?? "auto");

            IMcpModule? module = null;
            double confidence = 0.0;
            
            // If a preferred module is specified, try to use it first
            if (!string.IsNullOrEmpty(preferredModuleId))
            {
                lock (_lock)
                {
                    module = _modules.FirstOrDefault(m => m.Id.Equals(preferredModuleId, StringComparison.OrdinalIgnoreCase));
                }
                
                if (module != null)
                {
                    try
                    {
                        confidence = await module.CanHandleAsync(input, context);
                        _logger.LogInformation("Preferred module {ModuleId} confidence: {Confidence}", module.Id, confidence);
                        
                        // Use preferred module even with lower confidence, but still require minimum threshold
                        if (confidence < 0.1)
                        {
                            _logger.LogWarning("Preferred module {ModuleId} has very low confidence ({Confidence}), falling back to auto-selection", module.Id, confidence);
                            module = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error checking preferred module {ModuleId}, falling back to auto-selection", module.Id);
                        module = null;
                    }
                }
                else
                {
                    _logger.LogWarning("Preferred module {ModuleId} not found, falling back to auto-selection", preferredModuleId);
                }
            }
            
            // If no preferred module or preferred module failed, use auto-selection
            if (module == null)
            {
                var (autoModule, autoConfidence) = await FindBestModuleAsync(input, context);
                module = autoModule;
                confidence = autoConfidence;
            }

            // If no module can handle with good confidence, try fallback to ChatGPT
            if (module == null || confidence < 0.3)
            {
                _logger.LogInformation("No suitable module found (confidence: {Confidence}), attempting fallback to ChatGPT", confidence);
                
                var chatGptModule = GetChatGptModule();
                if (chatGptModule != null)
                {
                    _logger.LogInformation("Falling back to ChatGPT module for general assistance");
                    
                    var fallbackResponse = await chatGptModule.HandleCommandAsync(input, context);
                    
                    // Add fallback metadata
                    fallbackResponse.Metadata["moduleId"] = chatGptModule.Id;
                    fallbackResponse.Metadata["moduleName"] = chatGptModule.Name;
                    fallbackResponse.Metadata["confidence"] = 0.5; // Default confidence for fallback
                    fallbackResponse.Metadata["isFallback"] = true;
                    fallbackResponse.Metadata["originalConfidence"] = confidence;
                    fallbackResponse.Metadata["preferredModule"] = preferredModuleId ?? "auto";
                    
                    return fallbackResponse;
                }
                
                return McpResponse.CreateError(
                    "I'm not sure how to help with that. Could you please rephrase your request or try a different command?",
                    "NO_MATCHING_MODULE");
            }

            _logger.LogInformation("Selected module: {ModuleId} with confidence: {Confidence}", module.Id, confidence);

            try
            {
                var response = await module.HandleCommandAsync(input, context);
                
                // Check if the module failed to handle the command properly
                if (!response.Success && ShouldFallbackToChatGpt(response))
                {
                    _logger.LogInformation("Module {ModuleId} failed to handle command, attempting fallback to ChatGPT", module.Id);
                    
                    var chatGptModule = GetChatGptModule();
                    if (chatGptModule != null && chatGptModule.Id != module.Id)
                    {
                        var fallbackResponse = await chatGptModule.HandleCommandAsync(input, context);
                        
                        // Add fallback metadata
                        fallbackResponse.Metadata["moduleId"] = chatGptModule.Id;
                        fallbackResponse.Metadata["moduleName"] = chatGptModule.Name;
                        fallbackResponse.Metadata["confidence"] = 0.5;
                        fallbackResponse.Metadata["isFallback"] = true;
                        fallbackResponse.Metadata["originalModule"] = module.Id;
                        fallbackResponse.Metadata["originalError"] = response.Message;
                        fallbackResponse.Metadata["preferredModule"] = preferredModuleId ?? "auto";
                        
                        return fallbackResponse;
                    }
                }
                
                // Add module metadata to response
                response.Metadata["moduleId"] = module.Id;
                response.Metadata["moduleName"] = module.Name;
                response.Metadata["confidence"] = confidence;
                response.Metadata["isFallback"] = false;
                response.Metadata["preferredModule"] = preferredModuleId ?? "auto";

                return response;
            }
            catch (Exception moduleEx)
            {
                _logger.LogError(moduleEx, "Error in module {ModuleId}, attempting fallback", module.Id);
                
                var chatGptModule = GetChatGptModule();
                if (chatGptModule != null && chatGptModule.Id != module.Id)
                {
                    try
                    {
                        var fallbackResponse = await chatGptModule.HandleCommandAsync(input, context);
                        
                        fallbackResponse.Metadata["moduleId"] = chatGptModule.Id;
                        fallbackResponse.Metadata["moduleName"] = chatGptModule.Name;
                        fallbackResponse.Metadata["confidence"] = 0.5;
                        fallbackResponse.Metadata["isFallback"] = true;
                        fallbackResponse.Metadata["originalModule"] = module.Id;
                        fallbackResponse.Metadata["originalError"] = moduleEx.Message;
                        fallbackResponse.Metadata["preferredModule"] = preferredModuleId ?? "auto";
                        
                        return fallbackResponse;
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "Fallback to ChatGPT also failed");
                    }
                }
                
                throw; // Re-throw if fallback also fails
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command: {Input}", input);
            return McpResponse.CreateError("An error occurred while processing your request. Please try again.", "PROCESSING_ERROR");
        }
    }

    public async Task<List<IMcpModule>> GetRegisteredModulesAsync()
    {
        await Task.CompletedTask;
        lock (_lock)
        {
            return new List<IMcpModule>(_modules);
        }
    }

    public async Task RegisterModuleAsync(IMcpModule module)
    {
        await Task.CompletedTask;
        lock (_lock)
        {
            // Remove existing module with same ID
            _modules.RemoveAll(m => m.Id == module.Id);
            _modules.Add(module);
            
            // Sort by priority (higher priority first)
            _modules.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        
        _logger.LogInformation("Registered MCP module: {ModuleId} - {ModuleName}", module.Id, module.Name);
    }

    public async Task UnregisterModuleAsync(string moduleId)
    {
        await Task.CompletedTask;
        lock (_lock)
        {
            var removed = _modules.RemoveAll(m => m.Id == moduleId);
            if (removed > 0)
            {
                _logger.LogInformation("Unregistered MCP module: {ModuleId}", moduleId);
            }
        }
    }

    public async Task<(IMcpModule? module, double confidence)> FindBestModuleAsync(string input, UserContext context)
    {
        List<IMcpModule> modules;
        lock (_lock)
        {
            modules = new List<IMcpModule>(_modules);
        }

        if (!modules.Any())
        {
            return (null, 0);
        }

        var tasks = modules.Select(async module =>
        {
            try
            {
                var confidence = await module.CanHandleAsync(input, context);
                return new { Module = module, Confidence = confidence };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if module {ModuleId} can handle command", module.Id);
                return new { Module = module, Confidence = 0.0 };
            }
        });

        var results = await Task.WhenAll(tasks);
        var bestMatch = results
            .Where(r => r.Confidence > 0)
            .OrderByDescending(r => r.Confidence)
            .ThenByDescending(r => r.Module.Priority)
            .FirstOrDefault();

        return bestMatch != null ? (bestMatch.Module, bestMatch.Confidence) : (null, 0);
    }

    private IMcpModule? GetChatGptModule()
    {
        lock (_lock)
        {
            return _modules.FirstOrDefault(m => m.Id == "chatgpt");
        }
    }

    private bool ShouldFallbackToChatGpt(McpResponse response)
    {
        // Fallback to ChatGPT if:
        // 1. The response indicates the module couldn't handle the request
        // 2. The response has specific error codes that suggest the module can't help
        // 3. The response message suggests the module doesn't understand the request
        
        if (response.Success)
            return false;
            
        var errorCode = response.ErrorCode?.ToUpper();
        var message = response.Message?.ToLower() ?? "";
        
        // Check for specific error codes that indicate the module can't handle the request
        var fallbackErrorCodes = new[] 
        {
            "NOT_SUPPORTED",
            "INVALID_COMMAND", 
            "UNKNOWN_COMMAND",
            "CANNOT_HANDLE",
            "NOT_UNDERSTOOD",
            "UNSUPPORTED_OPERATION"
        };
        
        if (errorCode != null && fallbackErrorCodes.Contains(errorCode))
            return true;
            
        // Check for message patterns that suggest the module can't help
        var fallbackMessagePatterns = new[]
        {
            "don't understand",
            "can't help",
            "not supported",
            "invalid command",
            "unknown command",
            "cannot handle",
            "not sure how",
            "unable to process"
        };
        
        return fallbackMessagePatterns.Any(pattern => message.Contains(pattern));
    }
}