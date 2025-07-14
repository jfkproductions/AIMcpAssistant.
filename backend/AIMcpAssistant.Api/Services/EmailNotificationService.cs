using AIMcpAssistant.Core.Models;
using AIMcpAssistant.MCPs;
using AIMcpAssistant.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using AIMcpAssistant.Data.Repositories;
using AIMcpAssistant.Data.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Services;

namespace AIMcpAssistant.Api.Services;

public class EmailNotificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ConcurrentDictionary<string, DateTime> _lastEmailCheck = new();
    private readonly ConcurrentDictionary<string, List<string>> _lastEmailIds = new();
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes (reduced since email checking is disabled)

    public EmailNotificationService(
        IServiceProvider serviceProvider,
        ILogger<EmailNotificationService> logger,
        IHubContext<NotificationHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email notification service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForNewEmails();
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in email notification service");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait longer on error
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Email notification service stopped");
    }

    private async Task CheckForNewEmails()
    {
        using var scope = _serviceProvider.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        try
        {
            _logger.LogInformation("🔍 Starting email check cycle at {Time}", DateTime.UtcNow);
            var usersToNotify = await userRepository.GetUsersWithActiveEmailSubscriptionsAsync();
            
            if (usersToNotify.Any())
            {
                _logger.LogInformation("📋 Found {Count} users with email subscriptions and valid tokens", usersToNotify.Count());
                
                foreach (var user in usersToNotify)
                {
                    if (!string.IsNullOrEmpty(user.AccessToken))
                    {
                        _logger.LogInformation("👤 Checking emails for user {UserId} ({Email})", user.UserId, user.Email);
                        await CheckUserEmails(user.UserId, user.Email, user.AccessToken);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ User {UserId} has no access token", user.UserId);
                    }
                }
            }
            else
            {
                _logger.LogInformation("📭 No users with active email subscriptions and valid tokens found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error checking for new emails");
        }
    }

    private async Task CheckUserEmails(string userId, string userEmail, string accessToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            // Create EmailMcp instance to check for new emails
            var emailMcpLogger = scope.ServiceProvider.GetRequiredService<ILogger<EmailMcp>>();
            var conversationContextService = scope.ServiceProvider.GetRequiredService<IConversationContextService>();
            var tokenRefreshService = scope.ServiceProvider.GetRequiredService<TokenRefreshService>();
            var emailMcp = new EmailMcp(emailMcpLogger, conversationContextService, tokenRefreshService);
            
            // Get recent emails (last 10 emails)
            var context = new UserContext
            {
                UserId = userId,
                Email = userEmail,
                AccessToken = accessToken
            };
            
            var emailsResult = await emailMcp.HandleCommandAsync("read my emails", context);
                
                if (emailsResult.Success && emailsResult.Data != null)
            {
                var emails = emailsResult.Data as IEnumerable<dynamic> ?? new List<dynamic>();
                var emailList = emails.ToList();
                
                var lastCheck = _lastEmailCheck.GetValueOrDefault(userId, DateTime.UtcNow.AddMinutes(-5));
                var lastEmailIds = _lastEmailIds.GetValueOrDefault(userId, new List<string>());
                var newEmails = new List<dynamic>();
                
                foreach (var email in emailList)
                {
                    var emailId = email?.Id?.ToString();
                    var receivedTime = DateTime.UtcNow;
                    
                    if (email?.ReceivedDateTime != null)
                    {
                        DateTime.TryParse(email.ReceivedDateTime.ToString(), out receivedTime);
                    }
                    
                    // Check if this is a new email (not seen before and received after last check)
                    if (!string.IsNullOrEmpty(emailId) && 
                        !lastEmailIds.Contains(emailId) && 
                        receivedTime > lastCheck)
                    {
                        newEmails.Add(email);
                    }
                }
                
                // Send notifications for new emails
                foreach (var newEmail in newEmails)
                {
                    await SendEmailNotification(userId, newEmail);
                }
                
                // Update tracking data
                _lastEmailCheck[userId] = DateTime.UtcNow;
                _lastEmailIds[userId] = emailList
                    .Where(e => e?.Id != null)
                    .Select(e => e.Id.ToString())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();
                
                if (newEmails.Any())
                {
                    _logger.LogInformation("📬 Sent {Count} new email notifications to user {UserId}", newEmails.Count, userId);
                }
                else
                {
                    _logger.LogInformation("📪 No new emails found for user {UserId}", userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking emails for user {UserId}", userId);
        }
    }
    
    private async Task SendEmailNotification(string userId, dynamic email)
    {
        try
        {
            var subject = email.Subject?.ToString() ?? "No Subject";
            var from = email.From?.EmailAddress?.Address?.ToString() ?? "Unknown Sender";
            var snippet = email.BodyPreview?.ToString() ?? "";
            var receivedTime = email.ReceivedDateTime?.ToString() ?? DateTime.UtcNow.ToString();
            
            // Enhanced logging for debugging
            _logger.LogInformation("📧 NEW EMAIL NOTIFICATION: Subject='{Subject}', From='{From}', UserId='{UserId}'", (object)subject, (object)from, (object)userId);
            
            var notification = new McpUpdate
            {
                ModuleId = "EmailMcp",
                Type = "NewEmail",
                Title = "New Email",
                Message = $"New email from {from}: {subject}",
                Data = new
                {
                    EmailId = email.Id?.ToString(),
                    Subject = subject,
                    From = from,
                    Date = receivedTime,
                    Snippet = snippet,
                    IsUnread = email.IsRead == false
                },
                Timestamp = DateTime.UtcNow,
                Priority = "High",
                Metadata = new Dictionary<string, object>
                {
                    ["requiresVoiceResponse"] = true,
                    ["voiceMessage"] = $"New email from {from}. Subject: {subject}",
                    ["followUpActions"] = new[] { "read", "delete", "reply" }
                }
            };

            await NotificationHub.SendNotificationAsync(_hubContext, userId, notification);
            _logger.LogInformation("✅ Email notification sent successfully to user {UserId} via SignalR", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error sending email notification to user {UserId}", userId);
        }
    }
}