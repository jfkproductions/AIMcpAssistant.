using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Models;
using AIMcpAssistant.Data.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AIMcpAssistant.Core.Services;

public interface IConversationContextService
{
    Task<List<ConversationMessage>> GetRecentConversationAsync(string userId, int messageCount = 5);
    Task<string> BuildConversationContextAsync(string userId, int messageCount = 5);
    Task<bool> IsFollowUpCommandAsync(string input, string userId);
}

public class ConversationContextService : IConversationContextService
{
    private readonly ILogger<ConversationContextService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ConversationContextService(ILogger<ConversationContextService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<List<ConversationMessage>> GetRecentConversationAsync(string userId, int messageCount = 5)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var commandHistoryRepository = scope.ServiceProvider.GetRequiredService<ICommandHistoryRepository>();
            
            var recentHistory = await commandHistoryRepository.GetUserHistoryAsync(userId, messageCount);
            
            var conversation = new List<ConversationMessage>();
            
            foreach (var history in recentHistory.OrderBy(h => h.ExecutedAt))
            {
                // Add user command
                conversation.Add(new ConversationMessage
                {
                    Role = "user",
                    Content = history.Command,
                    Timestamp = history.ExecutedAt,
                    ModuleId = history.ModuleId
                });
                
                // Add assistant response
                conversation.Add(new ConversationMessage
                {
                    Role = "assistant",
                    Content = history.Response ?? "No response recorded",
                    Timestamp = history.ExecutedAt.AddSeconds(1),
                    ModuleId = history.ModuleId,
                    IsSuccess = history.IsSuccess
                });
            }
            
            return conversation.TakeLast(messageCount * 2).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history for user {UserId}", userId);
            return new List<ConversationMessage>();
        }
    }

    public async Task<string> BuildConversationContextAsync(string userId, int messageCount = 5)
    {
        var conversation = await GetRecentConversationAsync(userId, messageCount);
        
        if (!conversation.Any())
            return "No previous conversation history.";
        
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("Recent conversation history:");
        
        foreach (var message in conversation)
        {
            var timestamp = message.Timestamp.ToString("HH:mm:ss");
            var roleIcon = message.Role == "user" ? "👤" : "🤖";
            contextBuilder.AppendLine($"{timestamp} {roleIcon} {message.Content}");
        }
        
        return contextBuilder.ToString();
    }

    public async Task<bool> IsFollowUpCommandAsync(string input, string userId)
    {
        var recentConversation = await GetRecentConversationAsync(userId, 2);
        
        if (!recentConversation.Any())
            return false;
        
        var lastAssistantMessage = recentConversation
            .Where(m => m.Role == "assistant")
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefault();
        
        if (lastAssistantMessage == null)
            return false;
        
        // Check if the last assistant message was asking for confirmation or follow-up
        var confirmationKeywords = new[] { "yes", "no", "confirm", "cancel", "delete", "reply", "next" };
        var questionKeywords = new[] { "?", "yes or no", "confirm or cancel", "would you like" };
        
        var lastMessageLower = lastAssistantMessage.Content.ToLowerInvariant();
        var inputLower = input.ToLowerInvariant().Trim();
        
        // Check if last message was asking a question
        bool wasAskingQuestion = questionKeywords.Any(keyword => lastMessageLower.Contains(keyword));
        
        // Check if current input is a simple confirmation/response
        bool isSimpleResponse = confirmationKeywords.Any(keyword => inputLower == keyword || inputLower.Contains(keyword));
        
        // Check if the response happened within a reasonable time frame (5 minutes)
        bool isWithinTimeFrame = (DateTime.UtcNow - lastAssistantMessage.Timestamp).TotalMinutes <= 5;
        
        return wasAskingQuestion && isSimpleResponse && isWithinTimeFrame;
    }
}

public class ConversationMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? ModuleId { get; set; }
    public bool IsSuccess { get; set; } = true;
}