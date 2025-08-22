using LocalIdentityServer.Data.Repositories;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace LocalIdentityServer.Services;

/// <summary>
/// Implementation of OAuth2 Token Revocation Service (RFC 7009)
/// </summary>
public class TokenRevocationService : ITokenRevocationService
{
    private readonly IClientRepository _clientRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenBlacklistRepository _blacklistRepository;
    private readonly IPersistedKeyRepository _keyRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenRevocationService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public TokenRevocationService(
        IClientRepository clientRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenBlacklistRepository blacklistRepository,
        IPersistedKeyRepository keyRepository,
        IConfiguration configuration,
        ILogger<TokenRevocationService> logger)
    {
        _clientRepository = clientRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _blacklistRepository = blacklistRepository;
        _keyRepository = keyRepository;
        _configuration = configuration;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<bool> RevokeTokenAsync(string token, string? tokenTypeHint, string clientId, string? reason = null)
    {
        try
        {
            var revoked = false;

            // Try to revoke as refresh token first if hinted or no hint provided
            if (string.IsNullOrEmpty(tokenTypeHint) || tokenTypeHint == "refresh_token")
            {
                revoked = await RevokeRefreshTokenAsync(token, clientId, reason);
                if (revoked)
                {
                    _logger.LogInformation("Refresh token revoked for client {ClientId}", clientId);
                    return true;
                }
            }

            // Try to revoke as JWT access token
            if (string.IsNullOrEmpty(tokenTypeHint) || tokenTypeHint == "access_token")
            {
                revoked = await RevokeAccessTokenAsync(token, clientId, reason);
                if (revoked)
                {
                    _logger.LogInformation("Access token revoked for client {ClientId}", clientId);
                    return true;
                }
            }

            // Per RFC 7009: "If the server is unable to determine the token type, 
            // it MUST raise an unsupported_token_type error or return an HTTP 400 status code"
            // However, for better UX, we'll just log and return success for invalid tokens
            _logger.LogDebug("Token not found or already invalid for client {ClientId}", clientId);
            return true; // RFC 7009: return success even if token was already invalid
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token revocation for client {ClientId}", clientId);
            return false;
        }
    }

    public async Task<bool> ValidateClientAsync(string clientId, string? clientSecret)
    {
        try
        {
            var client = await _clientRepository.GetByClientIdAsync(clientId);
            if (client == null || !client.IsActive)
            {
                return false;
            }

            // If client secret is provided, verify it
            if (!string.IsNullOrEmpty(clientSecret))
            {
                return BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecret);
            }

            // For public clients, allow revocation without secret
            // You may want to restrict this based on your security requirements
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating client {ClientId} for revocation", clientId);
            return false;
        }
    }

    public async Task<bool> IsTokenRevokedAsync(string tokenId)
    {
        try
        {
            return await _blacklistRepository.IsTokenRevokedAsync(tokenId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if token {TokenId} is revoked", tokenId);
            return false;
        }
    }

    public async Task RevokeAllUserTokensAsync(string userId, string reason)
    {
        try
        {
            // Revoke all refresh tokens for the user
            await _refreshTokenRepository.RevokeByUserAsync(userId, reason);

            // For access tokens, we can't enumerate them directly, but they will be
            // invalidated by their natural expiration. For immediate revocation,
            // you might want to implement a user-based blacklist or shorter token lifetimes
            
            _logger.LogWarning("All tokens revoked for user {UserId}, reason: {Reason}", userId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all tokens for user {UserId}", userId);
            throw;
        }
    }

    public async Task RevokeAllClientTokensAsync(string clientId, string reason)
    {
        try
        {
            // Revoke all refresh tokens for the client
            await _refreshTokenRepository.RevokeByClientAsync(clientId, reason);

            _logger.LogWarning("All tokens revoked for client {ClientId}, reason: {Reason}", clientId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all tokens for client {ClientId}", clientId);
            throw;
        }
    }

    private async Task<bool> RevokeRefreshTokenAsync(string token, string clientId, string? reason)
    {
        try
        {
            var refreshToken = await _refreshTokenRepository.GetByTokenAsync(token);
            
            if (refreshToken == null || refreshToken.IsRevoked)
            {
                return false; // Token not found or already revoked
            }

            // Check if the requesting client has permission to revoke this token
            if (refreshToken.ClientId != clientId)
            {
                _logger.LogWarning("Client {ClientId} attempted to revoke refresh token belonging to {TokenClientId}", 
                    clientId, refreshToken.ClientId);
                return false;
            }

            // Revoke the token and its family (security measure)
            await _refreshTokenRepository.RevokeTokenFamilyAsync(refreshToken.TokenFamily, 
                reason ?? "Token revocation requested");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh token");
            return false;
        }
    }

    private async Task<bool> RevokeAccessTokenAsync(string token, string clientId, string? reason)
    {
        try
        {
            // Parse JWT to extract claims
            var jwtToken = _tokenHandler.ReadJwtToken(token);
            
            // Validate basic token structure
            if (jwtToken == null)
            {
                return false;
            }

            // Extract token ID and other claims
            var tokenId = jwtToken.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
            var audience = jwtToken.Claims.FirstOrDefault(c => c.Type == "aud")?.Value;
            var subject = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            if (string.IsNullOrEmpty(tokenId))
            {
                _logger.LogWarning("JWT token missing jti claim");
                return false;
            }

            // Check if the requesting client has permission to revoke this token
            if (audience != clientId)
            {
                _logger.LogWarning("Client {ClientId} attempted to revoke token with audience {Audience}", 
                    clientId, audience);
                return false;
            }

            // Validate the token signature (to ensure it's not forged)
            if (!await ValidateTokenSignatureAsync(token))
            {
                _logger.LogWarning("Invalid token signature for revocation request");
                return false;
            }

            // Add to blacklist
            var tokenHash = TokenBlacklistRepository.ComputeTokenHash(token);
            await _blacklistRepository.AddToBlacklistAsync(
                tokenId, 
                tokenHash, 
                jwtToken.ValidTo, 
                clientId, 
                subject, 
                reason ?? "Token revocation requested");

            return true;
        }
        catch (SecurityTokenMalformedException)
        {
            _logger.LogDebug("Malformed JWT token provided for revocation");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking access token");
            return false;
        }
    }

    private async Task<bool> ValidateTokenSignatureAsync(string token)
    {
        try
        {
            var key = await _keyRepository.GetPrimarySigningKeyAsync();
            if (key == null)
            {
                return false;
            }

            var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(key.EncryptedKeyData));
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false, // We're not validating audience here
                ValidateLifetime = false, // We're not validating lifetime for revocation
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "https://localhost:5001",
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.Zero
            };

            _tokenHandler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }
}