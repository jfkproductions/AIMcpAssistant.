using AIMcpAssistant.Core.Models;
using AIMcpAssistant.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Me.Messages.Item.Reply;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace AIMcpAssistant.MCPs;

public class EmailMcp : BaseMcpModule
{
    private readonly Dictionary<string, McpResponse> _lastResponse = new();
    private readonly Dictionary<string, dynamic> _lastEmailContext = new();
    public override string Id => "email";
    public override string Name => "Email Manager";
    public override string Description => "Manage emails from Google Gmail and Microsoft Outlook";
    public override int Priority => 10;

    public override List<string> SupportedCommands => new()
    {
        "read emails", "check emails", "show emails", "list emails",
        "read my emails", "check my inbox", "show my messages",
        "delete email", "remove email", "trash email",
        "reply to email", "respond to email", "answer email",
        "send email", "compose email", "write email",
        "mark as read", "mark as unread", "mark email",
        "search emails", "find emails", "look for emails"
    };

    public EmailMcp(ILogger<EmailMcp> logger) : base(logger) { }

        public override async Task<double> CanHandleAsync(string input, UserContext context)
    {
        if (_lastResponse.ContainsKey(context.UserId))
        {
            return 1.0; // High confidence for follow-up commands
        }
        
        var normalizedInput = input.ToLowerInvariant().Trim();
        
        // High confidence for follow-up commands when we have email context
        if (_lastEmailContext.ContainsKey(context.UserId))
        {
            var followUpKeywords = new[] { "reply", "respond", "delete", "spam", "junk", "next email", "read next" };
            if (followUpKeywords.Any(keyword => normalizedInput.Contains(keyword)))
            {
                return 0.95;
            }
        }
        
        // Only handle if input explicitly contains email-related keywords
        var emailKeywords = new[] { "email", "emails", "inbox", "message", "messages", "mail", "gmail", "outlook" };
        var actionKeywords = new[] { "read", "check", "show", "list", "send", "compose", "write", "delete", "remove", "reply", "respond", "search", "find" };
        
        bool hasEmailKeyword = emailKeywords.Any(keyword => normalizedInput.Contains(keyword));
        bool hasActionKeyword = actionKeywords.Any(keyword => normalizedInput.Contains(keyword));
        
        // High confidence only if both email context and action are present
        if (hasEmailKeyword && hasActionKeyword)
        {
            return 0.9;
        }
        
        // Medium confidence if just email keyword is present
        if (hasEmailKeyword)
        {
            return 0.7;
        }
        
        // Very low confidence for action words without email context
        if (hasActionKeyword)
        {
            return 0.1;
        }
        
        // No confidence for general queries
        return 0.0;
    }

        public override async Task<McpResponse> HandleCommandAsync(string input, UserContext context)
    {
        if (_lastResponse.TryGetValue(context.UserId, out var lastResponse))
        {
            // Clear the last response to avoid re-processing
            _lastResponse.Remove(context.UserId);

            if (lastResponse.Message.Contains("Would you like me to read the subjects?"))
             {
                 var normalizedInput = input.ToLowerInvariant().Trim();
                 if (normalizedInput.Contains("yes") || normalizedInput.Contains("read") || normalizedInput.Contains("subject"))
                 {
                     try
                     {
                         var responseData = lastResponse.Data as dynamic;
                         if (responseData != null)
                         {
                             var emails = responseData.Emails;
                             var subjectsList = new List<string>();
                             
                             foreach (var email in emails)
                             {
                                 subjectsList.Add($"• {email.Subject}");
                             }
                             
                             var subjectsText = string.Join("\n", subjectsList);
                             return Success($"Here are the subjects of the latest {subjectsList.Count} emails:\n\n{subjectsText}");
                         }
                     }
                     catch (Exception ex)
                     {
                         LogError(ex, "Error extracting email subjects from stored response");
                         return Error("Sorry, I had trouble accessing the email subjects. Please try asking for your emails again.");
                     }
                 }
                 else if (normalizedInput.Contains("no"))
                 {
                     return Success("Okay, I won't read the subjects. What would you like to do next?");
                 }
                 else
                 {
                     return Success("I'm not sure what you'd like me to do. Please say 'yes' to read the subjects or 'no' to skip.");
                 }
             }
        }
        try
        {
            var normalizedInput = input.ToLowerInvariant().Trim();
            
            // Check for follow-up commands when we have email context
            if (_lastEmailContext.ContainsKey(context.UserId))
            {
                if (normalizedInput.Contains("reply") || normalizedInput.Contains("respond"))
                {
                    return await HandleReplyToLastEmailAsync(normalizedInput, context);
                }
                if (normalizedInput.Contains("delete") && (normalizedInput.Contains("this") || normalizedInput.Contains("last") || normalizedInput.Contains("email")))
                {
                    return await HandleDeleteLastEmailAsync(normalizedInput, context);
                }
                if (normalizedInput.Contains("spam") || normalizedInput.Contains("junk"))
                {
                    return await HandleMoveToSpamAsync(normalizedInput, context);
                }
                if (normalizedInput.Contains("next email") || normalizedInput.Contains("read next"))
                {
                    return await HandleReadNextEmailAsync(normalizedInput, context);
                }
            }
            
            // Parse command intent
            if (IsReadCommand(normalizedInput))
            {
                // Check if user wants to read a specific email or just list emails
                if (normalizedInput.Contains("last email") || normalizedInput.Contains("read the last") || 
                    normalizedInput.Contains("read my last") || normalizedInput.Contains("read email content") || 
                    normalizedInput.Contains("full email") || normalizedInput.Contains("latest email"))
                {
                    return await HandleReadSpecificEmailAsync(normalizedInput, context);
                }
                return await HandleReadEmailsAsync(normalizedInput, context);
            }
            
            if (IsDeleteCommand(normalizedInput))
                return await HandleDeleteEmailAsync(normalizedInput, context);
            
            if (IsReplyCommand(normalizedInput))
                return await HandleReplyEmailAsync(normalizedInput, context);
            
            if (IsSendCommand(normalizedInput))
                return await HandleSendEmailAsync(normalizedInput, context);
            
            if (IsMarkCommand(normalizedInput))
                return await HandleMarkEmailAsync(normalizedInput, context);
            
                        if (IsSearchCommand(normalizedInput))
                return await HandleSearchEmailsAsync(normalizedInput, context);

            // If we are in a follow-up context, but the input doesn't match, we should let the user know.
            if (_lastResponse.ContainsKey(context.UserId))
            {
                _lastResponse.Remove(context.UserId);
                return Error("I'm not sure how to handle that response. Please try your command again.");
            }

            return Error("I understand you want to work with emails, but I'm not sure what specific action you'd like to take. Try 'read my emails' or 'send an email'.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error handling email command: {Input}", input);
            return Error("Sorry, I encountered an error while processing your email request. Please try again.");
        }
    }

    private async Task<McpResponse> HandleReadEmailsAsync(string input, UserContext context)
    {
        try
        {
            var count = ExtractNumberFromInput(input, 5); // Default to 5 emails
            var onlyUnread = input.Contains("unread");
            
            (int totalCount, int totalUnreadCount) = await GetTotalAndUnreadCountAsync(context);

            if (totalCount == 0)
            {
                return Success("Your inbox is empty.");
            }

            var emails = new List<dynamic>();
            if (context.Provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                var gmailResponse = await ReadGmailEmailsAsync(context, count, onlyUnread);
                if (gmailResponse.Data is not null) emails = ((dynamic)gmailResponse.Data).Emails;
            }
            else if (context.Provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                var outlookResponse = await ReadOutlookEmailsAsync(context, count, onlyUnread);
                if (outlookResponse.Data is not null) emails = ((dynamic)outlookResponse.Data).Emails;
            }

                        var responseMessage = $"You have {totalCount} emails in your inbox, with {totalUnreadCount} unread. I have loaded the latest {emails.Count} emails. Would you like me to read the subjects?";
            
            var response = Success(responseMessage, new { Emails = emails, TotalCount = totalCount, UnreadCount = totalUnreadCount });
            _lastResponse[context.UserId] = response;
            return response;
        }
        catch (Exception ex)
        {
            LogError(ex, "Error reading emails");
            return Error("Failed to read emails. Please check your authentication.");
        }
    }

    private async Task<(int totalCount, int totalUnreadCount)> GetTotalAndUnreadCountAsync(UserContext context)
    {
        if (context.Provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
        {
            var credential = GoogleCredential.FromAccessToken(context.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer() { HttpClientInitializer = credential });
            var profile = await service.Users.GetProfile("me").ExecuteAsync();
            var labels = await service.Users.Labels.Get("me", "INBOX").ExecuteAsync();
            return (labels.MessagesTotal ?? 0, labels.MessagesUnread ?? 0);
        }
        else if (context.Provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            var graphClient = GetGraphServiceClient(context.AccessToken);
            var inbox = await graphClient.Me.MailFolders["Inbox"].GetAsync();
            return (inbox.TotalItemCount ?? 0, inbox.UnreadItemCount ?? 0);
        }
        return (0, 0);
    }

    private async Task<McpResponse> HandleReadSpecificEmailAsync(string input, UserContext context)
    {
        try
        {
            McpResponse response;
            if (context.Provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                response = await ReadLatestGmailContentAsync(context);
            }
            else if (context.Provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                response = await ReadLatestOutlookContentAsync(context);
            }
            else
            {
                return Error("Email provider not supported.");
            }
            
            // Store the email context for follow-up commands
            if (response.IsSuccess && response.Data != null)
            {
                _lastEmailContext[context.UserId] = response.Data;
                
                // Enhance the response with follow-up options
                var enhancedMessage = response.Message + "\n\nWhat would you like to do next? You can:\n• Reply to this email\n• Delete this email\n• Move to spam\n• Read next email";
                return Success(enhancedMessage, response.Data);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            LogError(ex, "Error reading specific email");
            return Error("Failed to read email content. Please check your authentication.");
        }
    }

    private async Task<McpResponse> ReadLatestGmailContentAsync(UserContext context)
    {
        var credential = GoogleCredential.FromAccessToken(context.AccessToken);
        var service = new GmailService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential
        });

        var request = service.Users.Messages.List("me");
        request.MaxResults = 1;
        request.Q = "in:inbox";
        
        var response = await request.ExecuteAsync();
        
        if (response.Messages == null || !response.Messages.Any())
        {
            return Success("Your inbox is empty.");
        }

        var messageId = response.Messages.First().Id;
        var fullMessage = await service.Users.Messages.Get("me", messageId).ExecuteAsync();
        
        var subject = GetHeaderValue(fullMessage.Payload.Headers, "Subject") ?? "(No Subject)";
        var from = GetHeaderValue(fullMessage.Payload.Headers, "From") ?? "Unknown Sender";
        var date = GetHeaderValue(fullMessage.Payload.Headers, "Date") ?? "Unknown Date";
        
        var emailContent = ExtractEmailContent(fullMessage.Payload);
        var content = !string.IsNullOrEmpty(emailContent.text) ? emailContent.text : fullMessage.Snippet ?? "No content available";
        
        var emailDetails = $"**Subject:** {subject}\n**From:** {from}\n**Date:** {date}\n\n**Content:**\n{content}";
        
        var emailData = new
        {
            MessageId = messageId,
            Subject = subject,
            From = from,
            Date = date,
            Content = content,
            Provider = "Google"
        };
        
        return Success(emailDetails, emailData);
    }

    private async Task<McpResponse> ReadLatestOutlookContentAsync(UserContext context)
    {
        var graphClient = GetGraphServiceClient(context.AccessToken);

        var messages = await graphClient.Me.Messages
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Top = 1;
                requestConfiguration.QueryParameters.Orderby = new[] { "receivedDateTime desc" };
            });

        if (messages?.Value == null || !messages.Value.Any())
        {
            return Success("Your inbox is empty.");
        }

        var message = messages.Value.First();
        var subject = message.Subject ?? "(No Subject)";
        var from = message.From?.EmailAddress?.Address ?? "Unknown Sender";
        var date = message.ReceivedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown Date";
        var content = message.Body?.Content ?? message.BodyPreview ?? "No content available";
        
        var emailDetails = $"**Subject:** {subject}\n**From:** {from}\n**Date:** {date}\n\n**Content:**\n{content}";
        
        var emailData = new
        {
            MessageId = message.Id,
            Subject = subject,
            From = from,
            Date = date,
            Content = content,
            Provider = "Microsoft"
        };
        
        return Success(emailDetails, emailData);
    }

    private EmailContent ExtractEmailContent(MessagePart messagePart)
    {
        var textContent = "";
        var htmlContent = "";

        if (messagePart.Body != null && !string.IsNullOrEmpty(messagePart.Body.Data))
        {
            var content = Encoding.UTF8.GetString(Convert.FromBase64String(messagePart.Body.Data.Replace('-', '+').Replace('_', '/')));
            
            if (messagePart.MimeType == "text/plain")
            {
                textContent = content;
            }
            else if (messagePart.MimeType == "text/html")
            {
                htmlContent = content;
            }
        }

        if (messagePart.Parts != null)
        {
            foreach (var part in messagePart.Parts)
            {
                var partContent = ExtractEmailContent(part);
                if (!string.IsNullOrEmpty(partContent.text)) textContent += partContent.text;
                if (!string.IsNullOrEmpty(partContent.html)) htmlContent += partContent.html;
            }
        }

        return new EmailContent { text = textContent, html = htmlContent };
    }

    private class EmailContent
    {
        public string text { get; set; } = "";
        public string html { get; set; } = "";
    }

    private async Task<McpResponse> HandleReplyToLastEmailAsync(string input, UserContext context)
    {
        if (!_lastEmailContext.TryGetValue(context.UserId, out var emailData))
        {
            return Error("No email context found. Please read an email first.");
        }

        var emailInfo = (dynamic)emailData;
        return Success($"I'll help you reply to the email '{emailInfo.Subject}' from {emailInfo.From}. What would you like to say in your reply?");
    }

    private async Task<McpResponse> HandleDeleteLastEmailAsync(string input, UserContext context)
    {
        if (!_lastEmailContext.TryGetValue(context.UserId, out var emailData))
        {
            return Error("No email context found. Please read an email first.");
        }

        var emailInfo = (dynamic)emailData;
        return Success($"Are you sure you want to delete the email '{emailInfo.Subject}' from {emailInfo.From}? Reply 'yes' to confirm or 'no' to cancel.");
    }

    private async Task<McpResponse> HandleMoveToSpamAsync(string input, UserContext context)
    {
        if (!_lastEmailContext.TryGetValue(context.UserId, out var emailData))
        {
            return Error("No email context found. Please read an email first.");
        }

        var emailInfo = (dynamic)emailData;
        return Success($"Are you sure you want to move the email '{emailInfo.Subject}' from {emailInfo.From} to spam? Reply 'yes' to confirm or 'no' to cancel.");
    }

    private async Task<McpResponse> HandleReadNextEmailAsync(string input, UserContext context)
    {
        // Clear the current email context and read the next email
        _lastEmailContext.Remove(context.UserId);
        return await HandleReadSpecificEmailAsync("read next email", context);
    }

    private async Task<McpResponse> ReadGmailEmailsAsync(UserContext context, int count, bool onlyUnread)
    {
        var credential = GoogleCredential.FromAccessToken(context.AccessToken);
        var service = new GmailService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential
        });

        var request = service.Users.Messages.List("me");
        request.MaxResults = count;
        request.Q = onlyUnread ? "in:inbox is:unread" : "in:inbox";
        
        var response = await request.ExecuteAsync();
        
        if (response.Messages == null || !response.Messages.Any())
        {
            return Success(onlyUnread ? "You have no unread emails." : "Your inbox is empty.");
        }

        var emails = new List<object>();
        
        foreach (var message in response.Messages.Take(count))
        {
            var fullMessage = await service.Users.Messages.Get("me", message.Id).ExecuteAsync();
            
            var subject = GetHeaderValue(fullMessage.Payload.Headers, "Subject") ?? "(No Subject)";
            var from = GetHeaderValue(fullMessage.Payload.Headers, "From") ?? "Unknown Sender";
            var date = GetHeaderValue(fullMessage.Payload.Headers, "Date") ?? "Unknown Date";
            var snippet = fullMessage.Snippet ?? "";
            
            emails.Add(new
            {
                Id = message.Id,
                Subject = subject,
                From = from,
                Date = date,
                Snippet = snippet,
                IsUnread = fullMessage.LabelIds?.Contains("UNREAD") ?? false
            });
        }

        return Success("", new { Emails = emails });
    }

    private async Task<McpResponse> ReadOutlookEmailsAsync(UserContext context, int count, bool onlyUnread)
    {
        var graphClient = GetGraphServiceClient(context.AccessToken);

        var messages = await graphClient.Me.Messages
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Top = count;
                if (onlyUnread)
                {
                    requestConfiguration.QueryParameters.Filter = "isRead eq false";
                }
                requestConfiguration.QueryParameters.Orderby = new[] { "receivedDateTime desc" };
            });

        if (messages?.Value == null || !messages.Value.Any())
        {
            return Success(onlyUnread ? "You have no unread emails." : "Your inbox is empty.");
        }

        var emails = messages.Value.Select(msg => new
        {
            Id = msg.Id,
            Subject = msg.Subject ?? "(No Subject)",
            From = msg.From?.EmailAddress?.Address ?? "Unknown Sender",
            Date = msg.ReceivedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown Date",
            Snippet = msg.BodyPreview ?? "",
            IsUnread = !(msg.IsRead ?? true)
        }).ToList();

        return Success("", new { Emails = emails });
    }

    private GraphServiceClient GetGraphServiceClient(string accessToken)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return new GraphServiceClient(httpClient);
    }

    private async Task<McpResponse> HandleDeleteEmailAsync(string input, UserContext context)
    {
        try
        {
            // Extract email ID from input (this is a simplified implementation)
            var emailIdMatch = Regex.Match(input, @"\b[A-Za-z0-9_-]+\b");
            if (!emailIdMatch.Success)
            {
                return Error("Please specify which email to delete by providing the email ID or subject.");
            }

            if (context.Provider == "Google")
            {
                return await DeleteGmailEmailAsync(emailIdMatch.Value, context);
            }
            else if (context.Provider == "Microsoft")
            {
                return await DeleteOutlookEmailAsync(emailIdMatch.Value, context);
            }
            else
            {
                return Error("Email provider not supported for deletion.");
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error deleting email");
            return Error($"Failed to delete email: {ex.Message}");
        }
    }

    private async Task<McpResponse> HandleReplyEmailAsync(string input, UserContext context)
    {
        try
        {
            // Extract email ID and reply content from input
            var parts = input.Split(new[] { "reply to", "respond to" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return Error("Please specify the email to reply to and your message. Example: 'reply to email123 with thank you for your message'");
            }

            var emailId = parts[0].Trim();
            var replyContent = parts[1].Replace("with", "").Trim();

            if (context.Provider == "Google")
            {
                return await ReplyToGmailAsync(emailId, replyContent, context);
            }
            else if (context.Provider == "Microsoft")
            {
                return await ReplyToOutlookAsync(emailId, replyContent, context);
            }
            else
            {
                return Error("Email provider not supported for replies.");
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error replying to email");
            return Error($"Failed to reply to email: {ex.Message}");
        }
    }

    private async Task<McpResponse> HandleSendEmailAsync(string input, UserContext context)
    {
        try
        {
            // Parse send email command: "send email to user@example.com with subject 'Hello' and message 'How are you?'"
            var toMatch = Regex.Match(input, @"to\s+([\w\.-]+@[\w\.-]+\.[\w]+)");
            var subjectMatch = Regex.Match(input, @"subject\s+['""']([^'""']+)['""']|subject\s+([^\s]+(?:\s+[^\s]+)*?)(?:\s+and\s+message|$)");
            var messageMatch = Regex.Match(input, @"message\s+['""']([^'""']+)['""']|message\s+(.+)$");

            if (!toMatch.Success)
            {
                return Error("Please specify the recipient email address. Example: 'send email to user@example.com'");
            }

            var to = toMatch.Groups[1].Value;
            var subject = subjectMatch.Success ? (subjectMatch.Groups[1].Value ?? subjectMatch.Groups[2].Value) : "No Subject";
            var message = messageMatch.Success ? (messageMatch.Groups[1].Value ?? messageMatch.Groups[2].Value) : "";

            if (string.IsNullOrEmpty(message))
            {
                return Error("Please specify the message content. Example: 'send email to user@example.com with subject Hello and message How are you?'");
            }

            if (context.Provider == "Google")
            {
                return await SendGmailAsync(to, subject, message, context);
            }
            else if (context.Provider == "Microsoft")
            {
                return await SendOutlookAsync(to, subject, message, context);
            }
            else
            {
                return Error("Email provider not supported for sending.");
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error sending email");
            return Error($"Failed to send email: {ex.Message}");
        }
    }

    private async Task<McpResponse> HandleMarkEmailAsync(string input, UserContext context)
    {
        try
        {
            var emailIdMatch = Regex.Match(input, @"\b[A-Za-z0-9_-]+\b");
            var isMarkAsRead = input.Contains("read") && !input.Contains("unread");
            
            if (!emailIdMatch.Success)
            {
                return Error("Please specify which email to mark by providing the email ID.");
            }

            if (context.Provider == "Google")
            {
                return await MarkGmailEmailAsync(emailIdMatch.Value, isMarkAsRead, context);
            }
            else if (context.Provider == "Microsoft")
            {
                return await MarkOutlookEmailAsync(emailIdMatch.Value, isMarkAsRead, context);
            }
            else
            {
                return Error("Email provider not supported for marking.");
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error marking email");
            return Error($"Failed to mark email: {ex.Message}");
        }
    }

    private async Task<McpResponse> HandleSearchEmailsAsync(string input, UserContext context)
    {
        try
        {
            // Extract search query from input
            var searchQuery = input.Replace("search", "").Replace("find", "").Replace("look for", "").Replace("emails", "").Trim();
            
            if (string.IsNullOrEmpty(searchQuery))
            {
                return Error("Please specify what to search for. Example: 'search emails for meeting'");
            }

            if (context.Provider == "Google")
            {
                return await SearchGmailAsync(searchQuery, context);
            }
            else if (context.Provider == "Microsoft")
            {
                return await SearchOutlookAsync(searchQuery, context);
            }
            else
            {
                return Error("Email provider not supported for search.");
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error searching emails");
            return Error($"Failed to search emails: {ex.Message}");
        }
    }

    private static string? GetHeaderValue(IList<MessagePartHeader> headers, string name)
    {
        return headers?.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static int ExtractNumberFromInput(string input, int defaultValue)
    {
        var match = Regex.Match(input, @"\b(\d+)\b");
        return match.Success && int.TryParse(match.Value, out var number) ? number : defaultValue;
    }

    private static bool IsReadCommand(string input) => 
        input.Contains("read") || input.Contains("check") || input.Contains("show") || input.Contains("list");
    
    private static bool IsDeleteCommand(string input) => 
        input.Contains("delete") || input.Contains("remove") || input.Contains("trash");
    
    private static bool IsReplyCommand(string input) => 
        input.Contains("reply") || input.Contains("respond") || input.Contains("answer");
    
    private static bool IsSendCommand(string input) => 
        input.Contains("send") || input.Contains("compose") || input.Contains("write");
    
    private static bool IsMarkCommand(string input) => 
        input.Contains("mark");
    
    private static bool IsSearchCommand(string input) => 
        input.Contains("search") || input.Contains("find") || input.Contains("look for");

    public override async IAsyncEnumerable<McpUpdate> GetUpdatesAsync(UserContext context)
    {
        var lastEmailIds = new HashSet<string>();
        
        while (true)
        {
            List<McpUpdate> updates = new();
            
            try
            {
                // Check for new emails every 30 seconds
                await Task.Delay(TimeSpan.FromSeconds(30));
                
                // Get recent emails
                var response = await HandleReadEmailsAsync("read my emails", context);
                
                if (response.Success && response.Data != null)
                {
                    var emailData = response.Data as dynamic;
                    if (emailData?.Emails != null)
                    {
                        var emails = emailData.Emails as IEnumerable<dynamic>;
                        var currentEmailIds = emails?.Select(e => (string)e.Id).ToHashSet() ?? new HashSet<string>();
                        
                        // Find new emails
                        var newEmailIds = currentEmailIds.Except(lastEmailIds);
                        
                        foreach (var emailId in newEmailIds)
                        {
                            var email = emails?.FirstOrDefault(e => (string)e.Id == emailId);
                            if (email != null)
                            {
                                updates.Add(new McpUpdate
                                {
                                    ModuleId = Id,
                                    Type = "NewEmail",
                                    Title = "New Email",
                                    Message = $"New email from {email.From}: {email.Subject}",
                                    Data = new
                                    {
                                        EmailId = email.Id,
                                        Subject = email.Subject,
                                        From = email.From,
                                        Date = email.Date,
                                        Snippet = email.Snippet,
                                        IsUnread = email.IsUnread
                                    },
                                    Timestamp = DateTime.UtcNow,
                                    Priority = "High",
                                    Metadata = new Dictionary<string, object>
                                    {
                                        ["requiresVoiceResponse"] = true,
                                        ["voiceMessage"] = "New Email",
                                        ["followUpActions"] = new[] { "read", "delete", "reply" }
                                    }
                                });
                            }
                        }
                        
                        lastEmailIds = currentEmailIds;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "Error checking for email updates");
                // Wait longer on error
                await Task.Delay(TimeSpan.FromMinutes(2));
            }
            
            // Yield all updates outside the try-catch block
            foreach (var update in updates)
            {
                yield return update;
            }
        }
    }

    // Gmail-specific operations
    private async Task<McpResponse> DeleteGmailEmailAsync(string emailId, UserContext context)
    {
        try
        {
            var credential = GoogleCredential.FromAccessToken(context.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential
            });

            await service.Users.Messages.Trash("me", emailId).ExecuteAsync();
            return Success($"Email {emailId} moved to trash successfully.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error deleting Gmail email");
            return Error($"Failed to delete email: {ex.Message}");
        }
    }

    private async Task<McpResponse> ReplyToGmailAsync(string emailId, string replyContent, UserContext context)
    {
        try
        {
            var credential = GoogleCredential.FromAccessToken(context.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential
            });

            // Get the original message to extract reply information
            var originalMessage = await service.Users.Messages.Get("me", emailId).ExecuteAsync();
            var headers = originalMessage.Payload.Headers;
            
            var fromHeader = GetHeaderValue(headers, "From");
            var subjectHeader = GetHeaderValue(headers, "Subject");
            var messageIdHeader = GetHeaderValue(headers, "Message-ID");
            
            var replySubject = subjectHeader?.StartsWith("Re:") == true ? subjectHeader : $"Re: {subjectHeader}";
            
            var message = new Google.Apis.Gmail.v1.Data.Message
            {
                Payload = new MessagePart
                {
                    Headers = new List<MessagePartHeader>
                    {
                        new() { Name = "To", Value = fromHeader },
                        new() { Name = "Subject", Value = replySubject },
                        new() { Name = "In-Reply-To", Value = messageIdHeader }
                    },
                    Body = new MessagePartBody
                    {
                        Data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(replyContent))
                            .Replace('+', '-').Replace('/', '_').Replace("=", "")
                    }
                }
            };

            await service.Users.Messages.Send(message, "me").ExecuteAsync();
            return Success($"Reply sent successfully to {fromHeader}.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error replying to Gmail email");
            return Error($"Failed to reply to email: {ex.Message}");
        }
    }

    private async Task<McpResponse> SendGmailAsync(string to, string subject, string messageContent, UserContext context)
    {
        try
        {
            var credential = GoogleCredential.FromAccessToken(context.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential
            });

            var message = new Google.Apis.Gmail.v1.Data.Message
            {
                Payload = new MessagePart
                {
                    Headers = new List<MessagePartHeader>
                    {
                        new() { Name = "To", Value = to },
                        new() { Name = "Subject", Value = subject }
                    },
                    Body = new MessagePartBody
                    {
                        Data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(messageContent))
                            .Replace('+', '-').Replace('/', '_').Replace("=", "")
                    }
                }
            };

            await service.Users.Messages.Send(message, "me").ExecuteAsync();
            return Success($"Email sent successfully to {to}.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error sending Gmail email");
            return Error($"Failed to send email: {ex.Message}");
        }
    }

    private async Task<McpResponse> MarkGmailEmailAsync(string emailId, bool markAsRead, UserContext context)
    {
        try
        {
            var credential = GoogleCredential.FromAccessToken(context.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential
            });

            var modifyRequest = new ModifyMessageRequest
            {
                AddLabelIds = markAsRead ? null : new List<string> { "UNREAD" },
                RemoveLabelIds = markAsRead ? new List<string> { "UNREAD" } : null
            };

            await service.Users.Messages.Modify(modifyRequest, "me", emailId).ExecuteAsync();
            var status = markAsRead ? "read" : "unread";
            return Success($"Email {emailId} marked as {status}.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error marking Gmail email");
            return Error($"Failed to mark email: {ex.Message}");
        }
    }

    private async Task<McpResponse> SearchGmailAsync(string query, UserContext context)
    {
        try
        {
            var credential = GoogleCredential.FromAccessToken(context.AccessToken);
            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential
            });

            var request = service.Users.Messages.List("me");
            request.Q = query;
            request.MaxResults = 10;
            
            var response = await request.ExecuteAsync();
            
            if (response.Messages == null || !response.Messages.Any())
            {
                return Success($"No emails found matching '{query}'.");
            }

            var emails = new List<object>();
            foreach (var message in response.Messages.Take(5)) // Limit to 5 for performance
            {
                var fullMessage = await service.Users.Messages.Get("me", message.Id).ExecuteAsync();
                var headers = fullMessage.Payload.Headers;
                
                emails.Add(new
                {
                    Id = message.Id,
                    Subject = GetHeaderValue(headers, "Subject") ?? "(No Subject)",
                    From = GetHeaderValue(headers, "From") ?? "Unknown Sender",
                    Date = GetHeaderValue(headers, "Date") ?? "Unknown Date",
                    Snippet = fullMessage.Snippet ?? ""
                });
            }

            return Success($"Found {response.Messages.Count} emails matching '{query}'. Showing first {emails.Count}:", new { Emails = emails, Query = query, TotalCount = response.Messages.Count });
        }
        catch (Exception ex)
        {
            LogError(ex, "Error searching Gmail emails");
            return Error($"Failed to search emails: {ex.Message}");
        }
    }

    // Outlook-specific operations
    private async Task<McpResponse> DeleteOutlookEmailAsync(string emailId, UserContext context)
    {
        try
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
            var graphClient = new GraphServiceClient(httpClient);

            await graphClient.Me.Messages[emailId].DeleteAsync();
            return Success($"Email {emailId} deleted successfully.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error deleting Outlook email");
            return Error($"Failed to delete email: {ex.Message}");
        }
    }

    private async Task<McpResponse> ReplyToOutlookAsync(string emailId, string replyContent, UserContext context)
    {
        try
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
            var graphClient = new GraphServiceClient(httpClient);

            var replyMessage = new Microsoft.Graph.Models.Message
            {
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = replyContent
                }
            };

            await graphClient.Me.Messages[emailId].Reply.PostAsync(new ReplyPostRequestBody
            {
                Message = replyMessage
            });
            
            return Success($"Reply sent successfully.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error replying to Outlook email");
            return Error($"Failed to reply to email: {ex.Message}");
        }
    }

    private async Task<McpResponse> SendOutlookAsync(string to, string subject, string messageContent, UserContext context)
    {
        try
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
            var graphClient = new GraphServiceClient(httpClient);

            var message = new Microsoft.Graph.Models.Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = messageContent
                },
                ToRecipients = new List<Recipient>
                {
                    new()
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = to
                        }
                    }
                }
            };

            await graphClient.Me.SendMail.PostAsync(new SendMailPostRequestBody
            {
                Message = message
            });
            
            return Success($"Email sent successfully to {to}.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error sending Outlook email");
            return Error($"Failed to send email: {ex.Message}");
        }
    }

    private async Task<McpResponse> MarkOutlookEmailAsync(string emailId, bool markAsRead, UserContext context)
    {
        try
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
            var graphClient = new GraphServiceClient(httpClient);

            var message = new Microsoft.Graph.Models.Message
            {
                IsRead = markAsRead
            };

            await graphClient.Me.Messages[emailId].PatchAsync(message);
            var status = markAsRead ? "read" : "unread";
            return Success($"Email {emailId} marked as {status}.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error marking Outlook email");
            return Error($"Failed to mark email: {ex.Message}");
        }
    }

    private async Task<McpResponse> SearchOutlookAsync(string query, UserContext context)
    {
        try
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
            var graphClient = new GraphServiceClient(httpClient);

            var messages = await graphClient.Me.Messages
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Search = $"\"{query}\"";
                    requestConfiguration.QueryParameters.Top = 10;
                    requestConfiguration.QueryParameters.Orderby = new[] { "receivedDateTime desc" };
                });

            if (messages?.Value == null || !messages.Value.Any())
            {
                return Success($"No emails found matching '{query}'.");
            }

            var emails = messages.Value.Take(5).Select(msg => new
            {
                Id = msg.Id,
                Subject = msg.Subject ?? "(No Subject)",
                From = msg.From?.EmailAddress?.Address ?? "Unknown Sender",
                Date = msg.ReceivedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown Date",
                Snippet = msg.BodyPreview ?? ""
            }).ToList();

            return Success($"Found {messages.Value.Count} emails matching '{query}'. Showing first {emails.Count}:", new { Emails = emails, Query = query, TotalCount = messages.Value.Count });
        }
        catch (Exception ex)
        {
            LogError(ex, "Error searching Outlook emails");
            return Error($"Failed to search emails: {ex.Message}");
        }
    }
}