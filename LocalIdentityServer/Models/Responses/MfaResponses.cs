namespace LocalIdentityServer.Models.Responses;

/// <summary>
/// MFA 狀態回應
/// </summary>
public class MfaStatusResponse
{
    /// <summary>
    /// 是否已啟用 MFA
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 是否需要 MFA 驗證
    /// </summary>
    public bool RequiresMfa { get; set; }

    /// <summary>
    /// 主要 MFA 方法
    /// </summary>
    public string? PrimaryMethod { get; set; }

    /// <summary>
    /// 已啟用的 MFA 方法列表
    /// </summary>
    public List<MfaMethodInfo> EnabledMethods { get; set; } = new();

    /// <summary>
    /// 可用備用碼數量
    /// </summary>
    public int BackupCodesAvailable { get; set; }

    /// <summary>
    /// 總備用碼數量
    /// </summary>
    public int BackupCodesTotal { get; set; }
}

/// <summary>
/// MFA 方法資訊
/// </summary>
public class MfaMethodInfo
{
    /// <summary>
    /// 方法ID
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// MFA 方法類型
    /// </summary>
    public string Method { get; set; } = default!;

    /// <summary>
    /// 設備名稱
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 是否為主要方法
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// 最後使用時間
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// 是否被鎖定
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// 遮蔽的目標 (手機號或Email)
    /// </summary>
    public string? MaskedTarget { get; set; }
}

/// <summary>
/// TOTP 設定回應
/// </summary>
public class TotpSetupResponse
{
    /// <summary>
    /// 設定權杖 (用於確認步驟)
    /// </summary>
    public string SetupToken { get; set; } = default!;

    /// <summary>
    /// TOTP 密鑰 (Base32 編碼)
    /// </summary>
    public string Secret { get; set; } = default!;

    /// <summary>
    /// QR Code URI
    /// </summary>
    public string QrCodeUri { get; set; } = default!;

    /// <summary>
    /// QR Code 圖像 (Base64 編碼)
    /// </summary>
    public string QrCodeImage { get; set; } = default!;

    /// <summary>
    /// 手動輸入密鑰 (格式化)
    /// </summary>
    public string ManualEntryKey { get; set; } = default!;

    /// <summary>
    /// 設定說明
    /// </summary>
    public string Instructions { get; set; } = "請使用 Google Authenticator 或其他 TOTP 應用程式掃描 QR Code，然後輸入 6 位數驗證碼確認設定。";
}

/// <summary>
/// MFA 設定確認回應
/// </summary>
public class MfaSetupConfirmResponse
{
    /// <summary>
    /// 設定是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 備用碼列表
    /// </summary>
    public string[] BackupCodes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 回應訊息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 設定完成時間
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 一般 MFA 設定回應
/// </summary>
public class MfaSetupResponse
{
    /// <summary>
    /// 設定是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 回應訊息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 驗證碼ID (用於 SMS/Email 驗證)
    /// </summary>
    public string? VerificationId { get; set; }

    /// <summary>
    /// 遮蔽的目標 (手機號或Email)
    /// </summary>
    public string? MaskedTarget { get; set; }

    /// <summary>
    /// 驗證碼過期時間
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// MFA 停用回應
/// </summary>
public class MfaDisableResponse
{
    /// <summary>
    /// 停用是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 回應訊息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 是否還有其他啟用的 MFA 方法
    /// </summary>
    public bool HasOtherMethods { get; set; }

    /// <summary>
    /// 停用時間
    /// </summary>
    public DateTime DisabledAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MFA 驗證回應
/// </summary>
public class MfaVerifyResponse
{
    /// <summary>
    /// 驗證是否成功
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 是否被鎖定
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// 剩餘嘗試次數
    /// </summary>
    public int RemainingAttempts { get; set; }

    /// <summary>
    /// 鎖定到期時間
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// 風險評分
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// 回應訊息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 驗證時間
    /// </summary>
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MFA 挑戰回應
/// </summary>
public class MfaChallengeResponse
{
    /// <summary>
    /// 挑戰是否成功產生
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 挑戰ID
    /// </summary>
    public string? ChallengeId { get; set; }

    /// <summary>
    /// 挑戰過期時間
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 遮蔽的目標 (手機號或Email)
    /// </summary>
    public string? MaskedTarget { get; set; }

    /// <summary>
    /// 回應訊息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 挑戰產生時間
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 備用碼回應
/// </summary>
public class BackupCodesResponse
{
    /// <summary>
    /// 產生是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 備用碼列表
    /// </summary>
    public string[] BackupCodes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 過期時間
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 回應訊息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 產生時間
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 批次ID
    /// </summary>
    public string? BatchId { get; set; }
}

/// <summary>
/// 備用碼狀態回應
/// </summary>
public class BackupCodesStatusResponse
{
    /// <summary>
    /// 總產生數量
    /// </summary>
    public int TotalGenerated { get; set; }

    /// <summary>
    /// 可用數量
    /// </summary>
    public int AvailableCodes { get; set; }

    /// <summary>
    /// 已使用數量
    /// </summary>
    public int UsedCodes { get; set; }

    /// <summary>
    /// 最後產生時間
    /// </summary>
    public DateTime? LastGeneratedAt { get; set; }

    /// <summary>
    /// 最後使用時間
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// 過期時間
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 是否有過期的代碼
    /// </summary>
    public bool HasExpiredCodes { get; set; }
}

/// <summary>
/// MFA 審計日誌回應
/// </summary>
public class MfaAuditLogsResponse
{
    /// <summary>
    /// 審計日誌列表
    /// </summary>
    public List<MfaAuditLogInfo> Logs { get; set; } = new();
}

/// <summary>
/// MFA 審計日誌資訊
/// </summary>
public class MfaAuditLogInfo
{
    /// <summary>
    /// 事件類型
    /// </summary>
    public string EventType { get; set; } = default!;

    /// <summary>
    /// MFA 方法
    /// </summary>
    public string Method { get; set; } = default!;

    /// <summary>
    /// 事件結果
    /// </summary>
    public string Result { get; set; } = default!;

    /// <summary>
    /// 事件描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// IP 地址
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// 事件時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 失敗原因
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 風險評分
    /// </summary>
    public int RiskScore { get; set; }
}