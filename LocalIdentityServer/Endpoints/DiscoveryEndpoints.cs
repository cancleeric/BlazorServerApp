using Microsoft.IdentityModel.Tokens;

namespace LocalIdentityServer.Endpoints;

/// <summary>
/// Discovery 端點 - 遵循單一責任原則 (SRP)
/// 負責提供 OIDC Discovery Document 和 JWKS
/// </summary>
public static class DiscoveryEndpoints
{
    /// <summary>
    /// 配置 Discovery 相關端點
    /// </summary>
    public static void MapDiscoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // OIDC Discovery Document
        endpoints.MapGet("/.well-known/openid-configuration", GetDiscoveryDocument)
            .WithName("GetDiscoveryDocument")
            .WithDisplayName("OIDC Discovery Document")
            .Produces(200)
            .WithTags("Discovery");

        // JWKS (JSON Web Key Set)
        endpoints.MapGet("/.well-known/jwks.json", GetJwks)
            .WithName("GetJwks")
            .WithDisplayName("JSON Web Key Set")
            .Produces(200)
            .WithTags("Discovery");
    }

    /// <summary>
    /// 取得 OIDC Discovery Document
    /// </summary>
    private static object GetDiscoveryDocument(RsaSecurityKey key, IConfiguration config)
    {
        // 從配置讀取 issuer，提供彈性
        var issuer = config["IdentityServer:Issuer"] ?? "https://localhost:5055";
        
        return new
        {
            issuer = issuer,
            authorization_endpoint = $"{issuer}/connect/authorize",
            token_endpoint = $"{issuer}/connect/token",
            userinfo_endpoint = $"{issuer}/connect/userinfo",
            end_session_endpoint = $"{issuer}/connect/logout",
            jwks_uri = $"{issuer}/.well-known/jwks.json",
            response_types_supported = new[] { "code", "token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            scopes_supported = new[] { "openid", "profile", "email", "api.read", "api.write" },
            token_endpoint_auth_methods_supported = new[] { "client_secret_post", "client_secret_basic" },
            claims_supported = new[] { "sub", "name", "email", "role", "username" },
            grant_types_supported = new[] { "authorization_code", "refresh_token", "client_credentials" },
            response_modes_supported = new[] { "query", "fragment", "form_post" },
            code_challenge_methods_supported = new[] { "plain", "S256" }
        };
    }

    /// <summary>
    /// 取得 JWKS (JSON Web Key Set)
    /// </summary>
    private static object GetJwks(RsaSecurityKey key)
    {
        if (key?.Rsa == null)
            throw new InvalidOperationException("RSA key is not available");

        try
        {
            var parameters = key.Rsa.ExportParameters(false);
            
            return new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA",
                        use = "sig",
                        kid = key.KeyId,
                        alg = "RS256",
                        e = Base64UrlEncoder.Encode(parameters.Exponent!),
                        n = Base64UrlEncoder.Encode(parameters.Modulus!)
                    }
                }
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to export RSA key parameters", ex);
        }
    }
}
