namespace LocalIdentityServer.Models;

/// <summary>
/// Rate Limiting 檢查結果
/// </summary>
public class RateLimitResult
{
    /// <summary>
    /// 是否允許請求
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// 觸發的限流規則名稱
    /// </summary>
    public string? RuleName { get; set; }

    /// <summary>
    /// 觸發的限流層級
    /// </summary>
    public RateLimitLevel TriggeredLevel { get; set; }

    /// <summary>
    /// 最大請求數
    /// </summary>
    public int Limit { get; set; }

    /// <summary>
    /// 剩餘請求數
    /// </summary>
    public int Remaining { get; set; }

    /// <summary>
    /// 窗口重設時間 (Unix 時間戳)
    /// </summary>
    public long ResetTime { get; set; }

    /// <summary>
    /// 建議重試時間 (秒)
    /// </summary>
    public int? RetryAfterSeconds { get; set; }

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 限流識別資訊 (用於日誌記錄)
    /// </summary>
    public RateLimitIdentifier Identifier { get; set; } = new();

    /// <summary>
    /// 建立允許的結果
    /// </summary>
    public static RateLimitResult Allow(int limit, int remaining, long resetTime, RateLimitIdentifier identifier)
    {
        return new RateLimitResult
        {
            IsAllowed = true,
            Limit = limit,
            Remaining = remaining,
            ResetTime = resetTime,
            Identifier = identifier
        };
    }

    /// <summary>
    /// 建立拒絕的結果
    /// </summary>
    public static RateLimitResult Deny(string ruleName, RateLimitLevel level, int limit, 
        long resetTime, int? retryAfter, string? errorMessage, RateLimitIdentifier identifier)
    {
        return new RateLimitResult
        {
            IsAllowed = false,
            RuleName = ruleName,
            TriggeredLevel = level,
            Limit = limit,
            Remaining = 0,
            ResetTime = resetTime,
            RetryAfterSeconds = retryAfter,
            ErrorMessage = errorMessage,
            Identifier = identifier
        };
    }
}

/// <summary>
/// Rate Limiting 層級枚舉
/// </summary>
public enum RateLimitLevel
{
    /// <summary>
    /// IP 層級限制
    /// </summary>
    IpLevel,

    /// <summary>
    /// 使用者層級限制
    /// </summary>
    UserLevel,

    /// <summary>
    /// 端點層級限制
    /// </summary>
    EndpointLevel
}

/// <summary>
/// Rate Limiting 識別資訊
/// </summary>
public class RateLimitIdentifier
{
    /// <summary>
    /// 客戶端 IP 地址
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// 使用者 ID (如果已認證)
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 客戶端 ID (如果可識別)
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// 租戶 ID (多租戶情況下)
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 端點路徑
    /// </summary>
    public string EndpointPath { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 方法
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// User Agent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 請求時間
    /// </summary>
    public DateTime RequestTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 會話識別碼
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Rate Limiting 統計資訊
/// </summary>
public class RateLimitStatistics
{
    /// <summary>
    /// 規則名稱
    /// </summary>
    public string RuleName { get; set; } = default!;

    /// <summary>
    /// 總請求數
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// 被限流的請求數
    /// </summary>
    public long RateLimitedRequests { get; set; }

    /// <summary>
    /// 限流率 (百分比)
    /// </summary>
    public double RateLimitPercentage => TotalRequests > 0 
        ? (double)RateLimitedRequests / TotalRequests * 100 
        : 0;

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 統計時間範圍 (分鐘)
    /// </summary>
    public int TimeRangeMinutes { get; set; } = 60;
}