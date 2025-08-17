using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// MFA 主要服務介面 - 遵循介面隔離原則 (ISP)
/// 負責協調各種 MFA 方法的操作
/// </summary>
public interface IMfaService
{
    /// <summary>
    /// 檢查使用者是否已啟用 MFA
    /// </summary>
    Task<bool> IsMfaEnabledAsync(string userId);

    /// <summary>
    /// 檢查使用者是否需要完成 MFA 驗證
    /// </summary>
    Task<bool> RequiresMfaVerificationAsync(string userId);

    /// <summary>
    /// 取得使用者的主要 MFA 方法
    /// </summary>
    Task<UserMfaEntity?> GetPrimaryMfaMethodAsync(string userId);

    /// <summary>
    /// 取得使用者的所有 MFA 方法
    /// </summary>
    Task<IEnumerable<UserMfaEntity>> GetUserMfaMethodsAsync(string userId);

    /// <summary>
    /// 啟用使用者的 MFA
    /// </summary>
    Task<bool> EnableMfaAsync(string userId, string method, string? deviceName = null);

    /// <summary>
    /// 停用使用者的 MFA
    /// </summary>
    Task<bool> DisableMfaAsync(string userId, string method);

    /// <summary>
    /// 驗證 MFA 代碼
    /// </summary>
    Task<MfaVerificationResult> VerifyMfaCodeAsync(string userId, string method, string code, MfaVerificationContext context);

    /// <summary>
    /// 產生 MFA 挑戰 (發送 SMS/Email)
    /// </summary>
    Task<MfaChallengeResult> GenerateMfaChallengeAsync(string userId, string method, MfaVerificationContext context);

    /// <summary>
    /// 重設使用者的 MFA 設定
    /// </summary>
    Task<bool> ResetMfaAsync(string userId, string adminUserId, string reason);

    /// <summary>
    /// 檢查 MFA 方法是否被鎖定
    /// </summary>
    Task<bool> IsMfaMethodLockedAsync(string userId, string method);

    /// <summary>
    /// 解鎖 MFA 方法
    /// </summary>
    Task<bool> UnlockMfaMethodAsync(string userId, string method, string adminUserId);
}

/// <summary>
/// MFA 驗證結果
/// </summary>
public class MfaVerificationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FailureReason { get; set; }
    public bool IsLocked { get; set; }
    public int RemainingAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public int RiskScore { get; set; }
}

/// <summary>
/// MFA 挑戰結果
/// </summary>
public class MfaChallengeResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ChallengeId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? DeliveryMethod { get; set; }
    public string? MaskedTarget { get; set; } // 遮蔽的手機號或 Email
}

/// <summary>
/// MFA 驗證上下文
/// </summary>
public class MfaVerificationContext
{
    public string IpAddress { get; set; } = default!;
    public string UserAgent { get; set; } = default!;
    public string? ClientId { get; set; }
    public string? SessionId { get; set; }
    public string? DeviceFingerprint { get; set; }
    public string? GeoLocation { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}