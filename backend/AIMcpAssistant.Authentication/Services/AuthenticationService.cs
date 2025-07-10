using AIMcpAssistant.Authentication.Interfaces;
using AIMcpAssistant.Authentication.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AIMcpAssistant.Authentication.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IGoogleAuthenticationProvider _googleProvider;
    private readonly IMicrosoftAuthenticationProvider _microsoftProvider;
    private readonly IConfiguration _configuration;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public AuthenticationService(
        IGoogleAuthenticationProvider googleProvider,
        IMicrosoftAuthenticationProvider microsoftProvider,
        IConfiguration configuration)
    {
        _googleProvider = googleProvider;
        _microsoftProvider = microsoftProvider;
        _configuration = configuration;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string provider, string authorizationCode, string? redirectUri = null)
    {
        return provider.ToLower() switch
        {
            "google" => await _googleProvider.AuthenticateAsync(authorizationCode, redirectUri),
            "microsoft" => await _microsoftProvider.AuthenticateAsync(authorizationCode, redirectUri),
            _ => AuthenticationResult.Failure($"Unsupported authentication provider: {provider}")
        };
    }

    public async Task<AuthenticationResult> RefreshTokenAsync(string refreshToken, AuthenticationProvider provider)
    {
        return provider switch
        {
            AuthenticationProvider.Google => await _googleProvider.RefreshTokenAsync(refreshToken),
            AuthenticationProvider.Microsoft => await _microsoftProvider.RefreshTokenAsync(refreshToken),
            _ => AuthenticationResult.Failure($"Unsupported authentication provider: {provider}")
        };
    }

    public async Task<bool> ValidateTokenAsync(string accessToken, AuthenticationProvider provider)
    {
        return provider switch
        {
            AuthenticationProvider.Google => await _googleProvider.ValidateTokenAsync(accessToken),
            AuthenticationProvider.Microsoft => await _microsoftProvider.ValidateTokenAsync(accessToken),
            _ => false
        };
    }

    public async Task<AuthenticatedUser?> GetUserFromTokenAsync(string accessToken, AuthenticationProvider provider)
    {
        return provider switch
        {
            AuthenticationProvider.Google => await _googleProvider.GetUserInfoAsync(accessToken),
            AuthenticationProvider.Microsoft => await _microsoftProvider.GetUserInfoAsync(accessToken),
            _ => null
        };
    }

    public async Task<string> GenerateJwtTokenAsync(AuthenticatedUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new("provider", user.Provider.ToString())
        };

        // Add custom claims
        foreach (var claim in user.Claims)
        {
            claims.Add(new Claim(claim.Key, claim.Value));
        }

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return await Task.FromResult(_tokenHandler.WriteToken(token));
    }

    public async Task<ClaimsPrincipal?> ValidateJwtTokenAsync(string jwtToken)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured")));
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            };

            var principal = _tokenHandler.ValidateToken(jwtToken, validationParameters, out _);
            return await Task.FromResult(principal);
        }
        catch
        {
            return await Task.FromResult<ClaimsPrincipal?>(null);
        }
    }

    public async Task RevokeTokenAsync(string accessToken, AuthenticationProvider provider)
    {
        switch (provider)
        {
            case AuthenticationProvider.Google:
                await _googleProvider.RevokeTokenAsync(accessToken);
                break;
            case AuthenticationProvider.Microsoft:
                await _microsoftProvider.RevokeTokenAsync(accessToken);
                break;
        }
    }
}