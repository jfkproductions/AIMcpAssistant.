using AIMcpAssistant.Core.Models;

namespace AIMcpAssistant.Core.Interfaces;

public interface IMcpModule
{
    /// <summary>
    /// Unique identifier for the MCP module
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Display name for the module
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what this module does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// List of supported command patterns or keywords
    /// </summary>
    List<string> SupportedCommands { get; }
    
    /// <summary>
    /// Priority for command matching (higher = more priority)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Check if this module can handle the given command
    /// </summary>
    /// <param name="input">User input command</param>
    /// <param name="context">User context</param>
    /// <returns>Confidence score (0-1) that this module can handle the command</returns>
    Task<double> CanHandleAsync(string input, UserContext context);
    
    /// <summary>
    /// Handle the command and return a response
    /// </summary>
    /// <param name="input">User input command</param>
    /// <param name="context">User context with authentication tokens</param>
    /// <returns>MCP response</returns>
    Task<McpResponse> HandleCommandAsync(string input, UserContext context);
    
    /// <summary>
    /// Initialize the module with configuration
    /// </summary>
    /// <param name="configuration">Module configuration</param>
    Task InitializeAsync(Dictionary<string, object>? configuration = null);
    
    /// <summary>
    /// Get real-time updates for this module
    /// </summary>
    /// <param name="context">User context</param>
    /// <returns>Stream of real-time updates</returns>
    IAsyncEnumerable<McpUpdate> GetUpdatesAsync(UserContext context);
}