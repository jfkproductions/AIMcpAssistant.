using AIMcpAssistant.Api.Hubs;
using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Services;
using AIMcpAssistant.MCPs;
using AIMcpAssistant.Data;
using AIMcpAssistant.Authentication.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var frontendUrl = builder.Configuration["Frontend:Url"] ?? "http://localhost:3000";
        policy.WithOrigins(frontendUrl)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add SignalR
builder.Services.AddSignalR();

// Add HTTP client
builder.Services.AddHttpClient();

// Add logging
builder.Services.AddLogging();

// Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=aimcp.db"));

// Register repositories
builder.Services.AddScoped<AIMcpAssistant.Data.Interfaces.IUserRepository, AIMcpAssistant.Data.Repositories.UserRepository>();
builder.Services.AddScoped<AIMcpAssistant.Data.Repositories.UserModuleSubscriptionRepository>();
builder.Services.AddScoped<AIMcpAssistant.Data.Repositories.ModuleRepository>();

// Add authentication services
builder.Services.AddAuthenticationServices(builder.Configuration);

// Configure JWT for SignalR
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            
            return Task.CompletedTask;
        }
    };
});

// Register core services
builder.Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();

// Register individual MCPs manually for more control
builder.Services.AddScoped<EmailMcp>();
builder.Services.AddScoped<CalendarMcp>();
builder.Services.AddScoped<ChatGptMcp>(provider => 
    new ChatGptMcp(
        provider.GetRequiredService<ILogger<ChatGptMcp>>(),
        provider.GetRequiredService<HttpClient>(),
        provider.GetRequiredService<ICommandDispatcher>()));

// Register background services
// TODO: EmailNotificationService disabled - needs access to user tokens which aren't available in background services
// builder.Services.AddHostedService<AIMcpAssistant.Api.Services.EmailNotificationService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

// Initialize MCP modules
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
    var moduleRepository = scope.ServiceProvider.GetRequiredService<AIMcpAssistant.Data.Repositories.ModuleRepository>();
    
    try
    {
        // Ensure default modules exist in database and cleanup orphaned data
        await moduleRepository.EnsureDefaultModulesExistAsync();
        
        // Cleanup orphaned subscriptions
        var subscriptionRepository = scope.ServiceProvider.GetRequiredService<AIMcpAssistant.Data.Repositories.UserModuleSubscriptionRepository>();
        await subscriptionRepository.CleanupOrphanedSubscriptionsAsync();
        
        logger.LogInformation("Database cleanup completed");
        
        // Register Email MCP
        if (builder.Configuration.GetValue<bool>("McpModules:EmailMcp:Enabled", true))
        {
            try
            {
                var emailMcp = scope.ServiceProvider.GetRequiredService<EmailMcp>();
                await emailMcp.InitializeAsync();
                await dispatcher.RegisterModuleAsync(emailMcp);
                await moduleRepository.UpdateModuleRegistrationStatusAsync("EmailMcp", true);
                logger.LogInformation("EmailMcp module registered");
            }
            catch (Exception ex)
            {
                await moduleRepository.UpdateModuleRegistrationStatusAsync("EmailMcp", false);
                logger.LogError(ex, "Failed to initialize EmailMcp module");
            }
        }
        else
        {
            await moduleRepository.UpdateModuleRegistrationStatusAsync("EmailMcp", false);
            logger.LogInformation("EmailMcp module disabled in configuration");
        }
        
        // Register Calendar MCP
        if (builder.Configuration.GetValue<bool>("McpModules:CalendarMcp:Enabled", true))
        {
            try
            {
                var calendarMcp = scope.ServiceProvider.GetRequiredService<CalendarMcp>();
                await calendarMcp.InitializeAsync();
                await dispatcher.RegisterModuleAsync(calendarMcp);
                await moduleRepository.UpdateModuleRegistrationStatusAsync("CalendarMcp", true);
                logger.LogInformation("CalendarMcp module registered");
            }
            catch (Exception ex)
            {
                await moduleRepository.UpdateModuleRegistrationStatusAsync("CalendarMcp", false);
                logger.LogError(ex, "Failed to initialize CalendarMcp module");
            }
        }
        else
        {
            await moduleRepository.UpdateModuleRegistrationStatusAsync("CalendarMcp", false);
            logger.LogInformation("CalendarMcp module disabled in configuration");
        }
        

        
        // Register ChatGPT MCP
        if (builder.Configuration.GetValue<bool>("McpModules:chatgpt:Enabled", true))
        {
            try
            {
                var chatGptMcp = scope.ServiceProvider.GetRequiredService<ChatGptMcp>();
                var chatGptConfig = new Dictionary<string, object>
                {
                    { "ApiKey", builder.Configuration["OpenAI:ApiKey"] ?? "" }
                };
                await chatGptMcp.InitializeAsync(chatGptConfig);
                await dispatcher.RegisterModuleAsync(chatGptMcp);
                await moduleRepository.UpdateModuleRegistrationStatusAsync("chatgpt", true);
                logger.LogInformation("ChatGptMcp module registered");
            }
            catch (Exception ex)
            {
                await moduleRepository.UpdateModuleRegistrationStatusAsync("chatgpt", false);
                logger.LogError(ex, "Failed to initialize ChatGptMcp module");
            }
        }
        else
        {
            await moduleRepository.UpdateModuleRegistrationStatusAsync("chatgpt", false);
            logger.LogInformation("ChatGptMcp module disabled in configuration");
        }
        
        logger.LogInformation("MCP module initialization completed");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing MCP modules");
    }
});

// Add health check endpoint
app.MapGet("/health", () => new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
});

// Add MCP status endpoint
app.MapGet("/api/status", async (ICommandDispatcher dispatcher) =>
{
    var modules = await dispatcher.GetRegisteredModulesAsync();
    return new
    {
        status = "running",
        modulesCount = modules.Count,
        modules = modules.Select(m => new
        {
            id = m.Id,
            name = m.Name,
            priority = m.Priority
        })
    };
});

app.Run();