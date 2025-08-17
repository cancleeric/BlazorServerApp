using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// MFA 審計服務介面
/// 負責記錄和分析 MFA 相關的安全事件
/// </summary>
public interface IMfaAuditService
{
    /// <summary>
    /// 記錄 MFA 事件
    /// </summary>
    Task LogMfaEventAsync(MfaAuditEvent auditEvent);

    /// <summary>
    /// 取得使用者的 MFA 審計記錄
    /// </summary>
    Task<IEnumerable<MfaAuditLogEntity>> GetUserMfaAuditLogsAsync(string userId, int limit = 50);

    /// <summary>
    /// 取得特定時間範圍內的 MFA 事件
    /// </summary>
    Task<IEnumerable<MfaAuditLogEntity>> GetMfaEventsByTimeRangeAsync(DateTime startTime, DateTime endTime, string? userId = null);

    /// <summary>
    /// 分析可疑的 MFA 活動
    /// </summary>
    Task<IEnumerable<SuspiciousActivity>> AnalyzeSuspiciousActivityAsync(string? userId = null, int hours = 24);

    /// <summary>
    /// 取得 MFA 使用統計
    /// </summary>
    Task<MfaUsageStatistics> GetMfaUsageStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// 取得失敗的 MFA 嘗試 (用於偵測攻擊)
    /// </summary>
    Task<IEnumerable<MfaAuditLogEntity>> GetFailedMfaAttemptsAsync(string? ipAddress = null, int hours = 1);

    /// <summary>
    /// 計算風險評分
    /// </summary>
    Task<int> CalculateRiskScoreAsync(MfaVerificationContext context, string userId);

    /// <summary>
    /// 檢查是否為異常登入
    /// </summary>
    Task<bool> IsAnomalousLoginAsync(string userId, MfaVerificationContext context);

    /// <summary>
    /// 清理舊的審計記錄
    /// </summary>
    Task<int> CleanupOldAuditLogsAsync(int retentionDays = 90);
}

/// <summary>
/// MFA 審計事件
/// </summary>
public class MfaAuditEvent
{
    public string UserId { get; set; } = default!;
    public string? MfaMethodId { get; set; }
    public string EventType { get; set; } = default!;
    public string Method { get; set; } = default!;
    public string Result { get; set; } = default!;
    public string? FailureReason { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ClientId { get; set; }
    public string? SessionId { get; set; }
    public int RiskScore { get; set; }
    public string? GeoLocation { get; set; }
    public string? DeviceFingerprint { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// 可疑活動
/// </summary>
public class SuspiciousActivity
{
    public string UserId { get; set; } = default!;
    public string ActivityType { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int RiskScore { get; set; }
    public string? IpAddress { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
    public int Frequency { get; set; }
    public string[] RelatedEvents { get; set; } = Array.Empty<string>();
}

/// <summary>
/// MFA 使用統計
/// </summary>
public class MfaUsageStatistics
{
    public int TotalMfaUsers { get; set; }
    public int TotpUsers { get; set; }
    public int SmsUsers { get; set; }
    public int EmailUsers { get; set; }
    public int TotalVerifications { get; set; }
    public int SuccessfulVerifications { get; set; }
    public int FailedVerifications { get; set; }
    public int BackupCodeUsage { get; set; }
    public double SuccessRate { get; set; }
    public DateTime? DateRange { get; set; }
    public Dictionary<string, int> MethodUsage { get; set; } = new();
    public Dictionary<string, int> FailureReasons { get; set; } = new();
}

/// <summary>
/// MFA 事件類型常數
/// </summary>
public static class MfaEventTypes
{
    public const string Setup = "Setup";
    public const string Verify = "Verify";
    public const string Disable = "Disable";
    public const string BackupCodeGenerated = "BackupCodeGenerated";
    public const string BackupCodeUsed = "BackupCodeUsed";
    public const string MethodLocked = "MethodLocked";
    public const string MethodUnlocked = "MethodUnlocked";
    public const string Reset = "Reset";
    public const string ChallengeGenerated = "ChallengeGenerated";
}

/// <summary>
/// MFA 結果常數
/// </summary>
public static class MfaResults
{
    public const string Success = "Success";
    public const string Failed = "Failed";
    public const string Blocked = "Blocked";
    public const string Expired = "Expired";
    public const string InvalidCode = "InvalidCode";
    public const string TooManyAttempts = "TooManyAttempts";
    public const string MethodNotFound = "MethodNotFound";
    public const string UserNotFound = "UserNotFound";
}