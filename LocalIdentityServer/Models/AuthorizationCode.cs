namespace LocalIdentityServer.Models;

public class AuthorizationCode
{
    public string Code { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string RedirectUri { get; set; } = default!;
    public string Scope { get; set; } = default!;
    public string Subject { get; set; } = default!; // user id
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
    public string? Nonce { get; set; }
    public DateTime ExpiresAt { get; set; }
}
