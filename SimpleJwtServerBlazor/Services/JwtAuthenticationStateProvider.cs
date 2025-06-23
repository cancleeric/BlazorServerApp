using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace SimpleJwtServerBlazor.Services
{
    /// <summary>
    /// JWT 驗證狀態提供者（Blazor Server 專用）
    /// 
    /// 功能：
    /// 1. 管理 JWT Token 的 localStorage 儲存與取得
    /// 2. 提供 Blazor 元件用戶登入、登出、驗證狀態
    /// 3. 解析 JWT 取得 Claims
    /// 4. 通知 Blazor 授權狀態變更
    /// </summary>
    public class JwtAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _jsRuntime; // JS 互操作，操作 localStorage
        private readonly HttpClient _httpClient; // 用於 API 請求
        private const string TOKEN_KEY = "jwt_token"; // localStorage 的 Token Key
        private ClaimsPrincipal _currentUser = new(new ClaimsIdentity()); // 當前用戶

        /// <summary>
        /// 建構子，注入 JSRuntime 與 HttpClient
        /// </summary>
        public JwtAuthenticationStateProvider(IJSRuntime jsRuntime, HttpClient httpClient)
        {
            _jsRuntime = jsRuntime;
            _httpClient = httpClient;
        }

        /// <summary>
        /// 取得目前的身份驗證狀態（Blazor 會自動呼叫）
        /// </summary>
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // 從 localStorage 取得 JWT Token
            var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TOKEN_KEY);
            if (string.IsNullOrEmpty(token))
                return new AuthenticationState(_currentUser);

            // 設定 HttpClient 的 Bearer Token
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            // 解析 JWT 取得 Claims
            var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
            _currentUser = new ClaimsPrincipal(identity);
            return new AuthenticationState(_currentUser);
        }

        /// <summary>
        /// 登入，呼叫 API 並儲存 JWT Token
        /// </summary>
        public async Task<bool> LoginAsync(string username, string password)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new { username, password });
            if (!response.IsSuccessStatusCode) return false;
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LoginResult>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.Success == true && !string.IsNullOrEmpty(result.Token))
            {
                // 儲存 Token 到 localStorage
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TOKEN_KEY, result.Token);
                // 通知 Blazor 授權狀態變更
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                return true;
            }
            return false;
        }

        /// <summary>
        /// 登出，移除 Token 並重設狀態
        /// </summary>
        public async Task LogoutAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TOKEN_KEY);
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        }

        /// <summary>
        /// 解析 JWT Token 取得 Claims
        /// </summary>
        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var claims = new List<Claim>();
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            if (keyValuePairs != null)
            {
                foreach (var kvp in keyValuePairs)
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value.ToString() ?? ""));
                }
            }
            return claims;
        }

        /// <summary>
        /// 處理 JWT Base64 字串補齊
        /// </summary>
        private byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }

        /// <summary>
        /// 登入 API 回傳結果模型
        /// </summary>
        public class LoginResult
        {
            public bool Success { get; set; }
            public string? Token { get; set; }
        }
    }
}
