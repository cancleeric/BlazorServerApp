using Microsoft.JSInterop;
using AuthCoreSdk.Models;
using BlazorAuthSdk.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.NavigationManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace BlazorAuthSdk.Services;

public class AuthService : IAuthService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuthClientService _authClientService;
    private readonly JwtAuthenticationStateProvider _authenticationStateProvider;
    private readonly NavigationManager _navigationManager;
    private readonly IConfiguration _configuration;

    public AuthService(IJSRuntime jsRuntime, ILogger<AuthService> logger,
                       IAuthClientService authClientService, AuthenticationStateProvider authenticationStateProvider,
                       NavigationManager navigationManager, IConfiguration configuration)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _authClientService = authClientService;
        _authenticationStateProvider = (JwtAuthenticationStateProvider)authenticationStateProvider;
        _navigationManager = navigationManager;
        _configuration = configuration;
    }

    public void InitiateLogin()
    {
        var authenticationServerBaseUrl = _configuration["AuthenticationServer:BaseUrl"] ?? "https://localhost:7002";
        var redirectUri = _navigationManager.ToAbsoluteUri("/callback").ToString();
        var loginUrl = $"{authenticationServerBaseUrl}/login?returnUrl={Uri.EscapeDataString(redirectUri)}";
        _navigationManager.NavigateTo(loginUrl, forceLoad: true);
    }

    public async Task HandleLoginCallbackAsync(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
            _authenticationStateProvider.MarkUserAsAuthenticated(token);
            _logger.LogInformation("用戶透過回調登入成功");
        }
        else
        {
            _logger.LogWarning("回調登入失敗: 無效的令牌");
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "userInfo");
            _authenticationStateProvider.MarkUserAsLoggedOut();
            _logger.LogInformation("用戶登出成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登出過程中發生錯誤");
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return authState.User.Identity?.IsAuthenticated ?? false;
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _jsRuntime.InvokeVoidAsync<string?>("localStorage.getItem", "authToken");
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            return new UserInfo
            {
                Id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                Username = user.FindFirst(ClaimTypes.Name)?.Value,
                Role = user.FindFirst(ClaimTypes.Role)?.Value,
                Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
            };
        }
        return null;
    }

    public async Task<bool> HasRoleAsync(string role)
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return authState.User.IsInRole(role);
    }
}
