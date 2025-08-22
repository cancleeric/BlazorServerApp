using System.Text.Json.Serialization;

namespace LocalIdentityServer.Models.Responses;

/// <summary>
/// OAuth2 Token Introspection Response Model (RFC 7662)
/// </summary>
public class IntrospectionResponse
{
    /// <summary>
    /// Boolean indicator of whether the token is currently active
    /// </summary>
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    /// <summary>
    /// Client identifier for the OAuth 2.0 client that requested this token
    /// </summary>
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    /// <summary>
    /// Human-readable identifier for the resource owner who authorized this token
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    /// <summary>
    /// A JSON string containing a space-separated list of scopes
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// Integer timestamp indicating when this token will expire
    /// </summary>
    [JsonPropertyName("exp")]
    public long? Exp { get; set; }

    /// <summary>
    /// Integer timestamp indicating when this token was issued
    /// </summary>
    [JsonPropertyName("iat")]
    public long? Iat { get; set; }

    /// <summary>
    /// Integer timestamp indicating when this token became active
    /// </summary>
    [JsonPropertyName("nbf")]
    public long? Nbf { get; set; }

    /// <summary>
    /// Subject of the token, usually a machine-readable identifier
    /// </summary>
    [JsonPropertyName("sub")]
    public string? Sub { get; set; }

    /// <summary>
    /// Audience(s) for the token
    /// </summary>
    [JsonPropertyName("aud")]
    public string? Aud { get; set; }

    /// <summary>
    /// Issuer of the token
    /// </summary>
    [JsonPropertyName("iss")]
    public string? Iss { get; set; }

    /// <summary>
    /// Unique identifier for the token
    /// </summary>
    [JsonPropertyName("jti")]
    public string? Jti { get; set; }

    /// <summary>
    /// Type of the token (e.g., "Bearer")
    /// </summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
}