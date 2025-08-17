using Microsoft.Extensions.Options;
using StackExchange.Redis;
using LocalIdentityServer.Models;

namespace LocalIdentityServer.Services;

/// <summary>
/// Redis 實作的 Rate Limiting 快取服務 - 遵循單一責任原則 (SRP)
/// 負責分散式計數器的儲存與管理
/// </summary>
public class RateLimitingCacheService : IRateLimitingCacheService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly RateLimitingOptions _options;
    private readonly ILogger<RateLimitingCacheService> _logger;

    public RateLimitingCacheService(
        IConnectionMultiplexer redis,
        IOptions<RateLimitingOptions> options,
        ILogger<RateLimitingCacheService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = _redis.GetDatabase();
    }

    public async Task<long> GetCountAsync(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            var value = await _database.StringGetAsync(fullKey);
            return value.HasValue ? (long)value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get count for key: {Key}", key);
            return 0; // 在 Redis 故障時，回傳 0 以避免誤判為限流
        }
    }

    public async Task<long> IncrementAsync(string key, TimeSpan expiry)
    {
        try
        {
            var fullKey = GetFullKey(key);
            var transaction = _database.CreateTransaction();

            // 原子性操作：遞增並設定過期時間
            var incrementTask = transaction.StringIncrementAsync(fullKey);
            var expireTask = transaction.KeyExpireAsync(fullKey, expiry);

            await transaction.ExecuteAsync();

            var newValue = await incrementTask;
            await expireTask;

            return newValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment counter for key: {Key}", key);
            return long.MaxValue; // 在 Redis 故障時，回傳最大值以觸發限流保護
        }
    }

    public async Task SetCountAsync(string key, long value, TimeSpan expiry)
    {
        try
        {
            var fullKey = GetFullKey(key);
            await _database.StringSetAsync(fullKey, value, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set count for key: {Key}, value: {Value}", key, value);
        }
    }

    public async Task DeleteCountAsync(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            await _database.KeyDeleteAsync(fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete key: {Key}", key);
        }
    }

    public async Task<Dictionary<string, long>> GetCountBatchAsync(List<string> keys)
    {
        var result = new Dictionary<string, long>();

        try
        {
            var fullKeys = keys.Select(GetFullKey).ToArray();
            var redisKeys = fullKeys.Select(k => (RedisKey)k).ToArray();
            var values = await _database.StringGetAsync(redisKeys);

            for (int i = 0; i < keys.Count; i++)
            {
                result[keys[i]] = values[i].HasValue ? (long)values[i] : 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get batch counts for {KeyCount} keys", keys.Count);
            
            // 在故障時回傳空字典，讓調用方使用預設值
            foreach (var key in keys)
            {
                result[key] = 0;
            }
        }

        return result;
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            return await _database.KeyExistsAsync(fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence for key: {Key}", key);
            return false;
        }
    }

    public async Task<TimeSpan?> GetTimeToLiveAsync(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            return await _database.KeyTimeToLiveAsync(fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get TTL for key: {Key}", key);
            return null;
        }
    }

    /// <summary>
    /// 建立滑動窗口計數器的批次操作
    /// </summary>
    public async Task<long> IncrementSlidingWindowAsync(string baseKey, int windowSizeSeconds, int segments)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var segmentSize = windowSizeSeconds / segments;
            var currentSegment = now / segmentSize;

            var transaction = _database.CreateTransaction();
            var tasks = new List<Task<long>>();

            // 遞增當前分段
            var currentKey = GetSlidingWindowKey(baseKey, currentSegment);
            var incrementTask = transaction.StringIncrementAsync(currentKey);
            var expireTask = transaction.KeyExpireAsync(currentKey, TimeSpan.FromSeconds(windowSizeSeconds));
            
            tasks.Add(incrementTask);

            await transaction.ExecuteAsync();
            await expireTask;

            // 計算所有有效分段的總和
            var startSegment = currentSegment - segments + 1;
            var sumTasks = new List<Task<RedisValue>>();
            var segmentKeys = new List<RedisKey>();

            for (long segment = startSegment; segment <= currentSegment; segment++)
            {
                var segmentKey = GetSlidingWindowKey(baseKey, segment);
                segmentKeys.Add(segmentKey);
            }

            var values = await _database.StringGetAsync(segmentKeys.ToArray());
            long totalCount = 0;

            foreach (var value in values)
            {
                if (value.HasValue)
                {
                    totalCount += (long)value;
                }
            }

            return totalCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment sliding window for key: {BaseKey}", baseKey);
            return long.MaxValue; // 故障時觸發限流保護
        }
    }

    /// <summary>
    /// 清理過期的滑動窗口分段
    /// </summary>
    public async Task CleanupSlidingWindowAsync(string baseKey, int windowSizeSeconds, int segments)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var segmentSize = windowSizeSeconds / segments;
            var currentSegment = now / segmentSize;
            var cutoffSegment = currentSegment - segments;

            // 刪除過期的分段 (保留一些額外的分段以防時間偏差)
            var pattern = GetSlidingWindowPattern(baseKey);
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            
            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                var keyString = key.ToString();
                var segmentPart = keyString.Split(':').LastOrDefault();
                
                if (long.TryParse(segmentPart, out var segment) && segment < cutoffSegment - 10)
                {
                    await _database.KeyDeleteAsync(key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup sliding window for key: {BaseKey}", baseKey);
        }
    }

    private string GetFullKey(string key)
    {
        return $"{_options.RedisKeyPrefix}{key}";
    }

    private string GetSlidingWindowKey(string baseKey, long segment)
    {
        return GetFullKey($"sw:{baseKey}:{segment}");
    }

    private string GetSlidingWindowPattern(string baseKey)
    {
        return GetFullKey($"sw:{baseKey}:*");
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}

/// <summary>
/// 記憶體實作的 Rate Limiting 快取服務 (開發/測試用)
/// </summary>
public class InMemoryRateLimitingCacheService : IRateLimitingCacheService
{
    private readonly Dictionary<string, (long Value, DateTime Expiry)> _cache = new();
    private readonly object _lock = new();
    private readonly ILogger<InMemoryRateLimitingCacheService> _logger;

    public InMemoryRateLimitingCacheService(ILogger<InMemoryRateLimitingCacheService> logger)
    {
        _logger = logger;
    }

    public Task<long> GetCountAsync(string key)
    {
        lock (_lock)
        {
            CleanupExpired();
            
            if (_cache.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow)
            {
                return Task.FromResult(entry.Value);
            }
            
            return Task.FromResult(0L);
        }
    }

    public Task<long> IncrementAsync(string key, TimeSpan expiry)
    {
        lock (_lock)
        {
            CleanupExpired();
            
            var expiryTime = DateTime.UtcNow.Add(expiry);
            
            if (_cache.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow)
            {
                var newValue = entry.Value + 1;
                _cache[key] = (newValue, entry.Expiry);
                return Task.FromResult(newValue);
            }
            else
            {
                _cache[key] = (1L, expiryTime);
                return Task.FromResult(1L);
            }
        }
    }

    public Task SetCountAsync(string key, long value, TimeSpan expiry)
    {
        lock (_lock)
        {
            var expiryTime = DateTime.UtcNow.Add(expiry);
            _cache[key] = (value, expiryTime);
            return Task.CompletedTask;
        }
    }

    public Task DeleteCountAsync(string key)
    {
        lock (_lock)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }

    public Task<Dictionary<string, long>> GetCountBatchAsync(List<string> keys)
    {
        var result = new Dictionary<string, long>();
        
        lock (_lock)
        {
            CleanupExpired();
            
            foreach (var key in keys)
            {
                if (_cache.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow)
                {
                    result[key] = entry.Value;
                }
                else
                {
                    result[key] = 0;
                }
            }
        }
        
        return Task.FromResult(result);
    }

    public Task<bool> ExistsAsync(string key)
    {
        lock (_lock)
        {
            CleanupExpired();
            return Task.FromResult(_cache.ContainsKey(key) && _cache[key].Expiry > DateTime.UtcNow);
        }
    }

    public Task<TimeSpan?> GetTimeToLiveAsync(string key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow)
            {
                return Task.FromResult<TimeSpan?>(entry.Expiry - DateTime.UtcNow);
            }
            
            return Task.FromResult<TimeSpan?>(null);
        }
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache.Where(kv => kv.Value.Expiry <= now).Select(kv => kv.Key).ToList();
        
        foreach (var key in expiredKeys)
        {
            _cache.Remove(key);
        }
    }
}