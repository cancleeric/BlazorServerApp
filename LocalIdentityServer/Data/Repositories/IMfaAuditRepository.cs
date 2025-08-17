using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// MFA 審計日誌儲存庫介面
/// 負責 MFA 審計日誌的資料存取操作
/// </summary>
public interface IMfaAuditRepository
{
    /// <summary>
    /// 建立新的審計日誌
    /// </summary>
    Task<MfaAuditLogEntity> CreateAsync(MfaAuditLogEntity auditLog);

    /// <summary>
    /// 根據 ID 取得審計日誌
    /// </summary>
    Task<MfaAuditLogEntity?> GetByIdAsync(string id);

    /// <summary>
    /// 取得使用者的審計日誌
    /// </summary>
    Task<IEnumerable<MfaAuditLogEntity>> GetUserAuditLogsAsync(string userId, int limit = 50, int offset = 0);

    /// <summary>
    /// 根據時間範圍取得審計日誌
    /// </summary>
    Task<IEnumerable<MfaAuditLogEntity>> GetAuditLogsByTimeRangeAsync(
        DateTime startTime, 
        DateTime endTime, 
        string? userId = null,
        string? eventType = null,
        string? result = null,
        int limit = 1000,
        int offset = 0);

    /// <summary>
    /// 根據 IP 地址取得審計日誌
    /// </summary>
    Task<IEnumerable<MfaAuditLogEntity>> GetAuditLogsByIpAddressAsync(
        string ipAddress, 
        DateTime? startTime = null, 
        DateTime? endTime = null,
        int limit = 100);

    /// <summary>
    /// 取得失敗的 MFA 嘗試
    /// </summary>
    Task<IEnumerable<MfaAuditLogEntity>> GetFailedMfaAttemptsAsync(
        string? userId = null,
        string? ipAddress = null,
        DateTime? startTime = null,
        int limit = 100);

    /// <summary>
    /// 取得可疑活動
    /// </summary>
    Task<IEnumerable<SuspiciousActivityResult>> GetSuspiciousActivitiesAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        int minRiskScore = 50,
        int limit = 100);

    /// <summary>
    /// 取得 MFA 使用統計
    /// </summary>
    Task<MfaUsageStatistics> GetMfaUsageStatisticsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// 取得事件類型統計
    /// </summary>
    Task<Dictionary<string, int>> GetEventTypeStatisticsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// 取得方法使用統計
    /// </summary>
    Task<Dictionary<string, int>> GetMethodUsageStatisticsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// 取得失敗原因統計
    /// </summary>
    Task<Dictionary<string, int>> GetFailureReasonStatisticsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// 檢查是否為異常登入
    /// </summary>
    Task<bool> IsAnomalousLoginAsync(string userId, string ipAddress, string userAgent);

    /// <summary>
    /// 計算使用者的風險評分
    /// </summary>
    Task<int> CalculateUserRiskScoreAsync(string userId, string ipAddress, string userAgent);

    /// <summary>
    /// 清理舊的審計日誌
    /// </summary>
    Task<int> CleanupOldAuditLogsAsync(DateTime beforeDate);

    /// <summary>
    /// 取得審計日誌總數
    /// </summary>
    Task<long> GetTotalAuditLogsCountAsync();

    /// <summary>
    /// 匯出審計日誌
    /// </summary>
    Task<IEnumerable<MfaAuditLogEntity>> ExportAuditLogsAsync(
        DateTime startDate,
        DateTime endDate,
        string? userId = null);
}

/// <summary>
/// 可疑活動結果
/// </summary>
public class SuspiciousActivityResult
{
    public string UserId { get; set; } = default!;
    public string IpAddress { get; set; } = default!;
    public string ActivityType { get; set; } = default!;
    public int RiskScore { get; set; }
    public int EventCount { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
    public string[] EventTypes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// MFA 使用統計 (用於儲存庫)
/// </summary>
public class MfaUsageStatistics
{
    public int TotalEvents { get; set; }
    public int SuccessfulVerifications { get; set; }
    public int FailedVerifications { get; set; }
    public int UniqueUsers { get; set; }
    public double SuccessRate { get; set; }
    public Dictionary<string, int> MethodUsage { get; set; } = new();
    public Dictionary<string, int> EventTypes { get; set; } = new();
    public Dictionary<string, int> FailureReasons { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}