using System.Collections.Concurrent;
using LocalIdentityServer.Models;

namespace LocalIdentityServer.Stores;

public interface IRefreshTokenStore
{
    Task StoreAsync(RefreshToken token);
    Task<RefreshToken?> FindAsync(string token);
}

public class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshToken> _tokens = new();

    public Task StoreAsync(RefreshToken token)
    {
        _tokens[token.Token] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindAsync(string token)
    {
        _tokens.TryGetValue(token, out var value);
        return Task.FromResult(value);
    }
}
