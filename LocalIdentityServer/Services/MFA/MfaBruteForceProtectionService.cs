using Microsoft.Extensions.Caching.Memory;
using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Models;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// MFA 暴力破解防護服務實作
/// 遵循單一責任原則 (SRP) - 專責處理 MFA 暴力破解防護
/// 遵循依賴反轉原則 (DIP) - 透過介面依賴其他服務
/// </summary>
public class MfaBruteForceProtectionService : IMfaBruteForceProtectionService
{
    private readonly IMemoryCache _cache;
    private readonly IMfaAuditRepository _auditRepository;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ILogger<MfaBruteForceProtectionService> _logger;
    private readonly IConfiguration _configuration;

    // 配置參數
    private readonly int _maxFailedAttemptsPerHour;
    private readonly int _maxFailedAttemptsPerDay;
    private readonly int _suspiciousThreshold;
    private readonly int _autoLockDurationMinutes;
    private readonly int _autoBlockDurationMinutes;

    // 快取鍵前綴
    private const string FAILED_ATTEMPTS_PREFIX = "mfa_failed_";
    private const string LOCKED_USER_PREFIX = "mfa_locked_user_";
    private const string BLOCKED_IP_PREFIX = "mfa_blocked_ip_";
    private const string SUSPICIOUS_ACTIVITY_PREFIX = "mfa_suspicious_";

    public MfaBruteForceProtectionService(
        IMemoryCache cache,
        IMfaAuditRepository auditRepository,
        IRateLimitingService rateLimitingService,
        ILogger<MfaBruteForceProtectionService> logger,
        IConfiguration configuration)
    {
        _cache = cache;
        _auditRepository = auditRepository;
        _rateLimitingService = rateLimitingService;
        _logger = logger;
        _configuration = configuration;

        // 讀取配置
        _maxFailedAttemptsPerHour = int.Parse(_configuration["MfaSecurity:MaxFailedAttemptsPerHour"] ?? "10");
        _maxFailedAttemptsPerDay = int.Parse(_configuration["MfaSecurity:MaxFailedAttemptsPerDay"] ?? "50");
        _suspiciousThreshold = int.Parse(_configuration["MfaSecurity:SuspiciousThreshold"] ?? "70");
        _autoLockDurationMinutes = int.Parse(_configuration["MfaSecurity:AutoLockDurationMinutes"] ?? "30");
        _autoBlockDurationMinutes = int.Parse(_configuration["MfaSecurity:AutoBlockDurationMinutes"] ?? "60");
    }

    public async Task<SuspiciousActivityResult> CheckSuspiciousActivityAsync(
        string userId, 
        string ipAddress, 
        string method, 
        MfaVerificationContext context)
    {
        var result = new SuspiciousActivityResult();
        
        try
        {
            var riskScore = 0;
            var riskFactors = new List<string>();

            // 檢查失敗嘗試頻率
            var recentFailures = await GetRecentFailedAttempts(userId, ipAddress);
            if (recentFailures.HourlyCount > _maxFailedAttemptsPerHour / 2)
            {
                riskScore += 30;
                riskFactors.Add($"High failure rate: {recentFailures.HourlyCount} attempts in last hour");
            }

            // 檢查 IP 地址變化
            var knownIps = await GetUserKnownIps(userId);
            if (!knownIps.Contains(ipAddress))
            {
                riskScore += 25;
                riskFactors.Add("Unknown IP address");
            }

            // 檢查時間模式
            var timeRisk = AnalyzeTimePattern(context);
            riskScore += timeRisk.Score;
            if (timeRisk.Score > 0)
            {
                riskFactors.Add(timeRisk.Reason);
            }

            // 檢查設備指紋
            if (!string.IsNullOrEmpty(context.DeviceFingerprint))
            {
                var deviceRisk = await AnalyzeDeviceFingerprint(userId, context.DeviceFingerprint);
                riskScore += deviceRisk.Score;
                if (deviceRisk.Score > 0)
                {
                    riskFactors.Add(deviceRisk.Reason);
                }
            }

            // 檢查地理位置
            if (!string.IsNullOrEmpty(context.GeoLocation))
            {
                var geoRisk = await AnalyzeGeoLocation(userId, context.GeoLocation);
                riskScore += geoRisk.Score;
                if (geoRisk.Score > 0)
                {
                    riskFactors.Add(geoRisk.Reason);
                }
            }

            // 檢查同一 IP 的其他使用者活動
            var ipActivityRisk = await AnalyzeIpActivity(ipAddress, userId);
            riskScore += ipActivityRisk.Score;
            if (ipActivityRisk.Score > 0)
            {
                riskFactors.Add(ipActivityRisk.Reason);
            }

            result.RiskScore = Math.Min(riskScore, 100);
            result.RiskFactors = riskFactors;
            result.IsSuspicious = result.RiskScore >= _suspiciousThreshold;

            // 決定是否應該阻擋
            if (result.RiskScore >= 90)
            {
                result.ShouldBlock = true;
                result.BlockReason = "Critical risk score detected";
            }
            else if (recentFailures.HourlyCount >= _maxFailedAttemptsPerHour)
            {
                result.ShouldBlock = true;
                result.BlockReason = "Maximum hourly attempts exceeded";
            }

            // 生成建議措施
            result.RecommendedActions = GenerateRecommendedActions(result);

            _logger.LogInformation("Suspicious activity check for user {UserId} from IP {IP}: Risk={Risk}, Suspicious={Suspicious}", 
                userId, ipAddress, result.RiskScore, result.IsSuspicious);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking suspicious activity for user {UserId} from IP {IP}", userId, ipAddress);
            return new SuspiciousActivityResult { RiskScore = 0, IsSuspicious = false };
        }
    }

    public async Task RecordFailedAttemptAsync(
        string userId, 
        string ipAddress, 
        string method, 
        MfaVerificationContext context)
    {
        try
        {
            // 記錄到快取
            var userKey = $"{FAILED_ATTEMPTS_PREFIX}user_{userId}";
            var ipKey = $"{FAILED_ATTEMPTS_PREFIX}ip_{ipAddress}";
            var combinedKey = $"{FAILED_ATTEMPTS_PREFIX}combined_{userId}_{ipAddress}";

            await IncrementFailureCount(userKey, TimeSpan.FromHours(1));
            await IncrementFailureCount(ipKey, TimeSpan.FromHours(1));
            await IncrementFailureCount(combinedKey, TimeSpan.FromHours(24));

            // 檢查是否需要自動防護措施
            var userFailures = GetCachedValue<int>(userKey);
            var ipFailures = GetCachedValue<int>(ipKey);

            if (userFailures >= _maxFailedAttemptsPerHour)
            {
                await TemporaryLockUserMfaAsync(userId, _autoLockDurationMinutes, 
                    $"Automatic lock due to {userFailures} failed attempts");
            }

            if (ipFailures >= _maxFailedAttemptsPerHour * 2) // IP 限制更寬鬆
            {
                await TemporaryBlockIpAsync(ipAddress, _autoBlockDurationMinutes,
                    $"Automatic block due to {ipFailures} failed attempts");
            }

            _logger.LogWarning("MFA failed attempt recorded: User={UserId}, IP={IP}, Method={Method}, UserFailures={UserFailures}, IpFailures={IpFailures}",
                userId, ipAddress, method, userFailures, ipFailures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording failed MFA attempt for user {UserId}", userId);
        }
    }

    public async Task RecordSuccessfulAttemptAsync(
        string userId, 
        string ipAddress, 
        string method, 
        MfaVerificationContext context)
    {
        try
        {
            // 清除部分失敗計數 (成功一次減少失敗計數)
            var userKey = $"{FAILED_ATTEMPTS_PREFIX}user_{userId}";
            var currentFailures = GetCachedValue<int>(userKey);
            if (currentFailures > 0)
            {
                _cache.Set(userKey, Math.Max(0, currentFailures - 1), TimeSpan.FromHours(1));
            }

            // 記錄已知 IP
            await RecordKnownIp(userId, ipAddress);

            // 記錄設備指紋
            if (!string.IsNullOrEmpty(context.DeviceFingerprint))
            {
                await RecordKnownDevice(userId, context.DeviceFingerprint);
            }

            _logger.LogInformation("MFA successful attempt recorded: User={UserId}, IP={IP}, Method={Method}",
                userId, ipAddress, method);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording successful MFA attempt for user {UserId}", userId);
        }
    }

    public async Task<SecurityRecommendation> GetSecurityRecommendationAsync(string userId, string ipAddress)
    {
        var recommendation = new SecurityRecommendation();
        
        try
        {
            var recentFailures = await GetRecentFailedAttempts(userId, ipAddress);
            var riskScore = await CalculateUserRiskScore(userId, ipAddress);

            // 根據風險分數決定安全等級
            recommendation.Level = riskScore switch
            {
                >= 80 => SecurityLevel.Critical,
                >= 60 => SecurityLevel.High,
                >= 40 => SecurityLevel.Medium,
                _ => SecurityLevel.Low
            };

            // 生成建議
            var recommendations = new List<string>();

            if (recentFailures.HourlyCount > 3)
            {
                recommendations.Add("Consider enabling additional MFA methods");
                recommendations.Add("Review recent login activity");
            }

            if (riskScore >= 60)
            {
                recommendations.Add("Enable login notifications");
                recommendations.Add("Consider changing MFA backup codes");
                recommendation.RequiresImmediateAction = true;
            }

            if (riskScore >= 80)
            {
                recommendations.Add("Temporarily disable compromised MFA method");
                recommendations.Add("Force password reset");
                recommendations.Add("Review all active sessions");
            }

            recommendation.Recommendations = recommendations;
            recommendation.AdditionalInfo["risk_score"] = riskScore;
            recommendation.AdditionalInfo["recent_failures"] = recentFailures;

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security recommendation for user {UserId}", userId);
            return new SecurityRecommendation { Level = SecurityLevel.Low };
        }
    }

    public async Task<bool> TemporaryLockUserMfaAsync(string userId, int durationMinutes, string reason)
    {
        try
        {
            var lockKey = $"{LOCKED_USER_PREFIX}{userId}";
            var lockInfo = new
            {
                LockedAt = DateTime.UtcNow,
                Reason = reason,
                DurationMinutes = durationMinutes
            };

            _cache.Set(lockKey, lockInfo, TimeSpan.FromMinutes(durationMinutes));

            _logger.LogWarning("User MFA temporarily locked: User={UserId}, Duration={Duration}min, Reason={Reason}",
                userId, durationMinutes, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking user MFA for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> TemporaryBlockIpAsync(string ipAddress, int durationMinutes, string reason)
    {
        try
        {
            var blockKey = $"{BLOCKED_IP_PREFIX}{ipAddress}";
            var blockInfo = new
            {
                BlockedAt = DateTime.UtcNow,
                Reason = reason,
                DurationMinutes = durationMinutes
            };

            _cache.Set(blockKey, blockInfo, TimeSpan.FromMinutes(durationMinutes));

            // 同時使用 Rate Limiting 服務進行封鎖
            var identifier = new RateLimitIdentifier { IpAddress = ipAddress };
            await _rateLimitingService.BlacklistTemporaryAsync(identifier, durationMinutes, reason);

            _logger.LogWarning("IP temporarily blocked: IP={IP}, Duration={Duration}min, Reason={Reason}",
                ipAddress, durationMinutes, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking IP {IP}", ipAddress);
            return false;
        }
    }

    public async Task<int> CleanupExpiredDataAsync()
    {
        var cleanedCount = 0;
        
        try
        {
            // Memory cache 會自動清理過期資料，這裡主要是清理持久化的審計日誌
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            cleanedCount = await _auditRepository.CleanupOldAuditLogsAsync(cutoffDate);

            _logger.LogInformation("Cleaned up {Count} expired MFA protection records", cleanedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup of expired MFA protection data");
        }

        return cleanedCount;
    }

    public async Task<BruteForceProtectionStatistics> GetStatisticsAsync(int timeRangeHours = 24)
    {
        var statistics = new BruteForceProtectionStatistics
        {
            TimeRangeHours = timeRangeHours
        };

        try
        {
            var startTime = DateTime.UtcNow.AddHours(-timeRangeHours);
            
            var failedAttempts = await _auditRepository.GetFailedMfaAttemptsAsync(
                startTime: startTime, 
                limit: 10000);

            statistics.TotalFailedAttempts = failedAttempts.Count();
            statistics.BlockedIpCount = GetActiveBlockedIpCount();
            statistics.LockedUserCount = GetActiveLockedUserCount();
            
            // 分析攻擊模式
            var attackPatterns = failedAttempts
                .GroupBy(f => f.Method)
                .ToDictionary(g => g.Key, g => g.Count());
            
            statistics.CommonAttackPatterns = attackPatterns;

            // 計算平均風險分數
            var riskScores = await CalculateRiskScoresForFailedAttempts(failedAttempts);
            statistics.AverageRiskScore = riskScores.Any() ? riskScores.Average() : 0;

            statistics.SuspiciousActivityCount = riskScores.Count(score => score >= _suspiciousThreshold);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating brute force protection statistics");
        }

        return statistics;
    }

    // 私有輔助方法

    private async Task<(int HourlyCount, int DailyCount)> GetRecentFailedAttempts(string userId, string ipAddress)
    {
        var userKey = $"{FAILED_ATTEMPTS_PREFIX}user_{userId}";
        var ipKey = $"{FAILED_ATTEMPTS_PREFIX}ip_{ipAddress}";
        
        var userFailures = GetCachedValue<int>(userKey);
        var ipFailures = GetCachedValue<int>(ipKey);
        
        return (Math.Max(userFailures, ipFailures), userFailures + ipFailures);
    }

    private async Task<List<string>> GetUserKnownIps(string userId)
    {
        var key = $"known_ips_{userId}";
        return GetCachedValue<List<string>>(key) ?? new List<string>();
    }

    private async Task RecordKnownIp(string userId, string ipAddress)
    {
        var key = $"known_ips_{userId}";
        var knownIps = GetCachedValue<List<string>>(key) ?? new List<string>();
        
        if (!knownIps.Contains(ipAddress))
        {
            knownIps.Add(ipAddress);
            if (knownIps.Count > 10) // 限制記錄數量
            {
                knownIps.RemoveAt(0);
            }
            _cache.Set(key, knownIps, TimeSpan.FromDays(30));
        }
    }

    private async Task RecordKnownDevice(string userId, string deviceFingerprint)
    {
        var key = $"known_devices_{userId}";
        var knownDevices = GetCachedValue<List<string>>(key) ?? new List<string>();
        
        if (!knownDevices.Contains(deviceFingerprint))
        {
            knownDevices.Add(deviceFingerprint);
            if (knownDevices.Count > 5) // 限制記錄數量
            {
                knownDevices.RemoveAt(0);
            }
            _cache.Set(key, knownDevices, TimeSpan.FromDays(90));
        }
    }

    private (int Score, string Reason) AnalyzeTimePattern(MfaVerificationContext context)
    {
        var now = DateTime.UtcNow;
        
        // 檢查是否為異常時間
        if (now.Hour < 6 || now.Hour > 23)
        {
            return (15, "Login attempt during unusual hours");
        }
        
        return (0, string.Empty);
    }

    private async Task<(int Score, string Reason)> AnalyzeDeviceFingerprint(string userId, string deviceFingerprint)
    {
        var knownDevices = GetCachedValue<List<string>>($"known_devices_{userId}") ?? new List<string>();
        
        if (!knownDevices.Contains(deviceFingerprint))
        {
            return (20, "Unknown device fingerprint");
        }
        
        return (0, string.Empty);
    }

    private async Task<(int Score, string Reason)> AnalyzeGeoLocation(string userId, string geoLocation)
    {
        // 簡化的地理位置分析
        var knownLocations = GetCachedValue<List<string>>($"known_locations_{userId}") ?? new List<string>();
        
        if (!knownLocations.Contains(geoLocation))
        {
            return (25, "Login from new geographic location");
        }
        
        return (0, string.Empty);
    }

    private async Task<(int Score, string Reason)> AnalyzeIpActivity(string ipAddress, string userId)
    {
        // 檢查同一 IP 是否有其他使用者的異常活動
        var ipActivityKey = $"ip_activity_{ipAddress}";
        var ipActivity = GetCachedValue<Dictionary<string, int>>(ipActivityKey) ?? new Dictionary<string, int>();
        
        var otherUserActivity = ipActivity.Where(kv => kv.Key != userId).Sum(kv => kv.Value);
        
        if (otherUserActivity > 10)
        {
            return (30, "High activity from multiple users on same IP");
        }
        
        return (0, string.Empty);
    }

    private async Task<int> CalculateUserRiskScore(string userId, string ipAddress)
    {
        var recentFailures = await GetRecentFailedAttempts(userId, ipAddress);
        var baseScore = Math.Min(recentFailures.HourlyCount * 10, 50);
        
        // 可以添加更多風險因素
        return baseScore;
    }

    private List<string> GenerateRecommendedActions(SuspiciousActivityResult result)
    {
        var actions = new List<string>();
        
        if (result.RiskScore >= 70)
        {
            actions.Add("Enable additional MFA methods");
            actions.Add("Review recent account activity");
        }
        
        if (result.RiskScore >= 90)
        {
            actions.Add("Change password immediately");
            actions.Add("Revoke all active sessions");
        }
        
        return actions;
    }

    private int GetActiveBlockedIpCount()
    {
        // 簡化實作 - 實際中需要從快取中統計
        return 0;
    }

    private int GetActiveLockedUserCount()
    {
        // 簡化實作 - 實際中需要從快取中統計
        return 0;
    }

    private async Task<List<int>> CalculateRiskScoresForFailedAttempts(IEnumerable<LocalIdentityServer.Data.Entities.MfaAuditLogEntity> failedAttempts)
    {
        // 簡化實作 - 為每個失敗嘗試計算風險分數
        return failedAttempts.Select(f => 50).ToList(); // 預設風險分數
    }

    private async Task IncrementFailureCount(string key, TimeSpan expiration)
    {
        var currentCount = GetCachedValue<int>(key);
        _cache.Set(key, currentCount + 1, expiration);
    }

    private T GetCachedValue<T>(string key)
    {
        return _cache.TryGetValue(key, out var value) && value is T result ? result : default(T)!;
    }
}