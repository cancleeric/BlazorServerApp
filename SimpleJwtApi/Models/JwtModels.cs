namespace SimpleJwtApi.Models;

/// <summary>
/// 登入請求模型
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

/// <summary>
/// 登入回應模型
/// </summary>
public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Message { get; set; }
    public DateTime ExpiresAt { get; set; }
    public UserInfo? User { get; set; }
}

/// <summary>
/// 用戶信息模型
/// </summary>
public class UserInfo
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// API 回應模型
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }
}

/// <summary>
/// 模擬用戶類別
/// </summary>
public class User
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
}