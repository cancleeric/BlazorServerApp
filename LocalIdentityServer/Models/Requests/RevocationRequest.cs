using System.ComponentModel.DataAnnotations;

namespace LocalIdentityServer.Models.Requests;

/// <summary>
/// OAuth2 Token Revocation Request Model (RFC 7009)
/// </summary>
public class RevocationRequest
{
    /// <summary>
    /// The token to be revoked (required)
    /// </summary>
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Optional hint about the token type (access_token, refresh_token, etc.)
    /// </summary>
    public string? TokenTypeHint { get; set; }

    /// <summary>
    /// Client ID for authentication
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client Secret for authentication
    /// </summary>
    public string? ClientSecret { get; set; }
}