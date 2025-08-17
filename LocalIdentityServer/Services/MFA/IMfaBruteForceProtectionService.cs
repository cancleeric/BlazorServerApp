namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// MFA 暴力破解防護服務介面
/// 提供 MFA 相關的安全防護機制，包括異常行為偵測和自動防護
/// 遵循介面隔離原則 (ISP) - 提供專門的暴力破解防護功能
/// </summary>
public interface IMfaBruteForceProtectionService
{
    /// <summary>
    /// 檢查是否為可疑的 MFA 活動
    /// </summary>
    /// <param name="userId">使用者ID</param>
    /// <param name="ipAddress">IP位址</param>
    /// <param name="method">MFA方法</param>
    /// <param name="context">驗證上下文</param>
    /// <returns>可疑活動檢查結果</returns>
    Task<SuspiciousActivityResult> CheckSuspiciousActivityAsync(
        string userId, 
        string ipAddress, 
        string method, 
        MfaVerificationContext context);

    /// <summary>
    /// 記錄 MFA 失敗嘗試
    /// </summary>
    /// <param name="userId">使用者ID</param>
    /// <param name="ipAddress">IP位址</param>
    /// <param name="method">MFA方法</param>
    /// <param name="context">驗證上下文</param>
    /// <returns>記錄結果</returns>
    Task RecordFailedAttemptAsync(
        string userId, 
        string ipAddress, 
        string method, 
        MfaVerificationContext context);

    /// <summary>
    /// 記錄成功的 MFA 驗證
    /// </summary>
    /// <param name="userId">使用者ID</param>
    /// <param name="ipAddress">IP位址</param>
    /// <param name="method">MFA方法</param>
    /// <param name="context">驗證上下文</param>
    /// <returns>記錄結果</returns>
    Task RecordSuccessfulAttemptAsync(
        string userId, 
        string ipAddress, 
        string method, 
        MfaVerificationContext context);

    /// <summary>
    /// 檢查是否需要額外的安全措施
    /// </summary>
    /// <param name="userId">使用者ID</param>
    /// <param name="ipAddress">IP位址</param>
    /// <returns>安全建議</returns>
    Task<SecurityRecommendation> GetSecurityRecommendationAsync(string userId, string ipAddress);

    /// <summary>
    /// 臨時鎖定使用者的 MFA
    /// </summary>
    /// <param name="userId">使用者ID</param>
    /// <param name="durationMinutes">鎖定時長(分鐘)</param>
    /// <param name="reason">鎖定原因</param>
    /// <returns>鎖定結果</returns>
    Task<bool> TemporaryLockUserMfaAsync(string userId, int durationMinutes, string reason);

    /// <summary>
    /// 臨時封鎖 IP 地址
    /// </summary>
    /// <param name="ipAddress">IP位址</param>
    /// <param name="durationMinutes">封鎖時長(分鐘)</param>
    /// <param name="reason">封鎖原因</param>
    /// <returns>封鎖結果</returns>
    Task<bool> TemporaryBlockIpAsync(string ipAddress, int durationMinutes, string reason);

    /// <summary>
    /// 清理過期的防護資料
    /// </summary>
    /// <returns>清理的記錄數量</returns>
    Task<int> CleanupExpiredDataAsync();

    /// <summary>
    /// 取得暴力破解防護統計資料
    /// </summary>
    /// <param name="timeRangeHours">統計時間範圍(小時)</param>
    /// <returns>統計資料</returns>
    Task<BruteForceProtectionStatistics> GetStatisticsAsync(int timeRangeHours = 24);
}

/// <summary>
/// 可疑活動檢查結果
/// </summary>
public class SuspiciousActivityResult
{
    /// <summary>
    /// 是否為可疑活動
    /// </summary>
    public bool IsSuspicious { get; set; }

    /// <summary>
    /// 風險分數 (0-100)
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// 風險因子列表
    /// </summary>
    public List<string> RiskFactors { get; set; } = new();

    /// <summary>
    /// 建議的安全措施
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();

    /// <summary>
    /// 是否應該阻擋請求
    /// </summary>
    public bool ShouldBlock { get; set; }

    /// <summary>
    /// 阻擋原因
    /// </summary>
    public string? BlockReason { get; set; }
}

/// <summary>
/// 安全建議
/// </summary>
public class SecurityRecommendation
{
    /// <summary>
    /// 建議等級
    /// </summary>
    public SecurityLevel Level { get; set; }

    /// <summary>
    /// 建議措施
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// 是否需要立即行動
    /// </summary>
    public bool RequiresImmediateAction { get; set; }

    /// <summary>
    /// 額外資訊
    /// </summary>
    public Dictionary<string, object> AdditionalInfo { get; set; } = new();
}

/// <summary>
/// 安全等級
/// </summary>
public enum SecurityLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// 暴力破解防護統計資料
/// </summary>
public class BruteForceProtectionStatistics
{
    /// <summary>
    /// 總失敗嘗試次數
    /// </summary>
    public long TotalFailedAttempts { get; set; }

    /// <summary>
    /// 被阻擋的 IP 數量
    /// </summary>
    public int BlockedIpCount { get; set; }

    /// <summary>
    /// 被鎖定的使用者數量
    /// </summary>
    public int LockedUserCount { get; set; }

    /// <summary>
    /// 偵測到的可疑活動數量
    /// </summary>
    public int SuspiciousActivityCount { get; set; }

    /// <summary>
    /// 平均風險分數
    /// </summary>
    public double AverageRiskScore { get; set; }

    /// <summary>
    /// 最常見的攻擊模式
    /// </summary>
    public Dictionary<string, int> CommonAttackPatterns { get; set; } = new();

    /// <summary>
    /// 統計時間範圍
    /// </summary>
    public int TimeRangeHours { get; set; }

    /// <summary>
    /// 統計生成時間
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}