using BlazorAuthSdk.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.NavigationManager;
using Microsoft.JSInterop;

namespace BlazorAuthSdk.Services;

public interface IAuthService
{
    void InitiateLogin();
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetTokenAsync();
    Task<UserInfo?> GetCurrentUserAsync();
    Task<bool> HasRoleAsync(string role);
    Task HandleLoginCallbackAsync(string token);
}
