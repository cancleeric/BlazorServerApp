using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SimpleJwtWeb.Models;

namespace SimpleJwtWeb.Services;

public interface IApiService
{
    Task<string> GetPublicDataAsync();
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<string> GetProtectedDataAsync();
    Task<string> GetAdminDataAsync();
    Task<string> GetUserDataAsync();
    void SetAuthToken(string token);
    void ClearAuthToken();
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;
    private readonly string _baseUrl;

    public ApiService(HttpClient httpClient, IConfiguration configuration, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001";
        _httpClient.BaseAddress = new Uri(_baseUrl);
    }

    public async Task<string> GetPublicDataAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/auth/public");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "調用公開 API 失敗");
            return $"錯誤: {ex.Message}";
        }
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/auth/login", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return loginResponse ?? new LoginResponse { Success = false, Message = "解析回應失敗" };
            }
            else
            {
                return new LoginResponse { Success = false, Message = $"登入失敗: {responseContent}" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登入 API 調用失敗");
            return new LoginResponse { Success = false, Message = $"系統錯誤: {ex.Message}" };
        }
    }    public async Task<string> GetProtectedDataAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/auth/me");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            return "錯誤: 未授權，請先登入";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "調用受保護 API 失敗");
            return $"錯誤: {ex.Message}";
        }
    }

    public async Task<string> GetAdminDataAsync()
    {        try
        {
            var response = await _httpClient.GetAsync("/api/auth/admin-only");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            return "錯誤: 未授權，請先登入";
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("403"))
        {
            return "錯誤: 權限不足，需要管理員權限";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "調用管理員 API 失敗");
            return $"錯誤: {ex.Message}";
        }
    }    public async Task<string> GetUserDataAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/auth/me");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            return "錯誤: 未授權，請先登入";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "調用用戶 API 失敗");
            return $"錯誤: {ex.Message}";
        }
    }

    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearAuthToken()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }
}
