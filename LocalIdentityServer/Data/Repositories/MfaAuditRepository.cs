using LocalIdentityServer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// MFA 審計日誌儲存庫實作
/// 遵循儲存庫模式，負責 MFA 審計日誌的資料存取操作
/// </summary>
public class MfaAuditRepository : IMfaAuditRepository
{
    private readonly LocalIdentityDbContext _context;
    private readonly ILogger<MfaAuditRepository> _logger;

    public MfaAuditRepository(LocalIdentityDbContext context, ILogger<MfaAuditRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MfaAuditLogEntity> CreateAsync(MfaAuditLogEntity auditLog)
    {
        if (auditLog == null)
            throw new ArgumentNullException(nameof(auditLog));

        try
        {
            _context.MfaAuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
            
            return auditLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log for user: {UserId}", auditLog.UserId);
            throw;
        }
    }

    public async Task<MfaAuditLogEntity?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        try
        {
            return await _context.MfaAuditLogs
                .Include(al => al.User)
                .Include(al => al.MfaMethod)
                .FirstOrDefaultAsync(al => al.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit log by ID: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MfaAuditLogEntity>> GetUserAuditLogsAsync(string userId, int limit = 50, int offset = 0)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Enumerable.Empty<MfaAuditLogEntity>();

        try
        {
            return await _context.MfaAuditLogs
                .Where(al => al.UserId == userId)
                .OrderByDescending(al => al.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit logs for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<MfaAuditLogEntity>> GetAuditLogsByTimeRangeAsync(
        DateTime startTime, DateTime endTime, string? userId = null, string? eventType = null, 
        string? result = null, int limit = 1000, int offset = 0)
    {
        try
        {
            var query = _context.MfaAuditLogs
                .Where(al => al.CreatedAt >= startTime && al.CreatedAt <= endTime);

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(al => al.UserId == userId);

            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(al => al.EventType == eventType);

            if (!string.IsNullOrWhiteSpace(result))
                query = query.Where(al => al.Result == result);

            return await query
                .OrderByDescending(al => al.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit logs by time range");
            throw;
        }
    }

    public async Task<IEnumerable<MfaAuditLogEntity>> GetFailedMfaAttemptsAsync(
        string? userId = null, string? ipAddress = null, DateTime? startTime = null, int limit = 100)
    {
        try
        {
            var query = _context.MfaAuditLogs
                .Where(al => al.Result == "Failed");

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(al => al.UserId == userId);

            if (!string.IsNullOrWhiteSpace(ipAddress))
                query = query.Where(al => al.IpAddress == ipAddress);

            if (startTime.HasValue)
                query = query.Where(al => al.CreatedAt >= startTime.Value);

            return await query
                .OrderByDescending(al => al.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get failed MFA attempts");
            throw;
        }
    }

    public async Task<MfaUsageStatistics> GetMfaUsageStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var query = _context.MfaAuditLogs.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(al => al.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(al => al.CreatedAt <= endDate.Value);

            var logs = await query.ToListAsync();

            var verificationLogs = logs.Where(al => al.EventType == "Verify").ToList();
            var successfulVerifications = verificationLogs.Count(al => al.Result == "Success");
            var failedVerifications = verificationLogs.Count(al => al.Result == "Failed");

            return new MfaUsageStatistics
            {
                TotalEvents = logs.Count,
                SuccessfulVerifications = successfulVerifications,
                FailedVerifications = failedVerifications,
                UniqueUsers = logs.Select(al => al.UserId).Distinct().Count(),
                SuccessRate = verificationLogs.Count > 0 ? 
                            (double)successfulVerifications / verificationLogs.Count * 100 : 0,
                MethodUsage = logs.GroupBy(al => al.Method)
                                .ToDictionary(g => g.Key, g => g.Count()),
                EventTypes = logs.GroupBy(al => al.EventType)
                               .ToDictionary(g => g.Key, g => g.Count()),
                FailureReasons = logs.Where(al => !string.IsNullOrEmpty(al.FailureReason))
                                   .GroupBy(al => al.FailureReason!)
                                   .ToDictionary(g => g.Key, g => g.Count()),
                StartDate = startDate,
                EndDate = endDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA usage statistics");
            throw;
        }
    }

    public async Task<bool> IsAnomalousLoginAsync(string userId, string ipAddress, string userAgent)
    {
        try
        {
            var recentLogs = await _context.MfaAuditLogs
                .Where(al => al.UserId == userId && al.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                .ToListAsync();

            // 檢查新 IP 地址
            var knownIps = recentLogs.Select(al => al.IpAddress).Distinct().ToList();
            if (!knownIps.Contains(ipAddress))
                return true;

            // 檢查異常失敗次數
            var recentFailures = recentLogs
                .Where(al => al.Result == "Failed" && al.CreatedAt >= DateTime.UtcNow.AddHours(-1))
                .Count();

            return recentFailures >= 5;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check anomalous login for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<int> CalculateUserRiskScoreAsync(string userId, string ipAddress, string userAgent)
    {
        try
        {
            int riskScore = 0;

            var recentLogs = await _context.MfaAuditLogs
                .Where(al => al.UserId == userId && al.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                .ToListAsync();

            // 新 IP 地址風險
            var knownIps = recentLogs.Select(al => al.IpAddress).Distinct().ToList();
            if (!knownIps.Contains(ipAddress))
                riskScore += 30;

            // 失敗嘗試風險
            var recentFailures = recentLogs
                .Where(al => al.Result == "Failed" && al.CreatedAt >= DateTime.UtcNow.AddHours(-1))
                .Count();
            riskScore += Math.Min(recentFailures * 10, 50);

            // 多次登入嘗試風險
            var recentAttempts = recentLogs
                .Where(al => al.CreatedAt >= DateTime.UtcNow.AddMinutes(-15))
                .Count();
            if (recentAttempts > 5)
                riskScore += 20;

            return Math.Min(riskScore, 100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate risk score for user: {UserId}", userId);
            return 0;
        }
    }

    public async Task<int> CleanupOldAuditLogsAsync(DateTime beforeDate)
    {
        try
        {
            var oldLogs = await _context.MfaAuditLogs
                .Where(al => al.CreatedAt < beforeDate)
                .ToListAsync();

            _context.MfaAuditLogs.RemoveRange(oldLogs);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} old audit logs", oldLogs.Count);

            return oldLogs.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old audit logs");
            throw;
        }
    }

    // 實作剩餘方法的簡化版本
    public async Task<IEnumerable<MfaAuditLogEntity>> GetAuditLogsByIpAddressAsync(string ipAddress, DateTime? startTime = null, DateTime? endTime = null, int limit = 100)
    {
        var query = _context.MfaAuditLogs.Where(al => al.IpAddress == ipAddress);
        if (startTime.HasValue) query = query.Where(al => al.CreatedAt >= startTime.Value);
        if (endTime.HasValue) query = query.Where(al => al.CreatedAt <= endTime.Value);
        return await query.Take(limit).ToListAsync();
    }

    public async Task<IEnumerable<SuspiciousActivityResult>> GetSuspiciousActivitiesAsync(DateTime? startTime = null, DateTime? endTime = null, int minRiskScore = 50, int limit = 100)
    {
        return new List<SuspiciousActivityResult>(); // 簡化實作
    }

    public async Task<Dictionary<string, int>> GetEventTypeStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.MfaAuditLogs.AsQueryable();
        if (startDate.HasValue) query = query.Where(al => al.CreatedAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(al => al.CreatedAt <= endDate.Value);
        return await query.GroupBy(al => al.EventType).ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetMethodUsageStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.MfaAuditLogs.AsQueryable();
        if (startDate.HasValue) query = query.Where(al => al.CreatedAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(al => al.CreatedAt <= endDate.Value);
        return await query.GroupBy(al => al.Method).ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetFailureReasonStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.MfaAuditLogs.Where(al => !string.IsNullOrEmpty(al.FailureReason));
        if (startDate.HasValue) query = query.Where(al => al.CreatedAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(al => al.CreatedAt <= endDate.Value);
        return await query.GroupBy(al => al.FailureReason!).ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<long> GetTotalAuditLogsCountAsync()
    {
        return await _context.MfaAuditLogs.LongCountAsync();
    }

    public async Task<IEnumerable<MfaAuditLogEntity>> ExportAuditLogsAsync(DateTime startDate, DateTime endDate, string? userId = null)
    {
        var query = _context.MfaAuditLogs.Where(al => al.CreatedAt >= startDate && al.CreatedAt <= endDate);
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(al => al.UserId == userId);
        return await query.OrderBy(al => al.CreatedAt).ToListAsync();
    }
}