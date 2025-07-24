using BlazorAuthSdk.Models;

namespace BlazorAuthSdk.Services
{
    public interface IAuthClientService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        UserInfo? GetUserInfoFromToken(string token);
        bool IsTokenExpired(string token);
        JwtTokenDetails? GetTokenDetails(string token);
    }
}
