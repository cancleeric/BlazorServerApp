using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.ValueObjects;

namespace RedisTestConsole;

/// <summary>
/// Redis 實作的租戶上下文快取服務
/// </summary>
public class RedisTenantContextCache : ITenantContextCache
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisTenantContextCache> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private const string KeyPrefix = "tenant_context:";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

    public RedisTenantContextCache(
        IDistributedCache distributedCache,
        ILogger<RedisTenantContextCache> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// 設定租戶上下文快取
    /// </summary>
    public async Task SetAsync(string key, TenantContext context, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey(key);
            var serializedContext = JsonSerializer.Serialize(context, _jsonOptions);
            
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedContext, options, cancellationToken);
            
            _logger.LogDebug("已設定租戶上下文快取: {Key}, 過期時間: {Expiration}", 
                key, expiration ?? DefaultExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設定租戶上下文快取失敗: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// 取得租戶上下文快取
    /// </summary>
    public async Task<TenantContext?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey(key);
            var serializedContext = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
            
            if (string.IsNullOrEmpty(serializedContext))
            {
                _logger.LogDebug("租戶上下文快取未找到: {Key}", key);
                return null;
            }

            var context = JsonSerializer.Deserialize<TenantContext>(serializedContext, _jsonOptions);
            _logger.LogDebug("已取得租戶上下文快取: {Key}", key);
            
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得租戶上下文快取失敗: {Key}", key);
            return null; // 快取失敗時返回 null，讓應用程式繼續運行
        }
    }

    /// <summary>
    /// 移除租戶上下文快取
    /// </summary>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey(key);
            await _distributedCache.RemoveAsync(cacheKey, cancellationToken);
            
            _logger.LogDebug("已移除租戶上下文快取: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移除租戶上下文快取失敗: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// 檢查快取鍵值是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetCacheKey(key);
            var value = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
            return !string.IsNullOrEmpty(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查租戶上下文快取存在性失敗: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// 延長快取過期時間
    /// </summary>
    public async Task RefreshAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await GetAsync(key, cancellationToken);
            if (context != null)
            {
                await SetAsync(key, context, expiration, cancellationToken);
                _logger.LogDebug("已延長租戶上下文快取過期時間: {Key}, 新過期時間: {Expiration}", key, expiration);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "延長租戶上下文快取過期時間失敗: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// 取得符合模式的快取鍵值列表（簡化實作）
    /// </summary>
    public async Task<IEnumerable<string>> GetKeysAsync(string pattern, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetKeysAsync 方法需要 Redis 連線直接存取，目前返回空列表");
        // 注意：IDistributedCache 介面不支援 KEYS 操作
        // 在生產環境中，建議使用 Redis 的 SCAN 命令或維護鍵值索引
        await Task.CompletedTask;
        return Enumerable.Empty<string>();
    }

    /// <summary>
    /// 批次設定快取
    /// </summary>
    public async Task SetManyAsync(Dictionary<string, TenantContext> items, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var tasks = items.Select(kvp => SetAsync(kvp.Key, kvp.Value, expiration, cancellationToken));
        await Task.WhenAll(tasks);
        
        _logger.LogDebug("已批次設定 {Count} 個租戶上下文快取項目", items.Count);
    }

    /// <summary>
    /// 批次取得快取
    /// </summary>
    public async Task<Dictionary<string, TenantContext?>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var keysList = keys.ToList();
        var tasks = keysList.Select(key => GetAsync(key, cancellationToken));
        var results = await Task.WhenAll(tasks);
        
        var dictionary = new Dictionary<string, TenantContext?>();
        for (int i = 0; i < keysList.Count; i++)
        {
            dictionary[keysList[i]] = results[i];
        }
        
        _logger.LogDebug("已批次取得 {Count} 個租戶上下文快取項目", keysList.Count);
        return dictionary;
    }

    /// <summary>
    /// 產生快取鍵值
    /// </summary>
    private static string GetCacheKey(string key)
    {
        return $"{KeyPrefix}{key}";
    }
}