using AIMcpAssistant.Authentication.Interfaces;
using AIMcpAssistant.Authentication.Models;
using AIMcpAssistant.Authentication.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Security.Claims;
using Xunit;

namespace AIMcpAssistant.Tests.Authentication;

public class AuthenticationServiceTests
{
    private readonly Mock<IGoogleAuthenticationProvider> _mockGoogleProvider;
    private readonly Mock<IMicrosoftAuthenticationProvider> _mockMicrosoftProvider;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly AuthenticationService _authService;

    public AuthenticationServiceTests()
    {
        _mockGoogleProvider = new Mock<IGoogleAuthenticationProvider>();
        _mockMicrosoftProvider = new Mock<IMicrosoftAuthenticationProvider>();
        _mockConfiguration = new Mock<IConfiguration>();

        // Setup JWT configuration
        _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns("this-is-a-very-long-secret-key-for-jwt-token-generation-that-is-at-least-256-bits");
        _mockConfiguration.Setup(c => c["Jwt:Issuer"]).Returns("AIMcpAssistant");
        _mockConfiguration.Setup(c => c["Jwt:Audience"]).Returns("AIMcpAssistant-Users");

        _authService = new AuthenticationService(
            _mockGoogleProvider.Object,
            _mockMicrosoftProvider.Object,
            _mockConfiguration.Object);
    }

    [Fact]
    public async Task AuthenticateAsync_WithGoogleProvider_ShouldCallGoogleProvider()
    {
        // Arrange
        var authCode = "test-auth-code";
        var redirectUri = "https://localhost:3000/callback";
        var expectedResult = AuthenticationResult.Success(new AuthenticatedUser
        {
            UserId = "google-user-123",
            Email = "test@gmail.com",
            Name = "Test User",
            Provider = AuthenticationProvider.Google
        });

        _mockGoogleProvider
            .Setup(p => p.AuthenticateAsync(authCode, redirectUri))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _authService.AuthenticateAsync("google", authCode, redirectUri);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("google-user-123", result.User?.UserId);
        _mockGoogleProvider.Verify(p => p.AuthenticateAsync(authCode, redirectUri), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithMicrosoftProvider_ShouldCallMicrosoftProvider()
    {
        // Arrange
        var authCode = "test-auth-code";
        var redirectUri = "https://localhost:3000/callback";
        var expectedResult = AuthenticationResult.Success(new AuthenticatedUser
        {
            UserId = "microsoft-user-123",
            Email = "test@outlook.com",
            Name = "Test User",
            Provider = AuthenticationProvider.Microsoft
        });

        _mockMicrosoftProvider
            .Setup(p => p.AuthenticateAsync(authCode, redirectUri))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _authService.AuthenticateAsync("microsoft", authCode, redirectUri);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("microsoft-user-123", result.User?.UserId);
        _mockMicrosoftProvider.Verify(p => p.AuthenticateAsync(authCode, redirectUri), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnsupportedProvider_ShouldReturnFailure()
    {
        // Act
        var result = await _authService.AuthenticateAsync("unsupported", "auth-code");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Unsupported authentication provider", result.ErrorMessage);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithGoogleProvider_ShouldCallGoogleProvider()
    {
        // Arrange
        var refreshToken = "google-refresh-token";
        var expectedResult = AuthenticationResult.Success(new AuthenticatedUser
        {
            UserId = "google-user-123",
            Email = "test@gmail.com",
            Name = "Test User",
            Provider = AuthenticationProvider.Google
        });

        _mockGoogleProvider
            .Setup(p => p.RefreshTokenAsync(refreshToken))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _authService.RefreshTokenAsync(refreshToken, AuthenticationProvider.Google);

        // Assert
        Assert.True(result.IsSuccess);
        _mockGoogleProvider.Verify(p => p.RefreshTokenAsync(refreshToken), Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithGoogleProvider_ShouldCallGoogleProvider()
    {
        // Arrange
        var accessToken = "google-access-token";
        _mockGoogleProvider
            .Setup(p => p.ValidateTokenAsync(accessToken))
            .ReturnsAsync(true);

        // Act
        var result = await _authService.ValidateTokenAsync(accessToken, AuthenticationProvider.Google);

        // Assert
        Assert.True(result);
        _mockGoogleProvider.Verify(p => p.ValidateTokenAsync(accessToken), Times.Once);
    }

    [Fact]
    public async Task GenerateJwtTokenAsync_ShouldCreateValidToken()
    {
        // Arrange
        var user = new AuthenticatedUser
        {
            UserId = "test-user-123",
            Email = "test@example.com",
            Name = "Test User",
            Provider = AuthenticationProvider.Google,
            Claims = new Dictionary<string, string>
            {
                ["custom_claim"] = "custom_value"
            }
        };

        // Act
        var token = await _authService.GenerateJwtTokenAsync(user);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        
        // Validate the token
        var principal = await _authService.ValidateJwtTokenAsync(token);
        Assert.NotNull(principal);
        Assert.Equal("test-user-123", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("test@example.com", principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("Test User", principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("Google", principal.FindFirst("provider")?.Value);
        Assert.Equal("custom_value", principal.FindFirst("custom_claim")?.Value);
    }

    [Fact]
    public async Task ValidateJwtTokenAsync_WithInvalidToken_ShouldReturnNull()
    {
        // Act
        var result = await _authService.ValidateJwtTokenAsync("invalid-token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithGoogleProvider_ShouldCallGoogleProvider()
    {
        // Arrange
        var accessToken = "google-access-token";

        // Act
        await _authService.RevokeTokenAsync(accessToken, AuthenticationProvider.Google);

        // Assert
        _mockGoogleProvider.Verify(p => p.RevokeTokenAsync(accessToken), Times.Once);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithMicrosoftProvider_ShouldCallMicrosoftProvider()
    {
        // Arrange
        var accessToken = "microsoft-access-token";

        // Act
        await _authService.RevokeTokenAsync(accessToken, AuthenticationProvider.Microsoft);

        // Assert
        _mockMicrosoftProvider.Verify(p => p.RevokeTokenAsync(accessToken), Times.Once);
    }
}