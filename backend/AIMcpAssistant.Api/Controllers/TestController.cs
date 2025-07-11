using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AIMcpAssistant.Api.Hubs;
using AIMcpAssistant.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AIMcpAssistant.Data.Repositories;
using AIMcpAssistant.MCPs;
using AIMcpAssistant.Core.Services;
using AIMcpAssistant.Data.Interfaces;

namespace AIMcpAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TestController : ControllerBase
    {
        private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<TestController> _logger;
    private readonly UserModuleSubscriptionRepository _subscriptionRepository;
    private readonly EmailMcp _emailMcp;
    private readonly IUserRepository _userRepository;

    public TestController(IHubContext<NotificationHub> hubContext, ILogger<TestController> logger, UserModuleSubscriptionRepository subscriptionRepository, ILogger<EmailMcp> emailMcpLogger, IConversationContextService conversationContextService, IUserRepository userRepository)
    {
        _hubContext = hubContext;
        _logger = logger;
        _subscriptionRepository = subscriptionRepository;
        _emailMcp = new EmailMcp(emailMcpLogger, conversationContextService);
        _userRepository = userRepository;
    }

        [HttpPost("email-notification")]
        public async Task<IActionResult> TestEmailNotification([FromBody] TestEmailNotificationRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { success = false, message = "User not authenticated" });
                }

                // Ensure user is subscribed to email module for testing
                await _subscriptionRepository.UpsertSubscriptionAsync(userId, "email", true);
                _logger.LogInformation("🧪 TEST: Ensured user {UserId} is subscribed to email module", userId);

                // Get user details for sending actual email
                var user = await _userRepository.GetByUserIdAsync(userId);
                if (user == null || string.IsNullOrEmpty(user.Email) || string.IsNullOrEmpty(user.AccessToken))
                {
                    return BadRequest(new { success = false, message = "User not found or missing email/access token" });
                }

                _logger.LogInformation("🧪 TEST: Sending actual test email to user {UserId} at {Email}", userId, user.Email);

                // Create user context for EmailMcp
                var userContext = new UserContext
                {
                    UserId = userId,
                    Email = user.Email,
                    AccessToken = user.AccessToken,
                    Provider = user.Provider ?? "Microsoft" // Default to Microsoft if not specified
                };

                // Send actual email using EmailMcp
                var emailResult = await _emailMcp.HandleCommandAsync(
                    $"send email to {request.TestEmail} with subject '{request.Subject}' and message '{request.Content}'",
                    userContext
                );

                if (!emailResult.Success)
                {
                    _logger.LogError("❌ TEST: Failed to send test email: {Error}", emailResult.Message);
                    return StatusCode(500, new { success = false, message = $"Failed to send test email: {emailResult.Message}" });
                }

                // Also create a test email notification for the UI
                var testNotification = new McpUpdate
                {
                    ModuleId = "EmailMcp",
                    Type = "NewEmail",
                    Title = "New Email (TEST)",
                    Message = $"TEST: New email from {request.From}: {request.Subject}",
                    Data = new
                    {
                        EmailId = $"test-{Guid.NewGuid()}",
                        Subject = request.Subject,
                        From = request.From,
                        Date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        Snippet = request.Content,
                        IsUnread = true,
                        IsTest = true
                    },
                    Timestamp = DateTime.UtcNow,
                    Priority = "High",
                    Metadata = new Dictionary<string, object>
                    {
                        ["requiresVoiceResponse"] = true,
                        ["voiceMessage"] = $"Test email notification from {request.From}. Subject: {request.Subject}",
                        ["followUpActions"] = new[] { "read", "delete", "reply" },
                        ["isTest"] = true
                    }
                };

                // Send the notification via SignalR
                await NotificationHub.SendNotificationAsync(_hubContext, userId, testNotification);
                
                _logger.LogInformation("✅ TEST: Test email sent to {TestEmail} and notification sent to user {UserId}", request.TestEmail, userId);

                return Ok(new 
                { 
                    success = true, 
                    message = "Test email sent successfully and notification created",
                    data = new
                    {
                        userId = userId,
                        testEmail = request.TestEmail,
                        subject = request.Subject,
                        from = request.From,
                        timestamp = DateTime.UtcNow,
                        emailResult = emailResult.Message
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ TEST: Error sending test email notification");
                return StatusCode(500, new { success = false, message = $"Error sending test notification: {ex.Message}" });
            }
        }
    }

    public class TestEmailNotificationRequest
    {
        public string TestEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}