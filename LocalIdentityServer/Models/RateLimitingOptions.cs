using System.ComponentModel.DataAnnotations;

namespace LocalIdentityServer.Models;

/// <summary>
/// Rate Limiting 配置選項 - 遵循開放封閉原則 (OCP)
/// 支援多層級限流配置與動態規則調整
/// </summary>
public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// 是否啟用 Rate Limiting
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Redis 連線字串
    /// </summary>
    [Required]
    public string RedisConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Redis Key 前綴
    /// </summary>
    public string RedisKeyPrefix { get; set; } = "rate_limit:";

    /// <summary>
    /// 預設滑動窗口大小 (秒)
    /// </summary>
    [Range(1, 86400)]
    public int DefaultWindowSizeSeconds { get; set; } = 60;

    /// <summary>
    /// 窗口分段數量 (影響精確度)
    /// </summary>
    [Range(2, 100)]
    public int WindowSegments { get; set; } = 10;

    /// <summary>
    /// 限流規則集合
    /// </summary>
    public List<RateLimitRule> Rules { get; set; } = new();

    /// <summary>
    /// 預設回應訊息
    /// </summary>
    public string DefaultErrorMessage { get; set; } = "Rate limit exceeded. Please try again later.";

    /// <summary>
    /// 回應標頭配置
    /// </summary>
    public RateLimitHeaders Headers { get; set; } = new();

    /// <summary>
    /// 監控與日誌配置
    /// </summary>
    public RateLimitMonitoring Monitoring { get; set; } = new();
}

/// <summary>
/// Rate Limiting 規則定義
/// </summary>
public class RateLimitRule
{
    /// <summary>
    /// 規則名稱
    /// </summary>
    [Required]
    public string Name { get; set; } = default!;

    /// <summary>
    /// 端點模式 (支援萬用字元)
    /// </summary>
    [Required]
    public string EndpointPattern { get; set; } = default!;

    /// <summary>
    /// HTTP 方法限制 (空表示所有方法)
    /// </summary>
    public List<string> HttpMethods { get; set; } = new();

    /// <summary>
    /// 限流層級配置
    /// </summary>
    public RateLimitLevels Limits { get; set; } = new();

    /// <summary>
    /// 規則優先級 (數字越小優先級越高)
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// 是否啟用此規則
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 多層級限流配置
/// </summary>
public class RateLimitLevels
{
    /// <summary>
    /// IP 層級限制 (每個時間窗口內的最大請求數)
    /// </summary>
    public RateLimitConfig IpLevel { get; set; } = new();

    /// <summary>
    /// 使用者層級限制
    /// </summary>
    public RateLimitConfig UserLevel { get; set; } = new();

    /// <summary>
    /// 端點層級限制
    /// </summary>
    public RateLimitConfig EndpointLevel { get; set; } = new();
}

/// <summary>
/// 單一層級限流配置
/// </summary>
public class RateLimitConfig
{
    /// <summary>
    /// 時間窗口大小 (秒)
    /// </summary>
    [Range(1, 86400)]
    public int WindowSizeSeconds { get; set; } = 60;

    /// <summary>
    /// 最大請求數量
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxRequests { get; set; } = 100;

    /// <summary>
    /// 是否啟用此層級限制
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 回應標頭配置
/// </summary>
public class RateLimitHeaders
{
    /// <summary>
    /// 是否包含限流資訊標頭
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// 限制數量標頭名稱
    /// </summary>
    public string LimitHeader { get; set; } = "X-RateLimit-Limit";

    /// <summary>
    /// 剩餘數量標頭名稱
    /// </summary>
    public string RemainingHeader { get; set; } = "X-RateLimit-Remaining";

    /// <summary>
    /// 重設時間標頭名稱
    /// </summary>
    public string ResetHeader { get; set; } = "X-RateLimit-Reset";

    /// <summary>
    /// 重試建議標頭名稱
    /// </summary>
    public string RetryAfterHeader { get; set; } = "Retry-After";
}

/// <summary>
/// 監控與日誌配置
/// </summary>
public class RateLimitMonitoring
{
    /// <summary>
    /// 是否記錄到審計日誌
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// 是否記錄正常請求 (非限流)
    /// </summary>
    public bool LogNormalRequests { get; set; } = false;

    /// <summary>
    /// 是否記錄限流事件
    /// </summary>
    public bool LogRateLimitEvents { get; set; } = true;

    /// <summary>
    /// 是否啟用效能監控
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;
}