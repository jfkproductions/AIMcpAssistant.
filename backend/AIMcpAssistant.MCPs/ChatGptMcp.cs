using AIMcpAssistant.Core.Models;
using AIMcpAssistant.Core.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using AIMcpAssistant.Core.Interfaces;

namespace AIMcpAssistant.MCPs;

public class ChatGptMcp : BaseMcpModule
{
    private readonly HttpClient _httpClient;
    private readonly ICommandDispatcher _commandDispatcher;
    private string? _apiKey;

    public override string Id => "chatgpt";
    public override string Name => "General AI Assistant";
    public override string Description => "Intelligent orchestrator that analyzes intent and routes to appropriate modules or handles general queries";
    public override int Priority => 10; // High priority to act as orchestrator

    public override List<string> SupportedCommands => new()
    {
        "*" // Catch-all pattern for any command not handled by other modules
    };

    public ChatGptMcp(ILogger<ChatGptMcp> logger, HttpClient httpClient, ICommandDispatcher commandDispatcher) : base(logger)
    {
        _httpClient = httpClient;
        _commandDispatcher = commandDispatcher;
    }

    protected override async Task OnInitializeAsync()
    {
        // Get API key from configuration
        Configuration.TryGetValue("ApiKey", out var apiKey);
        _apiKey = apiKey?.ToString();
        
        if (string.IsNullOrEmpty(_apiKey))
        {
            Logger.LogWarning("OpenAI API key not configured. ChatGPT features will be disabled.");
        }
        
        await base.OnInitializeAsync();
    }

    public override async Task<double> CanHandleAsync(string input, UserContext context)
    {
        // As an orchestrator, this module can handle any input
        if (string.IsNullOrEmpty(_apiKey))
            return 0.0;
            
        // High confidence as orchestrator for non-calendar/email specific queries
        var normalizedInput = input.ToLowerInvariant().Trim();
        
        // Let specific modules handle their domains directly if they have high confidence
        var calendarKeywords = new[] { "calendar", "meeting", "appointment", "schedule", "event" };
        var emailKeywords = new[] { "email", "mail", "send", "reply", "inbox", "message" };
        
        bool isCalendarSpecific = calendarKeywords.Any(keyword => normalizedInput.Contains(keyword));
        bool isEmailSpecific = emailKeywords.Any(keyword => normalizedInput.Contains(keyword));
        
        if (isCalendarSpecific || isEmailSpecific)
            return 0.3; // Lower confidence for specific domains
            
        return 0.9; // High confidence for general queries
    }

    public override async Task<McpResponse> HandleCommandAsync(string input, UserContext context)
    {
        try
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return Error("ChatGPT service is not configured. Please contact your administrator.");
            }

            // Step 1: Analyze intent using ChatGPT
            var intentAnalysis = await AnalyzeIntentAsync(input);
            
            // Step 2: Route based on intent analysis
            if (intentAnalysis.ShouldRouteToSpecificModule)
            {
                var routingResult = await RouteToSpecificModuleAsync(input, context, intentAnalysis);
                if (routingResult != null)
                    return routingResult;
            }
            
            // Step 3: Handle as general query
            var response = await CallOpenAiAsync(input, context);
            return Success(response);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error handling command: {Input}", input);
            return Error("Sorry, I couldn't process your request right now. Please try again later.");
        }
    }

    private async Task<IntentAnalysis> AnalyzeIntentAsync(string input)
    {
        try
        {
            var systemPrompt = @"You are an intent analyzer. Analyze the user's input and determine:
1. If it's related to calendar/scheduling (meetings, appointments, events)
2. If it's related to email (sending, reading, replying to emails)
3. Confidence level (0.0 to 1.0)
4. The specific module that should handle it

Respond ONLY with a JSON object in this exact format:{
  ""shouldRouteToSpecificModule"": true/false,
  ""targetModule"": ""calendar"" or ""email"" or ""general"",
  ""confidence"": 0.0-1.0,
  ""reasoning"": ""brief explanation""
}";

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = input }
                },
                max_tokens = 200,
                temperature = 0.1
            };

            var response = await CallOpenAiApiAsync(requestBody);
            var intentAnalysis = JsonConvert.DeserializeObject<IntentAnalysis>(response);
            
            return intentAnalysis ?? new IntentAnalysis
            {
                ShouldRouteToSpecificModule = false,
                TargetModule = "general",
                Confidence = 0.1,
                Reasoning = "Failed to parse intent analysis"
            };
        }
        catch (Exception ex)
        {
            LogError(ex, "Error analyzing intent for input: {Input}", input);
            return new IntentAnalysis
            {
                ShouldRouteToSpecificModule = false,
                TargetModule = "general",
                Confidence = 0.1,
                Reasoning = "Error during intent analysis"
            };
        }
    }

    private async Task<McpResponse?> RouteToSpecificModuleAsync(string input, UserContext context, IntentAnalysis intentAnalysis)
    {
        try
        {
            if (intentAnalysis.Confidence < 0.7)
            {
                Logger.LogInformation("Intent confidence too low ({Confidence}) for routing to {Module}", 
                    intentAnalysis.Confidence, intentAnalysis.TargetModule);
                return null;
            }

            var modules = await _commandDispatcher.GetRegisteredModulesAsync();
            var targetModule = modules.FirstOrDefault(m => 
                m.Id.Equals(intentAnalysis.TargetModule, StringComparison.OrdinalIgnoreCase) ||
                (intentAnalysis.TargetModule == "calendar" && m.Id.Contains("calendar", StringComparison.OrdinalIgnoreCase)) ||
                (intentAnalysis.TargetModule == "email" && m.Id.Contains("email", StringComparison.OrdinalIgnoreCase)));

            if (targetModule != null)
            {
                Logger.LogInformation("Routing to {Module} with confidence {Confidence}: {Reasoning}", 
                    targetModule.Name, intentAnalysis.Confidence, intentAnalysis.Reasoning);
                
                var canHandle = await targetModule.CanHandleAsync(input, context);
                if (canHandle > 0.5)
                {
                    return await targetModule.HandleCommandAsync(input, context);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            LogError(ex, "Error routing to specific module: {Module}", intentAnalysis.TargetModule);
            return null;
        }
    }

    private async Task<string> CallOpenAiAsync(string input, UserContext context)
    {
        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = @"You are a helpful personal AI assistant. Your role is to:
1. Act as a friendly, conversational assistant for general queries and greetings
2. Provide concise, accurate, and helpful responses
3. For greetings like 'hello', 'hi', respond warmly and ask how you can help
4. Keep responses short and conversational unless detailed information is requested
5. If asked about weather, current events, or real-time information, explain that you may not have the most current data
6. When users mention email tasks (create email, send email, read email, reply), suggest they can use email commands
7. When users mention calendar tasks (schedule meeting, check calendar, create appointment), suggest they can use calendar commands
8. Be proactive in suggesting available features when appropriate"
                },
                new
                {
                    role = "user",
                    content = input
                }
            },
            max_tokens = 500,
            temperature = 0.7
        };

        return await CallOpenAiApiAsync(requestBody);
    }

    private async Task<string> CallOpenAiApiAsync(object requestBody)
    {
        try
        {
            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                throw new HttpRequestException($"OpenAI API returned {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonConvert.DeserializeObject<OpenAiChatResponse>(responseContent);

            if (chatResponse?.Choices?.Any() == true)
            {
                return chatResponse.Choices[0].Message.Content.Trim();
            }

            throw new InvalidOperationException("No response from OpenAI");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error calling OpenAI API");
            throw;
        }
    }

    // Data models for OpenAI API
    private class OpenAiChatResponse
    {
        [JsonProperty("choices")]
        public List<Choice> Choices { get; set; } = new();
    }

    private class Choice
    {
        [JsonProperty("message")]
        public Message Message { get; set; } = new();
    }

    private class Message
    {
        [JsonProperty("content")]
        public string Content { get; set; } = string.Empty;
    }

    // Intent analysis model
    private class IntentAnalysis
    {
        [JsonProperty("shouldRouteToSpecificModule")]
        public bool ShouldRouteToSpecificModule { get; set; }

        [JsonProperty("targetModule")]
        public string TargetModule { get; set; } = "general";

        [JsonProperty("confidence")]
        public double Confidence { get; set; }

        [JsonProperty("reasoning")]
        public string Reasoning { get; set; } = string.Empty;
    }
}
