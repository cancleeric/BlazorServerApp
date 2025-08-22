using LocalIdentityServer.Models.Responses;

namespace LocalIdentityServer.Services;

/// <summary>
/// Service for OAuth2 Token Introspection (RFC 7662)
/// </summary>
public interface ITokenIntrospectionService
{
    /// <summary>
    /// Introspects a token and returns information about its current state
    /// </summary>
    /// <param name="token">The token to introspect</param>
    /// <param name="tokenTypeHint">Optional hint about the token type</param>
    /// <param name="clientId">The client requesting introspection</param>
    /// <returns>Token introspection response</returns>
    Task<IntrospectionResponse> IntrospectTokenAsync(string token, string? tokenTypeHint, string clientId);

    /// <summary>
    /// Validates that the client is authorized to introspect tokens
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="clientSecret">Client Secret</param>
    /// <returns>True if authorized, false otherwise</returns>
    Task<bool> ValidateClientAsync(string clientId, string? clientSecret);
}