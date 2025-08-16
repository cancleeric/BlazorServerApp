using System.Security.Claims;
using System.Security.Cryptography;
using LocalIdentityServer.Stores;
using LocalIdentityServer.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace LocalIdentityServer.Services;

public class DefaultTokenService : ITokenService
{
    private readonly SigningCredentials _signingCredentials;
    private readonly RsaSecurityKey _key;
    private readonly IEnumerable<TestClient> _clients;

    public DefaultTokenService(SigningCredentials signingCredentials, RsaSecurityKey key, IEnumerable<TestClient> clients)
    {
        _signingCredentials = signingCredentials;
        _key = key;
        _clients = clients;
    }

    public Task<TokenIssueResult> IssueAsync(TokenIssueRequest request)
    {
        var now = DateTime.UtcNow;
        var scopes = (request.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>()).Distinct().ToArray();
        var tokenScopes = string.Join(' ', scopes);

        var accessClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.User.Id),
            new("username", request.User.UserName),
            new(JwtRegisteredClaimNames.Email, request.User.Email),
            new("scope", tokenScopes)
        };
        accessClaims.AddRange(request.User.Roles.Select(r => new Claim("role", r)));

        var access = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "https://localhost:5055",
            audience: "api_resource_a",
            claims: accessClaims,
            notBefore: now,
            expires: now.AddMinutes(30),
            signingCredentials: _signingCredentials
        );
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var accessToken = handler.WriteToken(access);

        string? idToken = null;
        if (scopes.Contains("openid"))
        {
            var idClaims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, request.User.Id),
                new("name", request.User.UserName),
                new("email", request.User.Email)
            };
            if (!string.IsNullOrEmpty(request.Nonce)) idClaims.Add(new Claim("nonce", request.Nonce));
            var idJwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: "https://localhost:5055",
                audience: request.ClientId,
                claims: idClaims,
                notBefore: now,
                expires: now.AddMinutes(30),
                signingCredentials: _signingCredentials
            );
            idToken = handler.WriteToken(idJwt);
        }

        var refresh = new RefreshToken
        {
            Token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32)),
            ClientId = request.ClientId,
            Subject = request.User.Id,
            Username = request.User.UserName,
            Email = request.User.Email,
            Scope = request.Scope,
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        };

        return Task.FromResult(new TokenIssueResult
        {
            AccessToken = accessToken,
            IdToken = idToken,
            Scope = tokenScopes,
            ExpiresIn = 1800,
            RefreshToken = refresh
        });
    }
}
