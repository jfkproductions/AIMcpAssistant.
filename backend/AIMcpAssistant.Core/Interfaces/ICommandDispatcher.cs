using AIMcpAssistant.Core.Models;

namespace AIMcpAssistant.Core.Interfaces;

public interface ICommandDispatcher
{
    /// <summary>
    /// Process a natural language command and route it to the appropriate MCP
    /// </summary>
    /// <param name="input">User input command</param>
    /// <param name="context">User context</param>
    /// <returns>Response from the selected MCP</returns>
    Task<McpResponse> ProcessCommandAsync(string input, UserContext context);
    
    /// <summary>
    /// Process a natural language command with a preferred module
    /// </summary>
    /// <param name="input">User input command</param>
    /// <param name="context">User context</param>
    /// <param name="preferredModuleId">ID of the preferred module to use</param>
    /// <returns>Response from the selected MCP</returns>
    Task<McpResponse> ProcessCommandAsync(string input, UserContext context, string? preferredModuleId);
    
    /// <summary>
    /// Get all registered MCP modules
    /// </summary>
    /// <returns>List of registered MCPs</returns>
    Task<List<IMcpModule>> GetRegisteredModulesAsync();
    
    /// <summary>
    /// Register a new MCP module
    /// </summary>
    /// <param name="module">MCP module to register</param>
    Task RegisterModuleAsync(IMcpModule module);
    
    /// <summary>
    /// Unregister an MCP module
    /// </summary>
    /// <param name="moduleId">ID of the module to unregister</param>
    Task UnregisterModuleAsync(string moduleId);
    
    /// <summary>
    /// Get the best matching MCP for a given input
    /// </summary>
    /// <param name="input">User input</param>
    /// <param name="context">User context</param>
    /// <returns>Best matching MCP and confidence score</returns>
    Task<(IMcpModule? module, double confidence)> FindBestModuleAsync(string input, UserContext context);
}