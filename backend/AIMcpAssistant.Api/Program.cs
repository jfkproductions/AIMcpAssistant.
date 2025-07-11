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

// Register repositories
builder.Services.AddScoped<AIMcpAssistant.Data.Interfaces.ICommandHistoryRepository, AIMcpAssistant.Data.Repositories.CommandHistoryRepository>();
builder.Services.AddScoped<AIMcpAssistant.Data.Interfaces.IUnitOfWork, AIMcpAssistant.Data.UnitOfWork>();

// Register core services
builder.Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
builder.Services.AddScoped<AIMcpAssistant.Core.Services.IConversationContextService, AIMcpAssistant.Core.Services.ConversationContextService>();

// Register individual MCPs manually for more control
builder.Services.AddScoped<EmailMcp>();
builder.Services.AddScoped<CalendarMcp>();
builder.Services.AddHttpClient<ChatGptMcp>();

// Register background services
builder.Services.AddHostedService<AIMcpAssistant.Api.Services.EmailNotificationService>();
builder.Services.AddScoped<AIMcpAssistant.Api.Services.ModuleRegistrationService>();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

app.Logger.LogInformation("Application built.");

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");



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

try
{
    app.Logger.LogInformation("Starting application.");

    // Register MCP modules before running the app
    using (var scope = app.Services.CreateScope())
    {
        var moduleRegistrationService = scope.ServiceProvider.GetRequiredService<AIMcpAssistant.Api.Services.ModuleRegistrationService>();
        await moduleRegistrationService.RegisterModulesAsync();
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Application terminated unexpectedly.");
}
finally
{
    app.Logger.LogInformation("Application shutting down.");
}