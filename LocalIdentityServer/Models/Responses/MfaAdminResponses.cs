namespace LocalIdentityServer.Models.Responses;

/// <summary>
/// MFA 管理員操作回應
/// </summary>
public class MfaAdminResponse
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 回應訊息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 執行的操作
    /// </summary>
    public string ActionPerformed { get; set; } = default!;

    /// <summary>
    /// 目標使用者ID
    /// </summary>
    public string? TargetUserId { get; set; }

    /// <summary>
    /// 執行操作的管理員ID
    /// </summary>
    public string AdminUserId { get; set; } = default!;

    /// <summary>
    /// 操作時間
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 額外的操作詳情
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// 使用者 MFA 狀態管理員檢視
/// </summary>
public class MfaAdminUserStatusResponse
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    public string UserId { get; set; } = default!;

    /// <summary>
    /// 是否已啟用 MFA
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 是否需要 MFA 驗證
    /// </summary>
    public bool RequiresMfa { get; set; }

    /// <summary>
    /// MFA 方法列表 (包含詳細資訊)
    /// </summary>
    public List<MfaAdminMethodInfo> Methods { get; set; } = new();

    /// <summary>
    /// 備用碼狀態詳情
    /// </summary>
    public BackupCodesAdminInfo BackupCodesStatus { get; set; } = default!;

    /// <summary>
    /// 最近活動
    /// </summary>
    public List<MfaAuditLogInfo> RecentActivity { get; set; } = new();

    /// <summary>
    /// 狀態檢索時間
    /// </summary>
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MFA 方法管理員資訊
/// </summary>
public class MfaAdminMethodInfo : MfaMethodInfo
{
    /// <summary>
    /// 鎖定到期時間
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// 失敗嘗試次數
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 手機號碼 (完整，僅管理員可見)
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Email 地址 (完整，僅管理員可見)
    /// </summary>
    public string? EmailAddress { get; set; }
}

/// <summary>
/// 備用碼管理員資訊
/// </summary>
public class BackupCodesAdminInfo : BackupCodesStatusResponse
{
    /// <summary>
    /// 當前批次ID
    /// </summary>
    public string? CurrentBatchId { get; set; }

    /// <summary>
    /// 過期代碼數量
    /// </summary>
    public int ExpiredCodes { get; set; }
}

/// <summary>
/// MFA 統計回應
/// </summary>
public class MfaStatisticsResponse
{
    /// <summary>
    /// 總 MFA 使用者數
    /// </summary>
    public int TotalMfaUsers { get; set; }

    /// <summary>
    /// TOTP 使用者數
    /// </summary>
    public int TotpUsers { get; set; }

    /// <summary>
    /// SMS 使用者數
    /// </summary>
    public int SmsUsers { get; set; }

    /// <summary>
    /// Email 使用者數
    /// </summary>
    public int EmailUsers { get; set; }

    /// <summary>
    /// 總驗證次數
    /// </summary>
    public int TotalVerifications { get; set; }

    /// <summary>
    /// 成功驗證次數
    /// </summary>
    public int SuccessfulVerifications { get; set; }

    /// <summary>
    /// 失敗驗證次數
    /// </summary>
    public int FailedVerifications { get; set; }

    /// <summary>
    /// 備用碼使用次數
    /// </summary>
    public int BackupCodeUsage { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// 方法使用統計
    /// </summary>
    public Dictionary<string, int> MethodUsage { get; set; } = new();

    /// <summary>
    /// 失敗原因統計
    /// </summary>
    public Dictionary<string, int> FailureReasons { get; set; } = new();

    /// <summary>
    /// 統計時間範圍
    /// </summary>
    public object? DateRange { get; set; }

    /// <summary>
    /// 統計產生時間
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 可疑活動回應
/// </summary>
public class SuspiciousActivityResponse
{
    /// <summary>
    /// 可疑活動列表
    /// </summary>
    public List<SuspiciousActivityInfo> Activities { get; set; } = new();

    /// <summary>
    /// 分析時間範圍 (小時)
    /// </summary>
    public int AnalysisTimeRange { get; set; }

    /// <summary>
    /// 產生時間
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// 可疑活動資訊
/// </summary>
public class SuspiciousActivityInfo
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    public string UserId { get; set; } = default!;

    /// <summary>
    /// 活動類型
    /// </summary>
    public string ActivityType { get; set; } = default!;

    /// <summary>
    /// 活動描述
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 風險評分
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// IP 地址
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// 首次發生時間
    /// </summary>
    public DateTime FirstOccurrence { get; set; }

    /// <summary>
    /// 最後發生時間
    /// </summary>
    public DateTime LastOccurrence { get; set; }

    /// <summary>
    /// 發生頻率
    /// </summary>
    public int Frequency { get; set; }

    /// <summary>
    /// 相關事件
    /// </summary>
    public string[] RelatedEvents { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 失敗嘗試回應
/// </summary>
public class FailedAttemptsResponse
{
    /// <summary>
    /// 失敗嘗試列表
    /// </summary>
    public List<FailedAttemptInfo> FailedAttempts { get; set; } = new();

    /// <summary>
    /// 時間範圍 (小時)
    /// </summary>
    public int TimeRange { get; set; }

    /// <summary>
    /// IP 地址篩選
    /// </summary>
    public string? FilteredByIp { get; set; }

    /// <summary>
    /// 總數量
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 產生時間
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 失敗嘗試資訊
/// </summary>
public class FailedAttemptInfo
{
    /// <summary>
    /// 使用者ID
    /// </summary>
    public string UserId { get; set; } = default!;

    /// <summary>
    /// MFA 方法
    /// </summary>
    public string Method { get; set; } = default!;

    /// <summary>
    /// IP 地址
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User Agent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 失敗原因
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 嘗試時間
    /// </summary>
    public DateTime AttemptTime { get; set; }

    /// <summary>
    /// 風險評分
    /// </summary>
    public int RiskScore { get; set; }
}