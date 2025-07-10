using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using AIMcpAssistant.Core.Models;

namespace AIMcpAssistant.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;
    private static readonly Dictionary<string, string> _userConnections = new();
    private static readonly object _lock = new();

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (!string.IsNullOrEmpty(userId))
        {
            lock (_lock)
            {
                _userConnections[userId] = Context.ConnectionId;
            }
            
            _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
            
            // Join user-specific group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            
            // Notify client of successful connection
            await Clients.Caller.SendAsync("Connected", new { userId, connectionId = Context.ConnectionId });
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (!string.IsNullOrEmpty(userId))
        {
            lock (_lock)
            {
                _userConnections.Remove(userId);
            }
            
            _logger.LogInformation("User {UserId} disconnected from connection {ConnectionId}", userId, Context.ConnectionId);
            
            // Remove from user-specific group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    // Client can subscribe to specific MCP updates
    public async Task SubscribeToModule(string moduleId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"module_{moduleId}");
            _logger.LogInformation("User {UserId} subscribed to module {ModuleId}", userId, moduleId);
            
            await Clients.Caller.SendAsync("SubscriptionConfirmed", new { moduleId });
        }
    }

    // Client can unsubscribe from specific MCP updates
    public async Task UnsubscribeFromModule(string moduleId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"module_{moduleId}");
            _logger.LogInformation("User {UserId} unsubscribed from module {ModuleId}", userId, moduleId);
            
            await Clients.Caller.SendAsync("UnsubscriptionConfirmed", new { moduleId });
        }
    }

    // Send a message to a specific user
    public static async Task SendToUserAsync(IHubContext<NotificationHub> hubContext, string userId, string method, object data)
    {
        await hubContext.Clients.Group($"user_{userId}").SendAsync(method, data);
    }

    // Send a message to all users subscribed to a specific module
    public static async Task SendToModuleSubscribersAsync(IHubContext<NotificationHub> hubContext, string moduleId, string method, object data)
    {
        await hubContext.Clients.Group($"module_{moduleId}").SendAsync(method, data);
    }

    // Send a notification update
    public static async Task SendNotificationAsync(IHubContext<NotificationHub> hubContext, string userId, McpUpdate update)
    {
        await SendToUserAsync(hubContext, userId, "NotificationReceived", new
        {
            id = Guid.NewGuid().ToString(),
            moduleId = update.ModuleId,
            type = update.Type,
            title = update.Title,
            message = update.Message,
            data = update.Data,
            timestamp = update.Timestamp,
            priority = update.Priority,
            metadata = update.Metadata
        });
    }

    // Send a command response
    public static async Task SendCommandResponseAsync(IHubContext<NotificationHub> hubContext, string userId, McpResponse response)
    {
        await SendToUserAsync(hubContext, userId, "CommandResponseReceived", new
        {
            success = response.Success,
            message = response.Message,
            data = response.Data,
            errorCode = response.ErrorCode,
            metadata = response.Metadata,
            suggestedActions = response.SuggestedActions,
            requiresFollowUp = response.RequiresFollowUp,
            followUpPrompt = response.FollowUpPrompt,
            timestamp = DateTime.UtcNow
        });
    }

    // Send real-time status updates
    public static async Task SendStatusUpdateAsync(IHubContext<NotificationHub> hubContext, string userId, string status, object? data = null)
    {
        await SendToUserAsync(hubContext, userId, "StatusUpdate", new
        {
            status,
            data,
            timestamp = DateTime.UtcNow
        });
    }

    // Get connected users count (for admin purposes)
    public static int GetConnectedUsersCount()
    {
        lock (_lock)
        {
            return _userConnections.Count;
        }
    }

    // Check if a user is connected
    public static bool IsUserConnected(string userId)
    {
        lock (_lock)
        {
            return _userConnections.ContainsKey(userId);
        }
    }

    // Get connection ID for a user
    public static string? GetUserConnectionId(string userId)
    {
        lock (_lock)
        {
            return _userConnections.TryGetValue(userId, out var connectionId) ? connectionId : null;
        }
    }
}