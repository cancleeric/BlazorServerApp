using LocalIdentityServer.Models;

namespace LocalIdentityServer.Services;

/// <summary>
/// Rate Limiting 服務介面 - 遵循依賴反轉原則 (DIP)
/// 負責實作多層級 Rate Limiting 與 DDoS 防護機制
/// </summary>
public interface IRateLimitingService
{
    /// <summary>
    /// 檢查請求是否符合限流規則
    /// </summary>
    /// <param name="identifier">請求識別資訊</param>
    /// <returns>限流檢查結果</returns>
    Task<RateLimitResult> CheckRateLimitAsync(RateLimitIdentifier identifier);

    /// <summary>
    /// 批次檢查多個識別碼的限流狀態
    /// </summary>
    /// <param name="identifiers">識別碼列表</param>
    /// <returns>限流檢查結果列表</returns>
    Task<List<RateLimitResult>> CheckRateLimitBatchAsync(List<RateLimitIdentifier> identifiers);

    /// <summary>
    /// 記錄請求 (更新計數器)
    /// </summary>
    /// <param name="identifier">請求識別資訊</param>
    /// <param name="allowed">請求是否被允許</param>
    Task RecordRequestAsync(RateLimitIdentifier identifier, bool allowed);

    /// <summary>
    /// 取得指定識別碼的當前限流狀態
    /// </summary>
    /// <param name="identifier">請求識別資訊</param>
    /// <returns>限流狀態</returns>
    Task<RateLimitStatus> GetCurrentStatusAsync(RateLimitIdentifier identifier);

    /// <summary>
    /// 重設指定識別碼的限流計數器
    /// </summary>
    /// <param name="identifier">請求識別資訊</param>
    /// <param name="reason">重設原因</param>
    Task ResetCounterAsync(RateLimitIdentifier identifier, string reason);

    /// <summary>
    /// 取得限流統計資訊
    /// </summary>
    /// <param name="timeRangeMinutes">統計時間範圍 (分鐘)</param>
    /// <returns>統計資訊列表</returns>
    Task<List<RateLimitStatistics>> GetStatisticsAsync(int timeRangeMinutes = 60);

    /// <summary>
    /// 動態新增或更新限流規則
    /// </summary>
    /// <param name="rule">限流規則</param>
    Task AddOrUpdateRuleAsync(RateLimitRule rule);

    /// <summary>
    /// 移除限流規則
    /// </summary>
    /// <param name="ruleName">規則名稱</param>
    Task RemoveRuleAsync(string ruleName);

    /// <summary>
    /// 取得目前所有生效的限流規則
    /// </summary>
    /// <returns>規則列表</returns>
    Task<List<RateLimitRule>> GetActiveRulesAsync();

    /// <summary>
    /// 暫時禁止指定識別碼
    /// </summary>
    /// <param name="identifier">識別資訊</param>
    /// <param name="durationMinutes">禁止時長 (分鐘)</param>
    /// <param name="reason">禁止原因</param>
    Task BlacklistTemporaryAsync(RateLimitIdentifier identifier, int durationMinutes, string reason);

    /// <summary>
    /// 將識別碼加入白名單 (永久豁免)
    /// </summary>
    /// <param name="identifier">識別資訊</param>
    /// <param name="reason">加入原因</param>
    Task WhitelistAsync(RateLimitIdentifier identifier, string reason);

    /// <summary>
    /// 清理過期的計數器和統計資料
    /// </summary>
    Task CleanupExpiredDataAsync();
}

/// <summary>
/// Rate Limiting 當前狀態
/// </summary>
public class RateLimitStatus
{
    /// <summary>
    /// 是否在黑名單中
    /// </summary>
    public bool IsBlacklisted { get; set; }

    /// <summary>
    /// 是否在白名單中
    /// </summary>
    public bool IsWhitelisted { get; set; }

    /// <summary>
    /// 各層級的當前計數
    /// </summary>
    public Dictionary<RateLimitLevel, int> CurrentCounts { get; set; } = new();

    /// <summary>
    /// 各層級的限制數量
    /// </summary>
    public Dictionary<RateLimitLevel, int> Limits { get; set; } = new();

    /// <summary>
    /// 窗口重設時間
    /// </summary>
    public Dictionary<RateLimitLevel, DateTime> ResetTimes { get; set; } = new();

    /// <summary>
    /// 黑名單到期時間 (如果適用)
    /// </summary>
    public DateTime? BlacklistExpiresAt { get; set; }

    /// <summary>
    /// 白名單建立時間 (如果適用)
    /// </summary>
    public DateTime? WhitelistCreatedAt { get; set; }
}

/// <summary>
/// Rate Limiting 快取服務介面
/// </summary>
public interface IRateLimitingCacheService
{
    /// <summary>
    /// 取得計數器值
    /// </summary>
    /// <param name="key">快取鍵</param>
    /// <returns>計數值</returns>
    Task<long> GetCountAsync(string key);

    /// <summary>
    /// 遞增計數器
    /// </summary>
    /// <param name="key">快取鍵</param>
    /// <param name="expiry">過期時間</param>
    /// <returns>遞增後的值</returns>
    Task<long> IncrementAsync(string key, TimeSpan expiry);

    /// <summary>
    /// 設定計數器值
    /// </summary>
    /// <param name="key">快取鍵</param>
    /// <param name="value">值</param>
    /// <param name="expiry">過期時間</param>
    Task SetCountAsync(string key, long value, TimeSpan expiry);

    /// <summary>
    /// 刪除計數器
    /// </summary>
    /// <param name="key">快取鍵</param>
    Task DeleteCountAsync(string key);

    /// <summary>
    /// 批次取得計數器值
    /// </summary>
    /// <param name="keys">快取鍵列表</param>
    /// <returns>計數值字典</returns>
    Task<Dictionary<string, long>> GetCountBatchAsync(List<string> keys);

    /// <summary>
    /// 檢查鍵是否存在
    /// </summary>
    /// <param name="key">快取鍵</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// 取得鍵的剩餘過期時間
    /// </summary>
    /// <param name="key">快取鍵</param>
    /// <returns>剩餘時間</returns>
    Task<TimeSpan?> GetTimeToLiveAsync(string key);
}