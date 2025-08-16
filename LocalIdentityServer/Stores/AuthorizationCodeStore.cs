using System.Collections.Concurrent;
using LocalIdentityServer.Models;

namespace LocalIdentityServer.Stores;

public interface IAuthorizationCodeStore
{
    Task StoreAsync(AuthorizationCode code);
    Task<AuthorizationCode?> TakeAsync(string code); // remove + return
}

public class InMemoryAuthorizationCodeStore : IAuthorizationCodeStore
{
    private readonly ConcurrentDictionary<string, AuthorizationCode> _codes = new();

    public Task StoreAsync(AuthorizationCode code)
    {
        _codes[code.Code] = code;
        return Task.CompletedTask;
    }

    public Task<AuthorizationCode?> TakeAsync(string code)
    {
        _codes.TryRemove(code, out var value);
        return Task.FromResult(value);
    }
}
