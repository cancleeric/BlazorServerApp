using LocalIdentityServer.Models.Requests;

namespace LocalIdentityServer.Services;

/// <summary>
/// Service for OIDC End Session functionality
/// </summary>
public interface IEndSessionService
{
    /// <summary>
    /// Validates the end session request
    /// </summary>
    /// <param name="request">End session request</param>
    /// <returns>Validation result</returns>
    Task<EndSessionValidationResult> ValidateEndSessionRequestAsync(EndSessionRequest request);

    /// <summary>
    /// Processes the logout and cleans up sessions and tokens
    /// </summary>
    /// <param name="request">End session request</param>
    /// <param name="userId">User ID to logout (if available)</param>
    /// <returns>Logout result</returns>
    Task<EndSessionResult> ProcessLogoutAsync(EndSessionRequest request, string? userId);

    /// <summary>
    /// Gets the post-logout redirect URL
    /// </summary>
    /// <param name="request">End session request</param>
    /// <param name="clientId">Client ID</param>
    /// <returns>Redirect URL or null if no redirect</returns>
    Task<string?> GetPostLogoutRedirectUriAsync(EndSessionRequest request, string? clientId);
}

/// <summary>
/// End session validation result
/// </summary>
public class EndSessionValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ClientId { get; set; }
    public string? Subject { get; set; }
    public List<string> ValidPostLogoutRedirectUris { get; set; } = new();
}

/// <summary>
/// End session processing result
/// </summary>
public class EndSessionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PostLogoutRedirectUri { get; set; }
    public string? State { get; set; }
    public bool RequiresUserInteraction { get; set; }
}