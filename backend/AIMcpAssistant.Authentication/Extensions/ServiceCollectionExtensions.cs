using AIMcpAssistant.Authentication.Interfaces;
using AIMcpAssistant.Authentication.Services;
using AIMcpAssistant.Core.Interfaces;
using AIMcpAssistant.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AIMcpAssistant.Authentication.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register HTTP client
        services.AddHttpClient<IGoogleAuthenticationProvider, GoogleAuthenticationProvider>();
        services.AddHttpClient<IMicrosoftAuthenticationProvider, MicrosoftAuthenticationProvider>();

        // Register authentication services
        services.AddScoped<IGoogleAuthenticationProvider, GoogleAuthenticationProvider>();
        services.AddScoped<IMicrosoftAuthenticationProvider, MicrosoftAuthenticationProvider>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        
        // Register token services
        services.AddScoped<ITokenStorageService, TokenStorageService>();
        services.AddScoped<ITokenRefreshService, TokenRefreshService>();

        // Configure JWT authentication
        var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var key = Encoding.UTF8.GetBytes(jwtKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = "Cookies";
        })
        .AddCookie("Cookies")
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };
        })
        .AddGoogle(options =>
        {
            options.ClientId = configuration["Authentication:Google:ClientId"] ?? string.Empty;
            options.ClientSecret = configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
            options.SaveTokens = true;
            
            // Add required scopes
            options.Scope.Add("email");
            options.Scope.Add("profile");
            options.Scope.Add("https://www.googleapis.com/auth/gmail.modify");
            options.Scope.Add("https://www.googleapis.com/auth/calendar.readonly");
        })
        .AddMicrosoftAccount(options =>
        {
            options.ClientId = configuration["Authentication:Microsoft:ClientId"] ?? string.Empty;
            options.ClientSecret = configuration["Authentication:Microsoft:ClientSecret"] ?? string.Empty;
            options.SaveTokens = true;
            
            // Add required scopes
            options.Scope.Add("https://graph.microsoft.com/Mail.Read");
            options.Scope.Add("https://graph.microsoft.com/Calendars.Read");
        });

        return services;
    }
}