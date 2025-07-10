using AIMcpAssistant.Authentication.Interfaces;
using AIMcpAssistant.Authentication.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace AIMcpAssistant.Authentication.Services;

public class GoogleAuthenticationProvider : IGoogleAuthenticationProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public GoogleAuthenticationProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _clientId = _configuration["Authentication:Google:ClientId"] ?? throw new InvalidOperationException("Google ClientId not configured");
        _clientSecret = _configuration["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Google ClientSecret not configured");
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string authorizationCode, string? redirectUri = null)
    {
        try
        {
            var tokenResponse = await ExchangeCodeForTokenAsync(authorizationCode, redirectUri);
            if (tokenResponse == null)
            {
                return AuthenticationResult.Failure("Failed to exchange authorization code for token");
            }

            var userInfo = await GetUserInfoAsync(tokenResponse.AccessToken);
            if (userInfo == null)
            {
                return AuthenticationResult.Failure("Failed to retrieve user information");
            }

            userInfo.AccessToken = tokenResponse.AccessToken;
            userInfo.RefreshToken = tokenResponse.RefreshToken;
            userInfo.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            return AuthenticationResult.Success(userInfo);
        }
        catch (Exception ex)
        {
            return AuthenticationResult.Failure($"Google authentication failed: {ex.Message}");
        }
    }

    public async Task<AuthenticationResult> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
            
            if (!response.IsSuccessStatusCode)
            {
                return AuthenticationResult.Failure("Failed to refresh Google token");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<GoogleTokenResponse>(jsonResponse);
            
            if (tokenData == null)
            {
                return AuthenticationResult.Failure("Invalid token response from Google");
            }

            var userInfo = await GetUserInfoAsync(tokenData.AccessToken);
            if (userInfo == null)
            {
                return AuthenticationResult.Failure("Failed to retrieve user information");
            }

            userInfo.AccessToken = tokenData.AccessToken;
            userInfo.RefreshToken = refreshToken; // Keep the original refresh token
            userInfo.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);

            return AuthenticationResult.Success(userInfo);
        }
        catch (Exception ex)
        {
            return AuthenticationResult.Failure($"Token refresh failed: {ex.Message}");
        }
    }

    public async Task<bool> ValidateTokenAsync(string accessToken)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://www.googleapis.com/oauth2/v1/tokeninfo?access_token={accessToken}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<AuthenticatedUser?> GetUserInfoAsync(string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(jsonResponse);
            
            if (userInfo == null)
            {
                return null;
            }

            return new AuthenticatedUser
            {
                UserId = userInfo.Id,
                Email = userInfo.Email,
                Name = userInfo.Name,
                Provider = AuthenticationProvider.Google,
                Claims = new Dictionary<string, string>
                {
                    ["picture"] = userInfo.Picture ?? string.Empty,
                    ["verified_email"] = userInfo.VerifiedEmail.ToString()
                }
            };
        }
        catch
        {
            return null;
        }
        finally
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task RevokeTokenAsync(string accessToken)
    {
        try
        {
            await _httpClient.PostAsync($"https://oauth2.googleapis.com/revoke?token={accessToken}", null);
        }
        catch
        {
            // Ignore revocation errors
        }
    }

    private async Task<GoogleTokenResponse?> ExchangeCodeForTokenAsync(string authorizationCode, string? redirectUri)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["code"] = authorizationCode,
            ["grant_type"] = "authorization_code"
        };

        if (!string.IsNullOrEmpty(redirectUri))
        {
            parameters["redirect_uri"] = redirectUri;
        }

        var content = new FormUrlEncodedContent(parameters);
        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GoogleTokenResponse>(jsonResponse);
    }

    private class GoogleTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = string.Empty;
    }

    private class GoogleUserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Picture { get; set; }
        public bool VerifiedEmail { get; set; }
    }
}