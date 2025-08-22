namespace LocalIdentityServer.Models.Requests;

/// <summary>
/// OIDC End Session Request Model (OIDC Core 1.0)
/// </summary>
public class EndSessionRequest
{
    /// <summary>
    /// Previously issued ID Token (optional but recommended)
    /// </summary>
    public string? IdTokenHint { get; set; }

    /// <summary>
    /// URL to redirect to after logout (optional)
    /// </summary>
    public string? PostLogoutRedirectUri { get; set; }

    /// <summary>
    /// State parameter to maintain state between the logout request and callback (optional)
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// UI locales hint (optional)
    /// </summary>
    public string? UiLocales { get; set; }

    /// <summary>
    /// Client ID hint (optional, derived from id_token_hint if not provided)
    /// </summary>
    public string? ClientId { get; set; }
}