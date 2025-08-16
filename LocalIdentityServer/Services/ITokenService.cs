namespace LocalIdentityServer.Services;

using System.Security.Claims;
using LocalIdentityServer.Stores;
using LocalIdentityServer.Models;

public interface ITokenService
{
    Task<TokenIssueResult> IssueAsync(TokenIssueRequest request);
}

public class TokenIssueRequest
{
    public required TestUser User { get; set; }
    public required string ClientId { get; set; }
    public required string Scope { get; set; }
    public string? Nonce { get; set; }
}

public class TokenIssueResult
{
    public required string AccessToken { get; set; }
    public string? IdToken { get; set; }
    public required string Scope { get; set; }
    public required int ExpiresIn { get; set; }
    public RefreshToken? RefreshToken { get; set; }
}
