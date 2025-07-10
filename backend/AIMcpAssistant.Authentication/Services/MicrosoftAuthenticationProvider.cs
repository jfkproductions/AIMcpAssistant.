using AIMcpAssistant.Authentication.Interfaces;
using AIMcpAssistant.Authentication.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace AIMcpAssistant.Authentication.Services;

public class MicrosoftAuthenticationProvider : IMicrosoftAuthenticationProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _tenantId;

    public MicrosoftAuthenticationProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _clientId = _configuration["Authentication:Microsoft:ClientId"] ?? throw new InvalidOperationException("Microsoft ClientId not configured");
        _clientSecret = _configuration["Authentication:Microsoft:ClientSecret"] ?? throw new InvalidOperationException("Microsoft ClientSecret not configured");
        _tenantId = _configuration["Authentication:Microsoft:TenantId"] ?? "common";
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
            return AuthenticationResult.Failure($"Microsoft authentication failed: {ex.Message}");
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
                ["grant_type"] = "refresh_token",
                ["scope"] = "https://graph.microsoft.com/User.Read https://graph.microsoft.com/Mail.ReadWrite https://graph.microsoft.com/Calendars.ReadWrite"
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync($"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token", content);
            
            if (!response.IsSuccessStatusCode)
            {
                return AuthenticationResult.Failure("Failed to refresh Microsoft token");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<MicrosoftTokenResponse>(jsonResponse);
            
            if (tokenData == null)
            {
                return AuthenticationResult.Failure("Invalid token response from Microsoft");
            }

            var userInfo = await GetUserInfoAsync(tokenData.AccessToken);
            if (userInfo == null)
            {
                return AuthenticationResult.Failure("Failed to retrieve user information");
            }

            userInfo.AccessToken = tokenData.AccessToken;
            userInfo.RefreshToken = tokenData.RefreshToken ?? refreshToken;
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
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
        finally
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<AuthenticatedUser?> GetUserInfoAsync(string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<MicrosoftUserInfo>(jsonResponse);
            
            if (userInfo == null)
            {
                return null;
            }

            return new AuthenticatedUser
            {
                UserId = userInfo.Id,
                Email = userInfo.Mail ?? userInfo.UserPrincipalName,
                Name = userInfo.DisplayName,
                Provider = AuthenticationProvider.Microsoft,
                Claims = new Dictionary<string, string>
                {
                    ["job_title"] = userInfo.JobTitle ?? string.Empty,
                    ["office_location"] = userInfo.OfficeLocation ?? string.Empty,
                    ["user_principal_name"] = userInfo.UserPrincipalName
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
            var parameters = new Dictionary<string, string>
            {
                ["token"] = accessToken,
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret
            };

            var content = new FormUrlEncodedContent(parameters);
            await _httpClient.PostAsync($"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/logout", content);
        }
        catch
        {
            // Ignore revocation errors
        }
    }

    private async Task<MicrosoftTokenResponse?> ExchangeCodeForTokenAsync(string authorizationCode, string? redirectUri)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["code"] = authorizationCode,
            ["grant_type"] = "authorization_code",
            ["scope"] = "https://graph.microsoft.com/User.Read https://graph.microsoft.com/Mail.ReadWrite https://graph.microsoft.com/Calendars.ReadWrite"
        };

        if (!string.IsNullOrEmpty(redirectUri))
        {
            parameters["redirect_uri"] = redirectUri;
        }

        var content = new FormUrlEncodedContent(parameters);
        var response = await _httpClient.PostAsync($"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token", content);
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MicrosoftTokenResponse>(jsonResponse);
    }

    private class MicrosoftTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
    }

    private class MicrosoftUserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string UserPrincipalName { get; set; } = string.Empty;
        public string? Mail { get; set; }
        public string? JobTitle { get; set; }
        public string? OfficeLocation { get; set; }
    }
}