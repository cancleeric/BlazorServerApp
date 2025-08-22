namespace LocalIdentityServer.Services;

/// <summary>
/// Service for OAuth2 Token Revocation (RFC 7009)
/// </summary>
public interface ITokenRevocationService
{
    /// <summary>
    /// Revokes a token (access token or refresh token)
    /// </summary>
    /// <param name="token">The token to revoke</param>
    /// <param name="tokenTypeHint">Optional hint about the token type</param>
    /// <param name="clientId">The client requesting revocation</param>
    /// <param name="reason">Reason for revocation</param>
    /// <returns>True if revocation was successful or if token was already invalid</returns>
    Task<bool> RevokeTokenAsync(string token, string? tokenTypeHint, string clientId, string? reason = null);

    /// <summary>
    /// Validates that the client is authorized to revoke tokens
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="clientSecret">Client Secret</param>
    /// <returns>True if authorized, false otherwise</returns>
    Task<bool> ValidateClientAsync(string clientId, string? clientSecret);

    /// <summary>
    /// Checks if a JWT token is revoked
    /// </summary>
    /// <param name="tokenId">JWT Token ID (jti claim)</param>
    /// <returns>True if revoked, false otherwise</returns>
    Task<bool> IsTokenRevokedAsync(string tokenId);

    /// <summary>
    /// Revokes all tokens for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="reason">Reason for revocation</param>
    Task RevokeAllUserTokensAsync(string userId, string reason);

    /// <summary>
    /// Revokes all tokens for a specific client
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="reason">Reason for revocation</param>
    Task RevokeAllClientTokensAsync(string clientId, string reason);
}