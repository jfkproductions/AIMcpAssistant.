using AIMcpAssistant.Core.Models;

namespace AIMcpAssistant.Core.Interfaces;

public interface ITokenRefreshService
{
    /// <summary>
    /// Gets a valid access token for the specified user, refreshing it if necessary
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>A valid access token or null if refresh failed</returns>
    Task<string?> GetValidAccessTokenAsync(string userId);
    
    /// <summary>
    /// Refreshes the access token for the specified user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>True if refresh was successful, false otherwise</returns>
    Task<bool> RefreshTokenAsync(string userId);
}