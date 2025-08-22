using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Models.Responses;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LocalIdentityServer.Services;

/// <summary>
/// Implementation of OAuth2 Token Introspection Service (RFC 7662)
/// </summary>
public class TokenIntrospectionService : ITokenIntrospectionService
{
    private readonly IClientRepository _clientRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPersistedKeyRepository _keyRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenIntrospectionService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public TokenIntrospectionService(
        IClientRepository clientRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPersistedKeyRepository keyRepository,
        IConfiguration configuration,
        ILogger<TokenIntrospectionService> logger)
    {
        _clientRepository = clientRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _keyRepository = keyRepository;
        _configuration = configuration;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<IntrospectionResponse> IntrospectTokenAsync(string token, string? tokenTypeHint, string clientId)
    {
        try
        {
            // Try to introspect as JWT access token first
            if (string.IsNullOrEmpty(tokenTypeHint) || tokenTypeHint == "access_token")
            {
                var jwtResponse = await IntrospectJwtTokenAsync(token, clientId);
                if (jwtResponse.Active)
                {
                    return jwtResponse;
                }
            }

            // Try to introspect as refresh token
            if (string.IsNullOrEmpty(tokenTypeHint) || tokenTypeHint == "refresh_token")
            {
                var refreshResponse = await IntrospectRefreshTokenAsync(token, clientId);
                if (refreshResponse.Active)
                {
                    return refreshResponse;
                }
            }

            // Token not found or inactive
            return new IntrospectionResponse { Active = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token introspection for client {ClientId}", clientId);
            return new IntrospectionResponse { Active = false };
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

            // For public clients, allow introspection without secret
            // You may want to restrict this based on your security requirements
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating client {ClientId} for introspection", clientId);
            return false;
        }
    }

    private async Task<IntrospectionResponse> IntrospectJwtTokenAsync(string token, string clientId)
    {
        try
        {
            // Validate JWT token signature and structure
            var key = await _keyRepository.GetPrimarySigningKeyAsync();
            if (key == null)
            {
                return new IntrospectionResponse { Active = false };
            }

            var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(key.EncryptedKeyData));
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "https://localhost:5001",
                ValidAudiences = new[] { clientId }, // Allow introspecting client to be audience
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            if (validatedToken is JwtSecurityToken jwtToken)
            {
                return CreateIntrospectionResponseFromJwt(jwtToken, principal);
            }

            return new IntrospectionResponse { Active = false };
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogDebug("JWT token expired during introspection");
            return new IntrospectionResponse { Active = false };
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.LogDebug(ex, "JWT token validation failed during introspection");
            return new IntrospectionResponse { Active = false };
        }
    }

    private async Task<IntrospectionResponse> IntrospectRefreshTokenAsync(string token, string clientId)
    {
        try
        {
            var refreshToken = await _refreshTokenRepository.GetByTokenAsync(token);
            
            if (refreshToken == null || 
                refreshToken.IsUsed || 
                refreshToken.IsRevoked ||
                refreshToken.ExpiresAt <= DateTime.UtcNow)
            {
                return new IntrospectionResponse { Active = false };
            }

            // Check if the requesting client has permission to introspect this token
            // Either it's the token owner or has introspection permissions
            if (refreshToken.ClientId != clientId)
            {
                // You might want to implement more sophisticated authorization here
                _logger.LogWarning("Client {ClientId} attempted to introspect token belonging to {TokenClientId}", 
                    clientId, refreshToken.ClientId);
                return new IntrospectionResponse { Active = false };
            }

            return new IntrospectionResponse
            {
                Active = true,
                ClientId = refreshToken.ClientId,
                Username = refreshToken.Username,
                Sub = refreshToken.Subject,
                Scope = refreshToken.Scope,
                Exp = new DateTimeOffset(refreshToken.ExpiresAt).ToUnixTimeSeconds(),
                Iat = new DateTimeOffset(refreshToken.CreatedAt).ToUnixTimeSeconds(),
                TokenType = "refresh_token"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error introspecting refresh token");
            return new IntrospectionResponse { Active = false };
        }
    }

    private IntrospectionResponse CreateIntrospectionResponseFromJwt(JwtSecurityToken jwtToken, ClaimsPrincipal principal)
    {
        var response = new IntrospectionResponse
        {
            Active = true,
            TokenType = "Bearer"
        };

        // Extract standard claims
        if (jwtToken.Claims.FirstOrDefault(c => c.Type == "client_id")?.Value is string clientId)
        {
            response.ClientId = clientId;
        }

        if (jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value is string sub)
        {
            response.Sub = sub;
        }

        if (jwtToken.Claims.FirstOrDefault(c => c.Type == "username")?.Value is string username)
        {
            response.Username = username;
        }

        if (jwtToken.Claims.FirstOrDefault(c => c.Type == "scope")?.Value is string scope)
        {
            response.Scope = scope;
        }

        if (jwtToken.Claims.FirstOrDefault(c => c.Type == "jti")?.Value is string jti)
        {
            response.Jti = jti;
        }

        // Set timestamps
        if (jwtToken.ValidTo != DateTime.MinValue)
        {
            response.Exp = new DateTimeOffset(jwtToken.ValidTo).ToUnixTimeSeconds();
        }

        if (jwtToken.ValidFrom != DateTime.MinValue)
        {
            response.Nbf = new DateTimeOffset(jwtToken.ValidFrom).ToUnixTimeSeconds();
        }

        if (jwtToken.IssuedAt != DateTime.MinValue)
        {
            response.Iat = new DateTimeOffset(jwtToken.IssuedAt).ToUnixTimeSeconds();
        }

        response.Iss = jwtToken.Issuer;
        response.Aud = string.Join(" ", jwtToken.Audiences);

        return response;
    }
}