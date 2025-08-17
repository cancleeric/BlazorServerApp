using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Infrastructure.Caching;

/// <summary>
/// Redis 實作的分散式會話管理服務
/// </summary>
public class RedisDistributedSessionService : IDistributedSessionService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisDistributedSessionService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private const string SessionKeyPrefix = "session:";
    private const string UserSessionsKeyPrefix = "user_sessions:";
    private const string SessionDataKeyPrefix = "session_data:";
    private static readonly TimeSpan DefaultSessionExpiration = TimeSpan.FromHours(8);

    public RedisDistributedSessionService(
        IDistributedCache distributedCache,
        ILogger<RedisDistributedSessionService> logger)
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
    /// 建立會話
    /// </summary>
    public async Task CreateSessionAsync(string sessionId, Guid userId, Guid tenantId, 
        Dictionary<string, object>? metadata = null, TimeSpan? expiration = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionExpiration = expiration ?? DefaultSessionExpiration;
            var now = DateTime.UtcNow;
            
            var sessionInfo = new SessionInfo
            {
                SessionId = sessionId,
                UserId = userId,
                TenantId = tenantId,
                CreatedAt = now,
                LastActivityAt = now,
                ExpiresAt = now.Add(sessionExpiration),
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            // 儲存會話資訊
            var sessionKey = GetSessionKey(sessionId);
            var serializedSession = JsonSerializer.Serialize(sessionInfo, _jsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = sessionExpiration
            };

            await _distributedCache.SetStringAsync(sessionKey, serializedSession, options, cancellationToken);

            // 維護使用者會話列表
            await AddToUserSessionsAsync(userId, sessionId, sessionExpiration, cancellationToken);
            
            _logger.LogInformation("已建立會話: {SessionId} for User: {UserId}, Tenant: {TenantId}", 
                sessionId, userId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立會話失敗: {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// 取得會話資訊
    /// </summary>
    public async Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionKey = GetSessionKey(sessionId);
            var serializedSession = await _distributedCache.GetStringAsync(sessionKey, cancellationToken);
            
            if (string.IsNullOrEmpty(serializedSession))
            {
                return null;
            }

            var sessionInfo = JsonSerializer.Deserialize<SessionInfo>(serializedSession, _jsonOptions);
            
            // 檢查會話是否過期
            if (sessionInfo?.IsExpired == true)
            {
                await DestroySessionAsync(sessionId, cancellationToken);
                return null;
            }

            return sessionInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得會話資訊失敗: {SessionId}", sessionId);
            return null;
        }
    }

    /// <summary>
    /// 更新會話最後活動時間
    /// </summary>
    public async Task RefreshSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionInfo = await GetSessionAsync(sessionId, cancellationToken);
            if (sessionInfo == null)
            {
                return;
            }

            sessionInfo.LastActivityAt = DateTime.UtcNow;
            
            var sessionKey = GetSessionKey(sessionId);
            var serializedSession = JsonSerializer.Serialize(sessionInfo, _jsonOptions);
            var remainingTime = sessionInfo.ExpiresAt.Subtract(DateTime.UtcNow);
            
            if (remainingTime > TimeSpan.Zero)
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = remainingTime
                };

                await _distributedCache.SetStringAsync(sessionKey, serializedSession, options, cancellationToken);
                _logger.LogDebug("已更新會話活動時間: {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新會話活動時間失敗: {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// 設定會話資料
    /// </summary>
    public async Task SetSessionDataAsync(string sessionId, string key, object value, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionInfo = await GetSessionAsync(sessionId, cancellationToken);
            if (sessionInfo == null)
            {
                throw new InvalidOperationException($"會話不存在: {sessionId}");
            }

            var dataKey = GetSessionDataKey(sessionId, key);
            var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
            var remainingTime = sessionInfo.ExpiresAt.Subtract(DateTime.UtcNow);
            
            if (remainingTime > TimeSpan.Zero)
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = remainingTime
                };

                await _distributedCache.SetStringAsync(dataKey, serializedValue, options, cancellationToken);
                await RefreshSessionAsync(sessionId, cancellationToken);
                
                _logger.LogDebug("已設定會話資料: {SessionId}.{Key}", sessionId, key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設定會話資料失敗: {SessionId}.{Key}", sessionId, key);
            throw;
        }
    }

    /// <summary>
    /// 取得會話資料
    /// </summary>
    public async Task<T?> GetSessionDataAsync<T>(string sessionId, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionInfo = await GetSessionAsync(sessionId, cancellationToken);
            if (sessionInfo == null)
            {
                return default;
            }

            var dataKey = GetSessionDataKey(sessionId, key);
            var serializedValue = await _distributedCache.GetStringAsync(dataKey, cancellationToken);
            
            if (string.IsNullOrEmpty(serializedValue))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(serializedValue, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得會話資料失敗: {SessionId}.{Key}", sessionId, key);
            return default;
        }
    }

    /// <summary>
    /// 移除會話資料
    /// </summary>
    public async Task RemoveSessionDataAsync(string sessionId, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var dataKey = GetSessionDataKey(sessionId, key);
            await _distributedCache.RemoveAsync(dataKey, cancellationToken);
            
            _logger.LogDebug("已移除會話資料: {SessionId}.{Key}", sessionId, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移除會話資料失敗: {SessionId}.{Key}", sessionId, key);
            throw;
        }
    }

    /// <summary>
    /// 銷毀會話
    /// </summary>
    public async Task DestroySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionInfo = await GetSessionAsync(sessionId, cancellationToken);
            if (sessionInfo != null)
            {
                // 從使用者會話列表中移除
                await RemoveFromUserSessionsAsync(sessionInfo.UserId, sessionId, cancellationToken);
            }

            // 移除會話資訊
            var sessionKey = GetSessionKey(sessionId);
            await _distributedCache.RemoveAsync(sessionKey, cancellationToken);
            
            _logger.LogInformation("已銷毀會話: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "銷毀會話失敗: {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// 取得使用者的所有會話（簡化實作）
    /// </summary>
    public async Task<IEnumerable<SessionInfo>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var userSessionsKey = GetUserSessionsKey(userId);
            var serializedSessions = await _distributedCache.GetStringAsync(userSessionsKey, cancellationToken);
            
            if (string.IsNullOrEmpty(serializedSessions))
            {
                return Enumerable.Empty<SessionInfo>();
            }

            var sessionIds = JsonSerializer.Deserialize<List<string>>(serializedSessions, _jsonOptions) ?? new List<string>();
            var sessions = new List<SessionInfo>();

            foreach (var sessionId in sessionIds)
            {
                var session = await GetSessionAsync(sessionId, cancellationToken);
                if (session != null)
                {
                    sessions.Add(session);
                }
            }

            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得使用者會話失敗: {UserId}", userId);
            return Enumerable.Empty<SessionInfo>();
        }
    }

    /// <summary>
    /// 銷毀使用者的所有會話
    /// </summary>
    public async Task DestroyUserSessionsAsync(Guid userId, string? excludeSessionId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessions = await GetUserSessionsAsync(userId, cancellationToken);
            var destroyTasks = sessions
                .Where(s => s.SessionId != excludeSessionId)
                .Select(s => DestroySessionAsync(s.SessionId, cancellationToken));

            await Task.WhenAll(destroyTasks);
            
            _logger.LogInformation("已銷毀使用者所有會話: {UserId}, 排除: {ExcludeSessionId}", 
                userId, excludeSessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "銷毀使用者會話失敗: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// 清理過期會話
    /// </summary>
    public async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始清理過期會話（Redis 自動過期機制）");
        // Redis 會自動清理過期的鍵值，這裡主要是記錄日誌
        await Task.CompletedTask;
    }

    /// <summary>
    /// 取得活躍會話統計
    /// </summary>
    public async Task<SessionStats> GetSessionStatsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        // 注意：這是簡化實作，生產環境建議使用 Redis 的專用統計機制
        var stats = new SessionStats
        {
            TotalSessions = 0,
            ActiveSessions = 0,
            ExpiredSessions = 0,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogWarning("GetSessionStatsAsync 是簡化實作，需要 Redis 連線直接存取以提供準確統計");
        await Task.CompletedTask;
        
        return stats;
    }

    #region Private Methods

    private static string GetSessionKey(string sessionId) => $"{SessionKeyPrefix}{sessionId}";
    private static string GetUserSessionsKey(Guid userId) => $"{UserSessionsKeyPrefix}{userId}";
    private static string GetSessionDataKey(string sessionId, string key) => $"{SessionDataKeyPrefix}{sessionId}:{key}";

    private async Task AddToUserSessionsAsync(Guid userId, string sessionId, TimeSpan expiration, CancellationToken cancellationToken)
    {
        try
        {
            var userSessionsKey = GetUserSessionsKey(userId);
            var serializedSessions = await _distributedCache.GetStringAsync(userSessionsKey, cancellationToken);
            
            var sessionIds = string.IsNullOrEmpty(serializedSessions) 
                ? new List<string>() 
                : JsonSerializer.Deserialize<List<string>>(serializedSessions, _jsonOptions) ?? new List<string>();

            if (!sessionIds.Contains(sessionId))
            {
                sessionIds.Add(sessionId);
                
                var updatedSessions = JsonSerializer.Serialize(sessionIds, _jsonOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration
                };

                await _distributedCache.SetStringAsync(userSessionsKey, updatedSessions, options, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "新增使用者會話列表失敗: {UserId}.{SessionId}", userId, sessionId);
        }
    }

    private async Task RemoveFromUserSessionsAsync(Guid userId, string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var userSessionsKey = GetUserSessionsKey(userId);
            var serializedSessions = await _distributedCache.GetStringAsync(userSessionsKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(serializedSessions))
            {
                var sessionIds = JsonSerializer.Deserialize<List<string>>(serializedSessions, _jsonOptions) ?? new List<string>();
                
                if (sessionIds.Remove(sessionId))
                {
                    var updatedSessions = JsonSerializer.Serialize(sessionIds, _jsonOptions);
                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) // 延長使用者會話列表的過期時間
                    };

                    await _distributedCache.SetStringAsync(userSessionsKey, updatedSessions, options, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "從使用者會話列表移除失敗: {UserId}.{SessionId}", userId, sessionId);
        }
    }

    #endregion
}