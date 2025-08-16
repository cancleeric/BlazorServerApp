namespace LocalIdentityServer.Models;

public class RefreshToken
{
    public string Token { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Scope { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
}
