namespace CreditMonitoring.Common.Models;

/// <summary>
/// JWT Token 請求模型
/// 用於用戶登入時提供認證資訊
/// </summary>
public class JwtTokenRequest
{
    /// <summary>
    /// 用戶名稱或員工編號
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密碼
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 銀行代碼 (可選)
    /// </summary>
    public string? BankCode { get; set; }

    /// <summary>
    /// 分行代碼 (可選)
    /// </summary>
    public string? BranchCode { get; set; }
}
