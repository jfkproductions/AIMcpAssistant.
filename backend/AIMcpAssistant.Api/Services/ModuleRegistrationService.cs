using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.MCPs;

namespace AIMcpAssistant.Api.Services;

public class ModuleRegistrationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleRegistrationService> _logger;

    public ModuleRegistrationService(IServiceProvider serviceProvider, ILogger<ModuleRegistrationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _logger.LogInformation("ModuleRegistrationService created.");
    }

    public async Task RegisterModulesAsync()
    {
        _logger.LogInformation("Module Registration Service is starting.");

        using var scope = _serviceProvider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var moduleRepository = scope.ServiceProvider.GetRequiredService<AIMcpAssistant.Data.Repositories.ModuleRepository>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        try
        {
            await moduleRepository.EnsureDefaultModulesExistAsync();
            _logger.LogInformation("Database cleanup completed");

            if (configuration.GetValue<bool>("McpModules:email:Enabled", true))
            {
                try
                {
                    var emailMcp = scope.ServiceProvider.GetRequiredService<EmailMcp>();
                    await emailMcp.InitializeAsync();
                    await dispatcher.RegisterModuleAsync(emailMcp);
                    await moduleRepository.UpdateModuleRegistrationStatusAsync("email", true);
                    _logger.LogInformation("Email module registered");
                }
                catch (Exception ex)
                {
                    await moduleRepository.UpdateModuleRegistrationStatusAsync("email", false);
                    _logger.LogError(ex, "Failed to initialize Email module");
                }
            }
            else
            {
                await moduleRepository.UpdateModuleRegistrationStatusAsync("email", false);
                _logger.LogInformation("Email module disabled in configuration");
            }

            if (configuration.GetValue<bool>("McpModules:calendar:Enabled", true))
            {
                try
                {
                    var calendarMcp = scope.ServiceProvider.GetRequiredService<CalendarMcp>();
                    await calendarMcp.InitializeAsync();
                    await dispatcher.RegisterModuleAsync(calendarMcp);
                    await moduleRepository.UpdateModuleRegistrationStatusAsync("calendar", true);
                    _logger.LogInformation("Calendar module registered");
                }
                catch (Exception ex)
                {
                    await moduleRepository.UpdateModuleRegistrationStatusAsync("calendar", false);
                    _logger.LogError(ex, "Failed to initialize Calendar module");
                }
            }
            else
            {
                await moduleRepository.UpdateModuleRegistrationStatusAsync("calendar", false);
                _logger.LogInformation("Calendar module disabled in configuration");
            }

            if (configuration.GetValue<bool>("McpModules:chatgpt:Enabled", true))
            {
                try
                {
                    var chatGptMcp = scope.ServiceProvider.GetRequiredService<ChatGptMcp>();
                    var chatGptConfig = new Dictionary<string, object>
                    {
                        { "ApiKey", configuration["OpenAI:ApiKey"] ?? "" }
                    };
                    await chatGptMcp.InitializeAsync(chatGptConfig);
                    await dispatcher.RegisterModuleAsync(chatGptMcp);
                    await moduleRepository.UpdateModuleRegistrationStatusAsync("chatgpt", true);
                    _logger.LogInformation("ChatGptMcp module registered");
                }
                catch (Exception ex)
                {
                    await moduleRepository.UpdateModuleRegistrationStatusAsync("chatgpt", false);
                    _logger.LogError(ex, "Failed to initialize ChatGPT module");
                }
            }
            else
            {
                await moduleRepository.UpdateModuleRegistrationStatusAsync("chatgpt", false);
                _logger.LogInformation("ChatGPT module disabled in configuration");
            }

            _logger.LogInformation("MCP module initialization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while registering MCP modules.");
        }
    }
}