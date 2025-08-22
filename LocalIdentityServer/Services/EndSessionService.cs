using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Models.Requests;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace LocalIdentityServer.Services;

/// <summary>
/// Implementation of OIDC End Session Service
/// </summary>
public class EndSessionService : IEndSessionService
{
    private readonly IClientRepository _clientRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenRevocationService _tokenRevocationService;
    private readonly IPersistedKeyRepository _keyRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EndSessionService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public EndSessionService(
        IClientRepository clientRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenRevocationService tokenRevocationService,
        IPersistedKeyRepository keyRepository,
        IConfiguration configuration,
        ILogger<EndSessionService> logger)
    {
        _clientRepository = clientRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenRevocationService = tokenRevocationService;
        _keyRepository = keyRepository;
        _configuration = configuration;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<EndSessionValidationResult> ValidateEndSessionRequestAsync(EndSessionRequest request)
    {
        var result = new EndSessionValidationResult();

        try
        {
            // If id_token_hint is provided, validate it
            if (!string.IsNullOrEmpty(request.IdTokenHint))
            {
                var tokenValidation = await ValidateIdTokenHintAsync(request.IdTokenHint);
                if (!tokenValidation.IsValid)
                {
                    result.ErrorMessage = tokenValidation.ErrorMessage;
                    return result;
                }

                result.ClientId = tokenValidation.ClientId;
                result.Subject = tokenValidation.Subject;
            }
            else if (!string.IsNullOrEmpty(request.ClientId))
            {
                // If no id_token_hint but client_id is provided, validate client
                var client = await _clientRepository.GetByClientIdAsync(request.ClientId);
                if (client == null || !client.IsActive)
                {
                    result.ErrorMessage = "Invalid client_id";
                    return result;
                }
                result.ClientId = request.ClientId;
            }

            // Get valid post-logout redirect URIs for the client
            if (!string.IsNullOrEmpty(result.ClientId))
            {
                var client = await _clientRepository.GetByClientIdAsync(result.ClientId);
                if (client != null)
                {
                    // Parse post-logout redirect URIs (assuming they're stored in a field)
                    // For now, we'll use the regular redirect URIs as a fallback
                    var redirectUris = client.RedirectUris?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    result.ValidPostLogoutRedirectUris.AddRange(redirectUris);
                }
            }

            // Validate post_logout_redirect_uri if provided
            if (!string.IsNullOrEmpty(request.PostLogoutRedirectUri))
            {
                if (result.ValidPostLogoutRedirectUris.Any())
                {
                    var isValidRedirectUri = result.ValidPostLogoutRedirectUris
                        .Any(uri => string.Equals(uri.Trim(), request.PostLogoutRedirectUri.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (!isValidRedirectUri)
                    {
                        result.ErrorMessage = "Invalid post_logout_redirect_uri";
                        return result;
                    }
                }
                else if (!string.IsNullOrEmpty(result.ClientId))
                {
                    // If client is identified but no valid redirect URIs, reject
                    result.ErrorMessage = "post_logout_redirect_uri not allowed for this client";
                    return result;
                }
            }

            result.IsValid = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating end session request");
            result.ErrorMessage = "Internal error during validation";
            return result;
        }
    }

    public async Task<EndSessionResult> ProcessLogoutAsync(EndSessionRequest request, string? userId)
    {
        var result = new EndSessionResult();

        try
        {
            // Validate the request first
            var validation = await ValidateEndSessionRequestAsync(request);
            if (!validation.IsValid)
            {
                result.ErrorMessage = validation.ErrorMessage;
                return result;
            }

            // Determine user to logout
            var userToLogout = userId ?? validation.Subject;

            // Revoke all tokens for the user if identified
            if (!string.IsNullOrEmpty(userToLogout))
            {
                await _tokenRevocationService.RevokeAllUserTokensAsync(userToLogout, "User logout");
                _logger.LogInformation("All tokens revoked for user {UserId} during logout", userToLogout);
            }

            // Revoke tokens for specific client if identified (additional security)
            if (!string.IsNullOrEmpty(validation.ClientId))
            {
                await _tokenRevocationService.RevokeAllClientTokensAsync(validation.ClientId, "Client logout");
                _logger.LogInformation("All tokens revoked for client {ClientId} during logout", validation.ClientId);
            }

            // Determine post-logout redirect URI
            result.PostLogoutRedirectUri = await GetPostLogoutRedirectUriAsync(request, validation.ClientId);
            result.State = request.State;
            result.Success = true;

            _logger.LogInformation("Logout processed successfully for user {UserId}, client {ClientId}", 
                userToLogout, validation.ClientId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing logout");
            result.ErrorMessage = "Internal error during logout processing";
            return result;
        }
    }

    public async Task<string?> GetPostLogoutRedirectUriAsync(EndSessionRequest request, string? clientId)
    {
        try
        {
            // If post_logout_redirect_uri is provided and validated, use it
            if (!string.IsNullOrEmpty(request.PostLogoutRedirectUri))
            {
                return request.PostLogoutRedirectUri;
            }

            // If no redirect URI specified, check client's default post-logout URI
            if (!string.IsNullOrEmpty(clientId))
            {
                var client = await _clientRepository.GetByClientIdAsync(clientId);
                if (client != null)
                {
                    // For now, return the first redirect URI as default
                    // In a real implementation, you might have a dedicated PostLogoutRedirectUris field
                    var redirectUris = client.RedirectUris?.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    return redirectUris?.FirstOrDefault()?.Trim();
                }
            }

            // No redirect specified or available
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining post-logout redirect URI");
            return null;
        }
    }

    private async Task<(bool IsValid, string? ErrorMessage, string? ClientId, string? Subject)> ValidateIdTokenHintAsync(string idToken)
    {
        try
        {
            // Parse the JWT token without validation first to extract claims
            var token = _tokenHandler.ReadJwtToken(idToken);
            
            // Extract basic claims
            var clientId = token.Claims.FirstOrDefault(c => c.Type == "aud")?.Value;
            var subject = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            // Validate token signature and claims
            var key = await _keyRepository.GetPrimarySigningKeyAsync();
            if (key == null)
            {
                return (false, "Unable to validate token signature", null, null);
            }

            var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(key.EncryptedKeyData));
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // For logout, we allow expired tokens
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "https://localhost:5001",
                ValidAudiences = !string.IsNullOrEmpty(clientId) ? new[] { clientId } : null,
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.FromMinutes(5) // Allow some clock skew
            };

            // Validate the token
            _tokenHandler.ValidateToken(idToken, validationParameters, out _);

            return (true, null, clientId, subject);
        }
        catch (SecurityTokenExpiredException)
        {
            // For logout, expired tokens are allowed - extract claims manually
            try
            {
                var token = _tokenHandler.ReadJwtToken(idToken);
                var clientId = token.Claims.FirstOrDefault(c => c.Type == "aud")?.Value;
                var subject = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                
                _logger.LogDebug("Accepting expired ID token for logout");
                return (true, null, clientId, subject);
            }
            catch
            {
                return (false, "Invalid ID token format", null, null);
            }
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogWarning(ex, "Invalid ID token provided for logout");
            return (false, "Invalid ID token", null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating ID token hint");
            return (false, "Error validating ID token", null, null);
        }
    }
}