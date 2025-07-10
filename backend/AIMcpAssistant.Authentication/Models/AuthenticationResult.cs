namespace AIMcpAssistant.Authentication.Models;

public class AuthenticationResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public AuthenticatedUser? User { get; set; }
    public string? RedirectUrl { get; set; }

    public static AuthenticationResult Success(AuthenticatedUser user, string? redirectUrl = null)
    {
        return new AuthenticationResult
        {
            IsSuccess = true,
            User = user,
            RedirectUrl = redirectUrl
        };
    }

    public static AuthenticationResult Failure(string errorMessage)
    {
        return new AuthenticationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}