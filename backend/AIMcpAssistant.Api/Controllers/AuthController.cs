using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AIMcpAssistant.Data.Interfaces;
using AIMcpAssistant.Data.Entities;

namespace AIMcpAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    private readonly IUserRepository _userRepository;

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger, IUserRepository userRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _userRepository = userRepository;
    }

    [HttpGet("login/google")]
    public IActionResult LoginGoogle(string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "Auth", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        
        // Request additional scopes for Gmail and Calendar
        properties.Items["scope"] = "openid profile email https://www.googleapis.com/auth/gmail.modify https://www.googleapis.com/auth/calendar.readonly";
        
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("login/microsoft")]
    public IActionResult LoginMicrosoft(string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(MicrosoftCallback), "Auth", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        
        // Request additional scopes for Outlook and Calendar
        properties.Items["scope"] = "openid profile email https://graph.microsoft.com/Mail.Read https://graph.microsoft.com/Calendars.Read";
        
        return Challenge(properties, MicrosoftAccountDefaults.AuthenticationScheme);
    }

    [HttpGet("/signin-google")]
    public async Task<IActionResult> GoogleSignIn()
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        
        if (!result.Succeeded)
        {
            _logger.LogWarning("Google authentication failed: {Error}", result.Failure?.Message);
            var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
            var errorUrl = $"{frontendUrl}/auth/callback?error=authentication_failed&provider=google";
            return Redirect(errorUrl);
        }

        var token = await GenerateJwtToken(result.Principal, "Google", result.Properties);
        
        // Redirect to frontend with token
        var frontendUrl2 = _configuration["Frontend:Url"] ?? "http://localhost:3000";
        var redirectUrl = $"{frontendUrl2}/auth/callback?token={token}&provider=google";
        
        return Redirect(redirectUrl);
    }

    [HttpGet("callback/google")]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = null)
    {
        try
        {
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            
            if (!result.Succeeded)
            {
                _logger.LogWarning("Google authentication failed");
                return BadRequest(new { error = "Authentication failed" });
            }

            var token = await GenerateJwtToken(result.Principal, "Google", result.Properties);
            
            // Redirect to frontend with token
            var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
            var redirectUrl = $"{frontendUrl}/auth/callback?token={token}&provider=google";
            
            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Google callback");
            return StatusCode(500, new { error = "Authentication error" });
        }
    }

    [HttpGet("/signin-microsoft")]
    public async Task<IActionResult> MicrosoftSignIn()
    {
        var result = await HttpContext.AuthenticateAsync(MicrosoftAccountDefaults.AuthenticationScheme);
        
        if (!result.Succeeded)
        {
            _logger.LogWarning("Microsoft authentication failed: {Error}", result.Failure?.Message);
            var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
            var errorUrl = $"{frontendUrl}/auth/callback?error=authentication_failed&provider=microsoft";
            return Redirect(errorUrl);
        }

        var token = await GenerateJwtToken(result.Principal, "Microsoft", result.Properties);
        
        // Redirect to frontend with token
        var frontendUrl2 = _configuration["Frontend:Url"] ?? "http://localhost:3000";
        var redirectUrl = $"{frontendUrl2}/auth/callback?token={token}&provider=microsoft";
        
        return Redirect(redirectUrl);
    }

    [HttpGet("callback/microsoft")]
    public async Task<IActionResult> MicrosoftCallback(string? returnUrl = null)
    {
        try
        {
            var result = await HttpContext.AuthenticateAsync(MicrosoftAccountDefaults.AuthenticationScheme);
            
            if (!result.Succeeded)
            {
                _logger.LogWarning("Microsoft authentication failed");
                return BadRequest(new { error = "Authentication failed" });
            }

            var token = await GenerateJwtToken(result.Principal, "Microsoft", result.Properties);
            
            // Redirect to frontend with token
            var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
            var redirectUrl = $"{frontendUrl}/auth/callback?token={token}&provider=microsoft";
            
            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Microsoft callback");
            return StatusCode(500, new { error = "Authentication error" });
        }
    }

    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> RefreshToken()
    {
        try
        {
            var refreshToken = User.FindFirst("refresh_token")?.Value;
            var provider = User.FindFirst("provider")?.Value;
            
            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(provider))
            {
                return BadRequest(new { error = "Invalid refresh token or provider" });
            }

            // Implement token refresh logic based on provider
            // This would involve calling the respective OAuth provider's token refresh endpoint
            
            return Ok(new { message = "Token refresh not yet implemented" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, new { error = "Token refresh failed" });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await HttpContext.SignOutAsync();
            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { error = "Logout failed" });
        }
    }

    [HttpGet("user")]
    [Authorize]
    public IActionResult GetUser()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var name = User.FindFirst(ClaimTypes.Name)?.Value;
            var provider = User.FindFirst("provider")?.Value;
            var tokenExpiry = User.FindFirst("token_expiry")?.Value;
            
            return Ok(new
            {
                userId,
                email,
                name,
                provider,
                tokenExpiry,
                isAuthenticated = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info");
            return StatusCode(500, new { error = "Failed to get user info" });
        }
    }

    private async Task<string> GenerateJwtToken(ClaimsPrincipal principal, string provider, AuthenticationProperties properties)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "AIMcpAssistant";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "AIMcpAssistant";
        
        // Extract user information from claims
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User ID not found in claims");
        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? throw new InvalidOperationException("Email not found in claims");
        var name = principal.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown User";
        
        // Ensure user exists in database
        await EnsureUserExistsAsync(userId, email, name, provider);
        
        // Extract and store OAuth tokens
        string? accessToken = null;
        string? refreshToken = null;
        DateTime? tokenExpiresAt = null;
        
        if (properties.Items.TryGetValue(".Token.access_token", out accessToken) && !string.IsNullOrEmpty(accessToken))
        {
            // Store access token in database for background services
        }
        
        if (properties.Items.TryGetValue(".Token.refresh_token", out refreshToken) && !string.IsNullOrEmpty(refreshToken))
        {
            // Store refresh token in database
        }
        
        if (properties.Items.TryGetValue(".Token.expires_at", out var expiresAtStr) && !string.IsNullOrEmpty(expiresAtStr))
        {
            if (DateTime.TryParse(expiresAtStr, out var parsedExpiry))
            {
                tokenExpiresAt = parsedExpiry;
            }
        }
        
        // Save OAuth tokens to database for background email checking
        if (!string.IsNullOrEmpty(accessToken))
        {
            await _userRepository.UpdateOAuthTokensAsync(userId, accessToken, refreshToken, tokenExpiresAt);
            _logger.LogDebug("Stored OAuth tokens for user: {UserId}", userId);
        }
        
        var claims = new List<Claim>(principal.Claims)
        {
            new("provider", provider),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add OAuth tokens to JWT claims for immediate use
        if (!string.IsNullOrEmpty(accessToken))
        {
            claims.Add(new Claim("access_token", accessToken));
        }
        
        if (!string.IsNullOrEmpty(refreshToken))
        {
            claims.Add(new Claim("refresh_token", refreshToken));
        }
        
        if (tokenExpiresAt.HasValue)
        {
            claims.Add(new Claim("token_expiry", tokenExpiresAt.Value.ToString("O")));
        }

        // Add scopes
        var scopes = GetScopesForProvider(provider);
        claims.Add(new Claim("scopes", string.Join(",", scopes)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task EnsureUserExistsAsync(string userId, string email, string name, string provider)
    {
        try
        {
            var existingUser = await _userRepository.GetByUserIdAsync(userId);
            
            if (existingUser == null)
            {
                // Create new user
                var newUser = new User
                {
                    UserId = userId,
                    Email = email,
                    Name = name,
                    Provider = provider,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };
                
                await _userRepository.AddAsync(newUser);
                _logger.LogInformation("Created new user: {UserId} ({Email})", userId, email);
            }
            else
            {
                // Update existing user's last login
                await _userRepository.UpdateLastLoginAsync(userId);
                _logger.LogDebug("Updated last login for user: {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring user exists: {UserId} ({Email})", userId, email);
            throw;
        }
    }

    private static List<string> GetScopesForProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => new List<string>
            {
                "https://www.googleapis.com/auth/gmail.modify",
                "https://www.googleapis.com/auth/calendar.readonly"
            },
            "microsoft" => new List<string>
            {
                "https://graph.microsoft.com/Mail.Read",
                "https://graph.microsoft.com/Calendars.Read"
            },
            _ => new List<string>()
        };
    }
}