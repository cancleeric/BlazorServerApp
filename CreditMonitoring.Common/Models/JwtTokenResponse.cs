namespace CreditMonitoring.Common.Models;

/// <summary>
/// JWT Token 回應模型
/// API 驗證成功後返回給客戶端的 Token 資訊
/// </summary>
public class JwtTokenResponse
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 錯誤訊息（當 Success 為 false 時）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// JWT Token（相容性屬性）
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// JWT Access Token
    /// 用於 API 認證的主要 Token
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token 類型，通常為 "Bearer"
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Token 過期時間 (秒)
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Token 過期日期時間
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Refresh Token (可選)
    /// 用於刷新 Access Token
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// 用戶基本資訊
    /// </summary>
    public UserInfo? User { get; set; }

    /// <summary>
    /// JWT 用戶信息（相容性屬性）
    /// </summary>
    public JwtUserInfo? UserInfo { get; set; }
}

/// <summary>
/// 用戶基本資訊
/// </summary>
public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// JWT 用戶信息模型（從 JwtTokenService 移動過來）
/// </summary>
public class JwtUserInfo
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string UserType { get; set; } = "";
    public string? BankCode { get; set; }
    public string? BranchCode { get; set; }
    public List<string> Roles { get; set; } = new();
}
