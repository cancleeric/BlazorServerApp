using Microsoft.Extensions.Options;
using LocalIdentityServer.Models;
using System.Text.RegularExpressions;

namespace LocalIdentityServer.Services;

/// <summary>
/// Rate Limiting 服務實作 - 遵循 SOLID 原則
/// 實作多層級限流、Sliding Window 演算法與安全防護機制
/// </summary>
public class RateLimitingService : IRateLimitingService
{
    private readonly IRateLimitingCacheService _cache;
    private readonly RateLimitingOptions _options;
    private readonly ILogger<RateLimitingService> _logger;
    private readonly List<RateLimitRule> _rules;

    public RateLimitingService(
        IRateLimitingCacheService cache,
        IOptions<RateLimitingOptions> options,
        ILogger<RateLimitingService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rules = _options.Rules ?? new List<RateLimitRule>();

        InitializeDefaultRules();
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(RateLimitIdentifier identifier)
    {
        if (!_options.Enabled)
        {
            return RateLimitResult.Allow(int.MaxValue, int.MaxValue, 
                DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), identifier);
        }

        try
        {
            // 檢查白名單
            if (await IsWhitelistedAsync(identifier))
            {
                return RateLimitResult.Allow(int.MaxValue, int.MaxValue, 
                    DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), identifier);
            }

            // 檢查黑名單
            if (await IsBlacklistedAsync(identifier))
            {
                return RateLimitResult.Deny("blacklist", RateLimitLevel.IpLevel, 0, 
                    DateTimeOffset.UtcNow.AddMinutes(60).ToUnixTimeSeconds(), 3600, 
                    "IP address is temporarily blocked", identifier);
            }

            // 找到匹配的規則
            var matchedRule = FindMatchingRule(identifier);
            if (matchedRule == null)
            {
                _logger.LogDebug("No matching rule found for endpoint: {Endpoint}", identifier.EndpointPath);
                return RateLimitResult.Allow(int.MaxValue, int.MaxValue, 
                    DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), identifier);
            }

            // 檢查各層級限制
            var result = await CheckAllLevelsAsync(identifier, matchedRule);
            
            // 記錄結果
            await RecordRequestAsync(identifier, result.IsAllowed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for {IP} on {Endpoint}", 
                identifier.IpAddress, identifier.EndpointPath);
            
            // 錯誤時允許請求，避免服務中斷
            return RateLimitResult.Allow(int.MaxValue, int.MaxValue, 
                DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), identifier);
        }
    }

    public async Task<List<RateLimitResult>> CheckRateLimitBatchAsync(List<RateLimitIdentifier> identifiers)
    {
        var results = new List<RateLimitResult>();
        
        // 平行處理批次檢查以提升效能
        var tasks = identifiers.Select(CheckRateLimitAsync).ToArray();
        var batchResults = await Task.WhenAll(tasks);
        
        results.AddRange(batchResults);
        return results;
    }

    public async Task RecordRequestAsync(RateLimitIdentifier identifier, bool allowed)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var statisticsKey = GetStatisticsKey(identifier.EndpointPath, timestamp);
            
            // 記錄統計資料
            if (allowed)
            {
                await _cache.IncrementAsync($"stats:allowed:{statisticsKey}", TimeSpan.FromHours(24));
            }
            else
            {
                await _cache.IncrementAsync($"stats:denied:{statisticsKey}", TimeSpan.FromHours(24));
            }

            // 記錄審計日誌
            if (_options.Monitoring.EnableAuditLogging)
            {
                _logger.LogInformation("Rate limit check: IP={IP}, User={User}, Endpoint={Endpoint}, Allowed={Allowed}",
                    identifier.IpAddress, identifier.UserId, identifier.EndpointPath, allowed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record request for {IP}", identifier.IpAddress);
        }
    }

    public async Task<RateLimitStatus> GetCurrentStatusAsync(RateLimitIdentifier identifier)
    {
        var status = new RateLimitStatus();

        try
        {
            status.IsWhitelisted = await IsWhitelistedAsync(identifier);
            status.IsBlacklisted = await IsBlacklistedAsync(identifier);

            var rule = FindMatchingRule(identifier);
            if (rule != null)
            {
                // 取得各層級的目前計數
                var ipKey = GetRateLimitKey(RateLimitLevel.IpLevel, identifier, rule);
                var userKey = GetRateLimitKey(RateLimitLevel.UserLevel, identifier, rule);
                var endpointKey = GetRateLimitKey(RateLimitLevel.EndpointLevel, identifier, rule);

                var counts = await _cache.GetCountBatchAsync(new List<string> { ipKey, userKey, endpointKey });

                status.CurrentCounts[RateLimitLevel.IpLevel] = (int)counts.GetValueOrDefault(ipKey, 0);
                status.CurrentCounts[RateLimitLevel.UserLevel] = (int)counts.GetValueOrDefault(userKey, 0);
                status.CurrentCounts[RateLimitLevel.EndpointLevel] = (int)counts.GetValueOrDefault(endpointKey, 0);

                status.Limits[RateLimitLevel.IpLevel] = rule.Limits.IpLevel.MaxRequests;
                status.Limits[RateLimitLevel.UserLevel] = rule.Limits.UserLevel.MaxRequests;
                status.Limits[RateLimitLevel.EndpointLevel] = rule.Limits.EndpointLevel.MaxRequests;
            }

            // 檢查黑名單到期時間
            if (status.IsBlacklisted)
            {
                var blacklistKey = GetBlacklistKey(identifier);
                var ttl = await _cache.GetTimeToLiveAsync(blacklistKey);
                if (ttl.HasValue)
                {
                    status.BlacklistExpiresAt = DateTime.UtcNow.Add(ttl.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current status for {IP}", identifier.IpAddress);
        }

        return status;
    }

    public async Task ResetCounterAsync(RateLimitIdentifier identifier, string reason)
    {
        try
        {
            var rule = FindMatchingRule(identifier);
            if (rule != null)
            {
                var keys = new List<string>
                {
                    GetRateLimitKey(RateLimitLevel.IpLevel, identifier, rule),
                    GetRateLimitKey(RateLimitLevel.UserLevel, identifier, rule),
                    GetRateLimitKey(RateLimitLevel.EndpointLevel, identifier, rule)
                };

                foreach (var key in keys)
                {
                    await _cache.DeleteCountAsync(key);
                }

                _logger.LogInformation("Reset rate limit counters for {IP}, reason: {Reason}", 
                    identifier.IpAddress, reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset counters for {IP}", identifier.IpAddress);
        }
    }

    public async Task<List<RateLimitStatistics>> GetStatisticsAsync(int timeRangeMinutes = 60)
    {
        var statistics = new List<RateLimitStatistics>();

        try
        {
            var endTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var startTime = endTime - (timeRangeMinutes * 60);

            foreach (var rule in _rules.Where(r => r.Enabled))
            {
                var stat = new RateLimitStatistics
                {
                    RuleName = rule.Name,
                    TimeRangeMinutes = timeRangeMinutes
                };

                // 計算時間範圍內的統計資料
                long totalAllowed = 0, totalDenied = 0;

                for (long timestamp = startTime; timestamp <= endTime; timestamp += 3600) // 每小時統計
                {
                    var statsKey = GetStatisticsKey(rule.EndpointPattern, timestamp);
                    var allowedKey = $"stats:allowed:{statsKey}";
                    var deniedKey = $"stats:denied:{statsKey}";

                    var counts = await _cache.GetCountBatchAsync(new List<string> { allowedKey, deniedKey });
                    totalAllowed += counts.GetValueOrDefault(allowedKey, 0);
                    totalDenied += counts.GetValueOrDefault(deniedKey, 0);
                }

                stat.TotalRequests = totalAllowed + totalDenied;
                stat.RateLimitedRequests = totalDenied;
                statistics.Add(stat);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics");
        }

        return statistics;
    }

    public async Task AddOrUpdateRuleAsync(RateLimitRule rule)
    {
        var existingIndex = _rules.FindIndex(r => r.Name == rule.Name);
        if (existingIndex >= 0)
        {
            _rules[existingIndex] = rule;
        }
        else
        {
            _rules.Add(rule);
        }

        // 按優先級排序
        _rules.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        _logger.LogInformation("Added/Updated rate limiting rule: {RuleName}", rule.Name);
    }

    public async Task RemoveRuleAsync(string ruleName)
    {
        var removed = _rules.RemoveAll(r => r.Name == ruleName);
        if (removed > 0)
        {
            _logger.LogInformation("Removed rate limiting rule: {RuleName}", ruleName);
        }
    }

    public async Task<List<RateLimitRule>> GetActiveRulesAsync()
    {
        return _rules.Where(r => r.Enabled).OrderBy(r => r.Priority).ToList();
    }

    public async Task BlacklistTemporaryAsync(RateLimitIdentifier identifier, int durationMinutes, string reason)
    {
        try
        {
            var blacklistKey = GetBlacklistKey(identifier);
            await _cache.SetCountAsync(blacklistKey, 1, TimeSpan.FromMinutes(durationMinutes));

            _logger.LogWarning("Temporarily blacklisted {IP} for {Duration} minutes, reason: {Reason}",
                identifier.IpAddress, durationMinutes, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to blacklist {IP}", identifier.IpAddress);
        }
    }

    public async Task WhitelistAsync(RateLimitIdentifier identifier, string reason)
    {
        try
        {
            var whitelistKey = GetWhitelistKey(identifier);
            await _cache.SetCountAsync(whitelistKey, 1, TimeSpan.FromDays(365)); // 永久白名單

            _logger.LogInformation("Added {IP} to whitelist, reason: {Reason}",
                identifier.IpAddress, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to whitelist {IP}", identifier.IpAddress);
        }
    }

    public async Task CleanupExpiredDataAsync()
    {
        try
        {
            _logger.LogDebug("Starting rate limiting data cleanup");
            
            // 清理過期的滑動窗口計數器
            foreach (var rule in _rules)
            {
                if (rule.Enabled)
                {
                    var baseKeys = new[]
                    {
                        $"ip:{rule.Name}",
                        $"user:{rule.Name}",
                        $"endpoint:{rule.Name}"
                    };

                    foreach (var baseKey in baseKeys)
                    {
                        // 清理過期分段 (僅適用於 Redis 實作)
                        if (_cache is RateLimitingCacheService redisCache)
                        {
                            await redisCache.CleanupSlidingWindowAsync(baseKey, 
                                _options.DefaultWindowSizeSeconds, _options.WindowSegments);
                        }
                    }
                }
            }

            _logger.LogDebug("Rate limiting data cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired data");
        }
    }

    private async Task<RateLimitResult> CheckAllLevelsAsync(RateLimitIdentifier identifier, RateLimitRule rule)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 檢查 IP 層級
        if (rule.Limits.IpLevel.Enabled)
        {
            var ipResult = await CheckLevelAsync(RateLimitLevel.IpLevel, identifier, rule);
            if (!ipResult.IsAllowed)
            {
                ipResult.RuleName = rule.Name;
                return ipResult;
            }
        }

        // 檢查使用者層級 (如果已認證)
        if (rule.Limits.UserLevel.Enabled && !string.IsNullOrEmpty(identifier.UserId))
        {
            var userResult = await CheckLevelAsync(RateLimitLevel.UserLevel, identifier, rule);
            if (!userResult.IsAllowed)
            {
                userResult.RuleName = rule.Name;
                return userResult;
            }
        }

        // 檢查端點層級
        if (rule.Limits.EndpointLevel.Enabled)
        {
            var endpointResult = await CheckLevelAsync(RateLimitLevel.EndpointLevel, identifier, rule);
            if (!endpointResult.IsAllowed)
            {
                endpointResult.RuleName = rule.Name;
                return endpointResult;
            }
        }

        // 所有層級都通過，允許請求
        var windowSize = rule.Limits.IpLevel.WindowSizeSeconds;
        var resetTime = now + windowSize;
        
        return RateLimitResult.Allow(rule.Limits.IpLevel.MaxRequests, 
            rule.Limits.IpLevel.MaxRequests - 1, resetTime, identifier);
    }

    private async Task<RateLimitResult> CheckLevelAsync(RateLimitLevel level, 
        RateLimitIdentifier identifier, RateLimitRule rule)
    {
        var config = level switch
        {
            RateLimitLevel.IpLevel => rule.Limits.IpLevel,
            RateLimitLevel.UserLevel => rule.Limits.UserLevel,
            RateLimitLevel.EndpointLevel => rule.Limits.EndpointLevel,
            _ => throw new ArgumentException($"Unknown rate limit level: {level}")
        };

        if (!config.Enabled)
        {
            return RateLimitResult.Allow(int.MaxValue, int.MaxValue, 
                DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), identifier);
        }

        var key = GetRateLimitKey(level, identifier, rule);
        var windowSize = TimeSpan.FromSeconds(config.WindowSizeSeconds);

        // 使用滑動窗口演算法
        long currentCount;
        if (_cache is RateLimitingCacheService redisCache)
        {
            currentCount = await redisCache.IncrementSlidingWindowAsync(key, 
                config.WindowSizeSeconds, _options.WindowSegments);
        }
        else
        {
            currentCount = await _cache.IncrementAsync(key, windowSize);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var resetTime = now + config.WindowSizeSeconds;

        if (currentCount > config.MaxRequests)
        {
            var retryAfter = config.WindowSizeSeconds;
            return RateLimitResult.Deny(rule.Name, level, config.MaxRequests, 
                resetTime, retryAfter, _options.DefaultErrorMessage, identifier);
        }

        var remaining = Math.Max(0, config.MaxRequests - (int)currentCount);
        return RateLimitResult.Allow(config.MaxRequests, remaining, resetTime, identifier);
    }

    private RateLimitRule? FindMatchingRule(RateLimitIdentifier identifier)
    {
        return _rules
            .Where(r => r.Enabled)
            .OrderBy(r => r.Priority)
            .FirstOrDefault(r => IsRuleMatch(r, identifier));
    }

    private bool IsRuleMatch(RateLimitRule rule, RateLimitIdentifier identifier)
    {
        // 檢查端點模式匹配
        if (!IsPatternMatch(rule.EndpointPattern, identifier.EndpointPath))
        {
            return false;
        }

        // 檢查 HTTP 方法
        if (rule.HttpMethods.Any() && !rule.HttpMethods.Contains(identifier.HttpMethod, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private bool IsPatternMatch(string pattern, string path)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(path))
            return false;

        // 支援萬用字元匹配
        var regexPattern = pattern
            .Replace("*", ".*")
            .Replace("?", ".")
            .Replace("/", "\\/");

        return Regex.IsMatch(path, $"^{regexPattern}$", RegexOptions.IgnoreCase);
    }

    private async Task<bool> IsWhitelistedAsync(RateLimitIdentifier identifier)
    {
        try
        {
            var whitelistKey = GetWhitelistKey(identifier);
            return await _cache.ExistsAsync(whitelistKey);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsBlacklistedAsync(RateLimitIdentifier identifier)
    {
        try
        {
            var blacklistKey = GetBlacklistKey(identifier);
            return await _cache.ExistsAsync(blacklistKey);
        }
        catch
        {
            return false;
        }
    }

    private string GetRateLimitKey(RateLimitLevel level, RateLimitIdentifier identifier, RateLimitRule rule)
    {
        return level switch
        {
            RateLimitLevel.IpLevel => $"ip:{rule.Name}:{identifier.IpAddress}",
            RateLimitLevel.UserLevel => $"user:{rule.Name}:{identifier.UserId ?? "anonymous"}",
            RateLimitLevel.EndpointLevel => $"endpoint:{rule.Name}:{identifier.EndpointPath}",
            _ => throw new ArgumentException($"Unknown rate limit level: {level}")
        };
    }

    private string GetWhitelistKey(RateLimitIdentifier identifier)
    {
        return $"whitelist:ip:{identifier.IpAddress}";
    }

    private string GetBlacklistKey(RateLimitIdentifier identifier)
    {
        return $"blacklist:ip:{identifier.IpAddress}";
    }

    private string GetStatisticsKey(string endpoint, long timestamp)
    {
        var hour = timestamp / 3600; // 按小時統計
        return $"{endpoint}:{hour}";
    }

    private void InitializeDefaultRules()
    {
        if (!_rules.Any())
        {
            // 登入端點嚴格限制
            _rules.Add(new RateLimitRule
            {
                Name = "auth_login",
                EndpointPattern = "/api/auth/token",
                HttpMethods = new List<string> { "POST" },
                Priority = 10,
                Limits = new RateLimitLevels
                {
                    IpLevel = new RateLimitConfig { MaxRequests = 10, WindowSizeSeconds = 60, Enabled = true },
                    UserLevel = new RateLimitConfig { MaxRequests = 5, WindowSizeSeconds = 60, Enabled = true },
                    EndpointLevel = new RateLimitConfig { MaxRequests = 100, WindowSizeSeconds = 60, Enabled = true }
                }
            });

            // 一般 API 中等限制
            _rules.Add(new RateLimitRule
            {
                Name = "general_api",
                EndpointPattern = "/api/*",
                Priority = 50,
                Limits = new RateLimitLevels
                {
                    IpLevel = new RateLimitConfig { MaxRequests = 60, WindowSizeSeconds = 60, Enabled = true },
                    UserLevel = new RateLimitConfig { MaxRequests = 30, WindowSizeSeconds = 60, Enabled = true },
                    EndpointLevel = new RateLimitConfig { MaxRequests = 200, WindowSizeSeconds = 60, Enabled = true }
                }
            });

            // 敏感操作嚴格限制
            _rules.Add(new RateLimitRule
            {
                Name = "sensitive_operations",
                EndpointPattern = "/api/admin/*",
                Priority = 5,
                Limits = new RateLimitLevels
                {
                    IpLevel = new RateLimitConfig { MaxRequests = 5, WindowSizeSeconds = 60, Enabled = true },
                    UserLevel = new RateLimitConfig { MaxRequests = 3, WindowSizeSeconds = 60, Enabled = true },
                    EndpointLevel = new RateLimitConfig { MaxRequests = 20, WindowSizeSeconds = 60, Enabled = true }
                }
            });

            // MFA 驗證端點超嚴格限制
            _rules.Add(new RateLimitRule
            {
                Name = "mfa_verification",
                EndpointPattern = "/api/mfa/verify*",
                HttpMethods = new List<string> { "POST" },
                Priority = 1,
                Limits = new RateLimitLevels
                {
                    IpLevel = new RateLimitConfig { MaxRequests = 5, WindowSizeSeconds = 60, Enabled = true },
                    UserLevel = new RateLimitConfig { MaxRequests = 3, WindowSizeSeconds = 60, Enabled = true },
                    EndpointLevel = new RateLimitConfig { MaxRequests = 10, WindowSizeSeconds = 60, Enabled = true }
                }
            });

            // MFA 挑戰生成端點限制
            _rules.Add(new RateLimitRule
            {
                Name = "mfa_challenge",
                EndpointPattern = "/api/mfa/challenge*",
                HttpMethods = new List<string> { "POST" },
                Priority = 2,
                Limits = new RateLimitLevels
                {
                    IpLevel = new RateLimitConfig { MaxRequests = 10, WindowSizeSeconds = 300, Enabled = true }, // 5分鐘10次
                    UserLevel = new RateLimitConfig { MaxRequests = 5, WindowSizeSeconds = 300, Enabled = true },
                    EndpointLevel = new RateLimitConfig { MaxRequests = 50, WindowSizeSeconds = 300, Enabled = true }
                }
            });

            // MFA Grant Type 嚴格限制
            _rules.Add(new RateLimitRule
            {
                Name = "mfa_grant",
                EndpointPattern = "/api/auth/token",
                HttpMethods = new List<string> { "POST" },
                Priority = 3,
                Limits = new RateLimitLevels
                {
                    IpLevel = new RateLimitConfig { MaxRequests = 8, WindowSizeSeconds = 60, Enabled = true },
                    UserLevel = new RateLimitConfig { MaxRequests = 5, WindowSizeSeconds = 60, Enabled = true },
                    EndpointLevel = new RateLimitConfig { MaxRequests = 20, WindowSizeSeconds = 60, Enabled = true }
                }
            });
        }
    }
}