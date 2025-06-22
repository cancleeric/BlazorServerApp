using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SimpleJwtWeb.Models;

namespace SimpleJwtWeb.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetTokenAsync();
    Task<UserInfo?> GetCurrentUserAsync();
    Task<bool> HasRoleAsync(string role);
    event Action<bool>? AuthStateChanged;
}

public class AuthService : IAuthService
{
    private readonly IApiService _apiService;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AuthService> _logger;
    private bool _isAuthenticated = false;
    private UserInfo? _currentUser;
    private string? _currentToken;

    public event Action<bool>? AuthStateChanged;

    public AuthService(IApiService apiService, IJSRuntime jsRuntime, ILogger<AuthService> logger)
    {
        _apiService = apiService;
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<bool> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _apiService.LoginAsync(request);
            
            if (response.Success && !string.IsNullOrEmpty(response.Token))
            {
                // 儲存到 localStorage
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", response.Token);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "userInfo", 
                    System.Text.Json.JsonSerializer.Serialize(response.User));

                // 設置 HTTP 客戶端的認證頭
                _apiService.SetAuthToken(response.Token);

                // 更新內部狀態
                _currentToken = response.Token;
                _currentUser = response.User;
                _isAuthenticated = true;

                // 通知狀態變更
                AuthStateChanged?.Invoke(true);

                _logger.LogInformation("用戶 {Username} 登入成功", request.Username);
                return true;
            }
            else
            {
                _logger.LogWarning("登入失敗: {Message}", response.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登入過程中發生錯誤");
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            // 清除 localStorage
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "userInfo");

            // 清除 HTTP 客戶端的認證頭
            _apiService.ClearAuthToken();

            // 清除內部狀態
            _currentToken = null;
            _currentUser = null;
            _isAuthenticated = false;

            // 通知狀態變更
            AuthStateChanged?.Invoke(false);

            _logger.LogInformation("用戶登出成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登出過程中發生錯誤");
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (_isAuthenticated && _currentToken != null)
        {
            // 檢查 token 是否過期
            if (IsTokenExpired(_currentToken))
            {
                await LogoutAsync();
                return false;
            }
            return true;
        }

        // 從 localStorage 檢查
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            if (!string.IsNullOrEmpty(token) && !IsTokenExpired(token))
            {
                _currentToken = token;
                _apiService.SetAuthToken(token);
                
                var userInfoJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "userInfo");
                if (!string.IsNullOrEmpty(userInfoJson))
                {
                    _currentUser = System.Text.Json.JsonSerializer.Deserialize<UserInfo>(userInfoJson);
                }

                _isAuthenticated = true;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查認證狀態時發生錯誤");
        }

        return false;
    }

    public async Task<string?> GetTokenAsync()
    {
        if (_currentToken != null)
            return _currentToken;

        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        if (_currentUser != null)
            return _currentUser;

        try
        {
            var userInfoJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "userInfo");
            if (!string.IsNullOrEmpty(userInfoJson))
            {
                _currentUser = System.Text.Json.JsonSerializer.Deserialize<UserInfo>(userInfoJson);
                return _currentUser;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取當前用戶信息時發生錯誤");
        }

        return null;
    }

    public async Task<bool> HasRoleAsync(string role)
    {
        var user = await GetCurrentUserAsync();
        return user?.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsTokenExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            return jsonToken.ValidTo <= DateTime.UtcNow;
        }
        catch
        {
            return true; // 如果無法解析，視為過期
        }
    }
}
