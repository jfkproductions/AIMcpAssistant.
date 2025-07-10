using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AIMcpAssistant.Core.Services;

public abstract class BaseMcpModule : IMcpModule
{
    protected readonly ILogger Logger;
    protected Dictionary<string, object> Configuration { get; private set; } = new();

    protected BaseMcpModule(ILogger logger)
    {
        Logger = logger;
    }

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract List<string> SupportedCommands { get; }
    public virtual int Priority => 1;

    public virtual async Task<double> CanHandleAsync(string input, UserContext context)
    {
        await Task.CompletedTask;
        
        var normalizedInput = input.ToLowerInvariant().Trim();
        var maxConfidence = 0.0;

        foreach (var command in SupportedCommands)
        {
            var confidence = CalculateCommandConfidence(normalizedInput, command.ToLowerInvariant());
            maxConfidence = Math.Max(maxConfidence, confidence);
        }

        // Additional context-based confidence adjustments
        maxConfidence = AdjustConfidenceForContext(maxConfidence, normalizedInput, context);

        return Math.Min(maxConfidence, 1.0);
    }

    protected virtual double CalculateCommandConfidence(string input, string command)
    {
        // Exact match
        if (input == command)
            return 1.0;

        // Contains all words from command
        var commandWords = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var inputWords = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var matchingWords = commandWords.Count(cw => 
            inputWords.Any(iw => iw.Contains(cw) || cw.Contains(iw)));
        
        if (matchingWords == commandWords.Length)
            return 0.8;

        // Partial word matches
        var partialMatches = commandWords.Count(cw => 
            inputWords.Any(iw => 
                LevenshteinDistance(iw, cw) <= Math.Max(1, Math.Min(iw.Length, cw.Length) / 3)));
        
        if (partialMatches > 0)
            return Math.Min(0.6, (double)partialMatches / commandWords.Length);

        // Regex pattern matching for more flexible commands
        try
        {
            var pattern = command.Replace("*", ".*").Replace("?", ".");
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                return 0.7;
        }
        catch
        {
            // Ignore regex errors
        }

        return 0.0;
    }

    protected virtual double AdjustConfidenceForContext(double baseConfidence, string input, UserContext context)
    {
        // Override in derived classes for context-specific adjustments
        return baseConfidence;
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    public virtual async Task InitializeAsync(Dictionary<string, object>? configuration = null)
    {
        Configuration = configuration ?? new Dictionary<string, object>();
        await OnInitializeAsync();
        Logger.LogInformation("Initialized MCP module: {ModuleId}", Id);
    }

    protected virtual async Task OnInitializeAsync()
    {
        await Task.CompletedTask;
    }

    public abstract Task<McpResponse> HandleCommandAsync(string input, UserContext context);

    public virtual async IAsyncEnumerable<McpUpdate> GetUpdatesAsync(UserContext context)
    {
        await Task.CompletedTask;
        yield break;
    }

    protected McpResponse Success(string message, object? data = null)
    {
        return McpResponse.CreateSuccess(message, data);
    }

    protected McpResponse Error(string message, string? errorCode = null)
    {
        return McpResponse.CreateError(message, errorCode);
    }

    protected void LogInfo(string message, params object[] args)
    {
        Logger.LogInformation($"[{Id}] {message}", args);
    }

    protected void LogError(Exception ex, string message, params object[] args)
    {
        Logger.LogError(ex, $"[{Id}] {message}", args);
    }
}