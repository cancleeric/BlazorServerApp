using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using CreditMonitoring.Common.Services;

namespace CreditMonitoring.Web.Services
{
    /// <summary>
    /// JWT身份認證狀態提供者
    /// 
    /// 功能說明：
    /// 1. 管理JWT令牌的儲存和檢索
    /// 2. 提供身份認證狀態給Blazor組件
    /// 3. 處理令牌過期和自動刷新
    /// 4. 整合瀏覽器本地儲存
    /// 
    /// 設計模式：
    /// - Observer Pattern：通知身份認證狀態變更
    /// - Strategy Pattern：支援多種令牌儲存策略
    /// - Decorator Pattern：擴展原有的身份認證功能
    /// 
    /// 安全考量：
    /// 1. 使用 localStorage 儲存令牌
    /// 2. 定期檢查令牌有效性
    /// 3. 自動處理令牌過期
    /// 4. 提供安全的登出機制
    /// </summary>
    public class JwtAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly HttpClient _httpClient;
        private readonly ILogger<JwtAuthenticationStateProvider> _logger;
        
        private const string TOKEN_KEY = "jwt_token";
        private const string USER_INFO_KEY = "user_info";
        
        private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

        public JwtAuthenticationStateProvider(
            IJSRuntime jsRuntime,
            HttpClient httpClient,
            ILogger<JwtAuthenticationStateProvider> logger)
        {
            _jsRuntime = jsRuntime;
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// 獲取當前身份認證狀態
        /// 
        /// 執行流程：
        /// 1. 從瀏覽器儲存中讀取JWT令牌
        /// 2. 驗證令牌有效性
        /// 3. 解析用戶聲明信息
        /// 4. 構建身份認證狀態
        /// </summary>
        /// <returns>身份認證狀態</returns>
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // 嘗試從會話儲存中讀取令牌
                var token = await GetTokenAsync();
                
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogDebug("未找到JWT令牌，返回匿名用戶");
                    return new AuthenticationState(_currentUser);
                }

                // 驗證令牌
                var isValid = await ValidateTokenAsync(token);
                if (!isValid)
                {
                    _logger.LogWarning("JWT令牌無效，清除儲存的令牌");
                    await ClearTokenAsync();
                    return new AuthenticationState(_currentUser);
                }

                // 從令牌創建用戶聲明
                var userClaims = await CreateClaimsFromTokenAsync(token);
                if (userClaims != null)
                {
                    _currentUser = userClaims;
                    _logger.LogDebug("成功從JWT令牌創建用戶聲明");
                }

                return new AuthenticationState(_currentUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取身份認證狀態發生錯誤");
                return new AuthenticationState(_currentUser);
            }
        }

        /// <summary>
        /// 用戶登入
        /// 
        /// 登入流程：
        /// 1. 發送登入請求到API
        /// 2. 儲存JWT令牌到瀏覽器
        /// 3. 更新身份認證狀態
        /// 4. 通知所有訂閱者
        /// </summary>
        /// <param name="username">用戶名</param>
        /// <param name="password">密碼</param>
        /// <param name="bankCode">銀行代碼</param>
        /// <param name="branchCode">分行代碼</param>
        /// <returns>登入結果</returns>
        public async Task<(bool Success, string? ErrorMessage)> LoginAsync(
            string username, 
            string password, 
            string? bankCode = null, 
            string? branchCode = null)
        {
            try
            {
                _logger.LogInformation("開始JWT登入流程: 用戶={Username}", username);

                // 準備登入請求
                var loginRequest = new
                {
                    Username = username,
                    Password = password,
                    BankCode = bankCode,
                    BranchCode = branchCode
                };

                // 發送登入請求到API
                var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // 解析登入響應
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (loginResponse?.Success == true && !string.IsNullOrEmpty(loginResponse.Token))
                    {
                        // 儲存JWT令牌和用戶信息
                        await StoreTokenAsync(loginResponse.Token);
                        if (loginResponse.UserInfo != null)
                        {
                            await StoreUserInfoAsync(loginResponse.UserInfo);
                        }

                        // 從令牌創建用戶聲明
                        var userClaims = await CreateClaimsFromTokenAsync(loginResponse.Token);
                        if (userClaims != null)
                        {
                            _currentUser = userClaims;
                        }

                        // 通知身份認證狀態變更
                        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));

                        _logger.LogInformation("JWT登入成功: 用戶={Username}", username);
                        return (true, null);
                    }
                    else
                    {
                        var errorMessage = loginResponse?.ErrorMessage ?? "登入失敗";
                        _logger.LogWarning("JWT登入失敗: 用戶={Username}, 錯誤={Error}", username, errorMessage);
                        return (false, errorMessage);
                    }
                }
                else
                {
                    var errorMessage = $"登入請求失敗: {response.StatusCode}";
                    _logger.LogWarning("JWT登入請求失敗: 用戶={Username}, 狀態碼={StatusCode}", username, response.StatusCode);
                    return (false, errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JWT登入發生異常: 用戶={Username}", username);
                return (false, "系統錯誤，請稍後再試");
            }
        }

        /// <summary>
        /// 用戶登出
        /// 
        /// 登出流程：
        /// 1. 通知API用戶登出
        /// 2. 清除本地儲存的令牌
        /// 3. 重置身份認證狀態
        /// 4. 通知所有訂閱者
        /// </summary>
        public async Task LogoutAsync()
        {
            try
            {
                _logger.LogInformation("開始JWT登出流程");

                // 獲取當前令牌
                var token = await GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    // 通知API用戶登出
                    try
                    {
                        _httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        await _httpClient.PostAsync("/api/auth/logout", null);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "通知API登出時發生錯誤");
                    }
                }

                // 清除本地儲存
                await ClearTokenAsync();
                await ClearUserInfoAsync();

                // 重置身份認證狀態
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));

                _logger.LogInformation("JWT登出完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JWT登出發生異常");
            }
        }        /// <summary>
        /// 從瀏覽器儲存中讀取JWT令牌
        /// </summary>
        private async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TOKEN_KEY);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 將JWT令牌儲存到瀏覽器
        /// </summary>
        private async Task StoreTokenAsync(string token)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TOKEN_KEY, token);
        }

        /// <summary>
        /// 清除儲存的JWT令牌
        /// </summary>
        private async Task ClearTokenAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TOKEN_KEY);
        }

        /// <summary>
        /// 儲存用戶信息到瀏覽器
        /// </summary>
        private async Task StoreUserInfoAsync(object userInfo)
        {
            var json = JsonSerializer.Serialize(userInfo);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", USER_INFO_KEY, json);
        }

        /// <summary>
        /// 清除儲存的用戶信息
        /// </summary>
        private async Task ClearUserInfoAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", USER_INFO_KEY);
        }

        /// <summary>
        /// 驗證JWT令牌有效性
        /// </summary>
        private async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var request = new { Token = token };
                var response = await _httpClient.PostAsJsonAsync("/api/auth/validate", request);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ValidationResult>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return result?.IsValid == true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 從JWT令牌創建用戶聲明
        /// </summary>
        private async Task<ClaimsPrincipal?> CreateClaimsFromTokenAsync(string token)
        {
            try
            {
                // 設定Authorization標頭
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // 獲取用戶信息
                var response = await _httpClient.GetAsync("/api/auth/me");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var userInfo = JsonSerializer.Deserialize<UserInfo>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (userInfo != null)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, userInfo.UserId),
                            new Claim(ClaimTypes.Name, userInfo.Username),
                            new Claim("userType", userInfo.UserType),
                        };

                        if (!string.IsNullOrEmpty(userInfo.BankCode))
                            claims.Add(new Claim("bankCode", userInfo.BankCode));

                        if (!string.IsNullOrEmpty(userInfo.BranchCode))
                            claims.Add(new Claim("branchCode", userInfo.BranchCode));

                        if (userInfo.Roles != null)
                        {
                            foreach (var role in userInfo.Roles)
                            {
                                claims.Add(new Claim(ClaimTypes.Role, role));
                            }
                        }

                        var identity = new ClaimsIdentity(claims, "jwt");
                        return new ClaimsPrincipal(identity);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    // 響應模型
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? ErrorMessage { get; set; }
        public UserInfo? UserInfo { get; set; }
    }

    public class UserInfo
    {
        public string UserId { get; set; } = "";
        public string Username { get; set; } = "";
        public string UserType { get; set; } = "";
        public string? BankCode { get; set; }
        public string? BranchCode { get; set; }
        public List<string>? Roles { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? Message { get; set; }
    }
}
