using AIMcpAssistant.Core.Models;
using AIMcpAssistant.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

using Microsoft.Graph.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using System.Text.RegularExpressions;

namespace AIMcpAssistant.MCPs;

public class CalendarMcp : BaseMcpModule
{
    public override string Id => "calendar";
    public override string Name => "Calendar Manager";
    public override string Description => "Manage calendar events from Google Calendar and Microsoft Outlook";
    public override int Priority => 9;

    public override List<string> SupportedCommands => new()
    {
        "show calendar", "check calendar", "view calendar", "list events",
        "what's on my calendar", "my schedule", "today's events", "tomorrow's events",
        "create event", "schedule meeting", "add appointment", "book time",
        "cancel event", "delete event", "remove appointment",
        "update event", "modify event", "change meeting",
        "free time", "available time", "when am I free",
        "next meeting", "upcoming events", "what's next"
    };

    public CalendarMcp(ILogger<CalendarMcp> logger) : base(logger) { }

    public override async Task<double> CanHandleAsync(string input, UserContext context)
    {
        var normalizedInput = input.ToLowerInvariant().Trim();
        
        // Only handle if input explicitly contains calendar-related keywords
        var calendarKeywords = new[] { "calendar", "schedule", "meeting", "event", "events", "appointment", "appointments" };
        var actionKeywords = new[] { "show", "check", "view", "list", "create", "add", "book", "cancel", "delete", "remove", "update", "modify", "change" };
        var timeKeywords = new[] { "today", "tomorrow", "next", "upcoming", "free", "available", "when" };
        
        bool hasCalendarKeyword = calendarKeywords.Any(keyword => normalizedInput.Contains(keyword));
        bool hasActionKeyword = actionKeywords.Any(keyword => normalizedInput.Contains(keyword));
        bool hasTimeKeyword = timeKeywords.Any(keyword => normalizedInput.Contains(keyword));
        
        // High confidence only if calendar context and action are present
        if (hasCalendarKeyword && hasActionKeyword)
        {
            return 0.9;
        }
        
        // Medium confidence if just calendar keyword is present
        if (hasCalendarKeyword)
        {
            return 0.7;
        }
        
        // Low confidence for time-related queries that might be calendar-related
        if (hasTimeKeyword && hasActionKeyword)
        {
            return 0.3;
        }
        
        // Very low confidence for action words without calendar context
        if (hasActionKeyword)
        {
            return 0.1;
        }
        
        // No confidence for general queries
        return 0.0;
    }

    public override async Task<McpResponse> HandleCommandAsync(string input, UserContext context)
    {
        try
        {
            var normalizedInput = input.ToLowerInvariant().Trim();
            
            if (IsViewCommand(normalizedInput))
                return await HandleViewCalendarAsync(normalizedInput, context);
            
            if (IsCreateCommand(normalizedInput))
                return await HandleCreateEventAsync(normalizedInput, context);
            
            if (IsDeleteCommand(normalizedInput))
                return await HandleDeleteEventAsync(normalizedInput, context);
            
            if (IsUpdateCommand(normalizedInput))
                return await HandleUpdateEventAsync(normalizedInput, context);
            
            if (IsFreeTimeCommand(normalizedInput))
                return await HandleFreeTimeAsync(normalizedInput, context);
            
            if (IsNextEventCommand(normalizedInput))
                return await HandleNextEventAsync(normalizedInput, context);

            return Error("I understand you want to work with your calendar, but I'm not sure what specific action you'd like to take. Try 'show my calendar' or 'what's my next meeting'.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error handling calendar command: {Input}", input);
            return Error("Sorry, I encountered an error while processing your calendar request. Please try again.");
        }
    }

    private async Task<McpResponse> HandleViewCalendarAsync(string input, UserContext context)
    {
        try
        {
            var timeRange = ExtractTimeRange(input);
            
            if (context.Provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                return await ViewGoogleCalendarAsync(context, timeRange.start, timeRange.end);
            }
            else if (context.Provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                return await ViewOutlookCalendarAsync(context, timeRange.start, timeRange.end);
            }
            
            return Error("Unsupported calendar provider. Please use Google or Microsoft account.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error viewing calendar");
            return Error("Failed to view calendar. Please check your authentication.");
        }
    }

    private async Task<McpResponse> ViewGoogleCalendarAsync(UserContext context, DateTime start, DateTime end)
    {
        var credential = GoogleCredential.FromAccessToken(context.AccessToken);
        var service = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential
        });

        var request = service.Events.List("primary");
        request.TimeMin = start;
        request.TimeMax = end;
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var events = await request.ExecuteAsync();
        
        if (events.Items == null || !events.Items.Any())
        {
            return Success($"No events found between {start:MMM dd} and {end:MMM dd}.");
        }

        var eventList = events.Items.Select(evt => new
        {
            Id = evt.Id,
            Summary = evt.Summary ?? "(No Title)",
            Start = evt.Start?.DateTime?.ToString("MMM dd, yyyy HH:mm") ?? 
                   evt.Start?.Date ?? "All Day",
            End = evt.End?.DateTime?.ToString("MMM dd, yyyy HH:mm") ?? 
                 evt.End?.Date ?? "All Day",
            Location = evt.Location ?? "",
            Description = evt.Description ?? "",
            Attendees = evt.Attendees?.Select(a => a.Email).ToList() ?? new List<string>()
        }).ToList();

        var message = $"You have {eventList.Count} event(s) between {start:MMM dd} and {end:MMM dd}.";
        return Success(message, new { Events = eventList, Count = eventList.Count });
    }

    private async Task<McpResponse> ViewOutlookCalendarAsync(UserContext context, DateTime start, DateTime end)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
        var graphClient = new GraphServiceClient(httpClient);

        var events = await graphClient.Me.Calendar.Events
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Filter = $"start/dateTime ge '{start:yyyy-MM-ddTHH:mm:ss.fffK}' and end/dateTime le '{end:yyyy-MM-ddTHH:mm:ss.fffK}'";
                requestConfiguration.QueryParameters.Orderby = new[] { "start/dateTime" };
            });

        if (events?.Value == null || !events.Value.Any())
        {
            return Success($"No events found between {start:MMM dd} and {end:MMM dd}.");
        }

        var eventList = events.Value.Select(evt => new
        {
            Id = evt.Id,
            Summary = evt.Subject ?? "(No Title)",
            Start = evt.Start?.DateTime != null && DateTime.TryParse(evt.Start.DateTime, out var startTime) ? startTime.ToString("MMM dd, yyyy HH:mm") : "All Day",
            End = evt.End?.DateTime != null && DateTime.TryParse(evt.End.DateTime, out var endTime) ? endTime.ToString("MMM dd, yyyy HH:mm") : "All Day",
            Location = evt.Location?.DisplayName ?? "",
            Description = evt.Body?.Content ?? "",
            Attendees = evt.Attendees?.Select(a => a.EmailAddress?.Address).Where(e => !string.IsNullOrEmpty(e)).ToList() ?? new List<string>()
        }).ToList();

        var message = $"You have {eventList.Count} event(s) between {start:MMM dd} and {end:MMM dd}.";
        return Success(message, new { Events = eventList, Count = eventList.Count });
    }

    private async Task<McpResponse> HandleCreateEventAsync(string input, UserContext context)
    {
        return Error("Event creation feature is not yet implemented. Please use your calendar app to create events.");
    }

    private async Task<McpResponse> HandleDeleteEventAsync(string input, UserContext context)
    {
        return Error("Event deletion feature is not yet implemented for safety reasons.");
    }

    private async Task<McpResponse> HandleUpdateEventAsync(string input, UserContext context)
    {
        return Error("Event update feature is not yet implemented.");
    }

    private async Task<McpResponse> HandleFreeTimeAsync(string input, UserContext context)
    {
        return Error("Free time lookup feature is not yet implemented.");
    }

    private async Task<McpResponse> HandleNextEventAsync(string input, UserContext context)
    {
        try
        {
            var now = DateTime.UtcNow;
            var endOfDay = now.Date.AddDays(1);
            
            if (context.Provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                return await GetNextGoogleEventAsync(context, now, endOfDay);
            }
            else if (context.Provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                return await GetNextOutlookEventAsync(context, now, endOfDay);
            }
            
            return Error("Unsupported calendar provider.");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting next event");
            return Error("Failed to get next event.");
        }
    }

    private async Task<McpResponse> GetNextGoogleEventAsync(UserContext context, DateTime start, DateTime end)
    {
        var credential = GoogleCredential.FromAccessToken(context.AccessToken);
        var service = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential
        });

        var request = service.Events.List("primary");
        request.TimeMin = start;
        request.TimeMax = end;
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        request.MaxResults = 1;

        var events = await request.ExecuteAsync();
        
        if (events.Items == null || !events.Items.Any())
        {
            return Success("No upcoming events today.");
        }

        var nextEvent = events.Items.First();
        var eventTime = nextEvent.Start?.DateTime?.ToString("HH:mm") ?? "All Day";
        var message = $"Your next event is '{nextEvent.Summary}' at {eventTime}";
        
        if (!string.IsNullOrEmpty(nextEvent.Location))
        {
            message += $" at {nextEvent.Location}";
        }
        
        return Success(message, new { Event = nextEvent });
    }

    private async Task<McpResponse> GetNextOutlookEventAsync(UserContext context, DateTime start, DateTime end)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
        var graphClient = new GraphServiceClient(httpClient);

        var events = await graphClient.Me.Calendar.Events
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Filter = $"start/dateTime ge '{start:yyyy-MM-ddTHH:mm:ss.fffK}' and end/dateTime le '{end:yyyy-MM-ddTHH:mm:ss.fffK}'";
                requestConfiguration.QueryParameters.Orderby = new[] { "start/dateTime" };
                requestConfiguration.QueryParameters.Top = 1;
            });

        if (events?.Value == null || !events.Value.Any())
        {
            return Success("No upcoming events today.");
        }

        var nextEvent = events.Value.First();
        var eventTime = nextEvent.Start?.DateTime != null && DateTime.TryParse(nextEvent.Start.DateTime, out var startTime) ? startTime.ToString("HH:mm") : "All Day";
        var message = $"Your next event is '{nextEvent.Subject}' at {eventTime}";
        
        if (!string.IsNullOrEmpty(nextEvent.Location?.DisplayName))
        {
            message += $" at {nextEvent.Location.DisplayName}";
        }
        
        return Success(message, new { Event = nextEvent });
    }

    private static (DateTime start, DateTime end) ExtractTimeRange(string input)
    {
        var now = DateTime.Now;
        
        if (input.Contains("today"))
            return (now.Date, now.Date.AddDays(1));
        
        if (input.Contains("tomorrow"))
            return (now.Date.AddDays(1), now.Date.AddDays(2));
        
        if (input.Contains("week"))
            return (now.Date, now.Date.AddDays(7));
        
        if (input.Contains("month"))
            return (now.Date, now.Date.AddMonths(1));
        
        // Default to today
        return (now.Date, now.Date.AddDays(1));
    }

    private static bool IsViewCommand(string input) => 
        input.Contains("show") || input.Contains("check") || input.Contains("view") || 
        input.Contains("list") || input.Contains("calendar") || input.Contains("schedule") ||
        input.Contains("events");
    
    private static bool IsCreateCommand(string input) => 
        input.Contains("create") || input.Contains("schedule") || input.Contains("add") || 
        input.Contains("book");
    
    private static bool IsDeleteCommand(string input) => 
        input.Contains("cancel") || input.Contains("delete") || input.Contains("remove");
    
    private static bool IsUpdateCommand(string input) => 
        input.Contains("update") || input.Contains("modify") || input.Contains("change");
    
    private static bool IsFreeTimeCommand(string input) => 
        input.Contains("free") || input.Contains("available");
    
    private static bool IsNextEventCommand(string input) => 
        input.Contains("next") || input.Contains("upcoming");

    public override async IAsyncEnumerable<McpUpdate> GetUpdatesAsync(UserContext context)
    {
        // Implementation for real-time calendar updates
        await Task.CompletedTask;
        yield break;
    }
}