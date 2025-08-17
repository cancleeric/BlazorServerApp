using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// MFA 審計服務實作
/// 負責記錄和分析 MFA 相關的安全事件
/// </summary>
public class MfaAuditService : IMfaAuditService
{
    private readonly IMfaAuditRepository _auditRepository;
    private readonly ILogger<MfaAuditService> _logger;

    public MfaAuditService(
        IMfaAuditRepository auditRepository,
        ILogger<MfaAuditService> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task LogMfaEventAsync(MfaAuditEvent auditEvent)
    {
        if (auditEvent == null)
            throw new ArgumentNullException(nameof(auditEvent));

        try
        {
            var auditLog = new MfaAuditLogEntity
            {
                UserId = auditEvent.UserId,
                MfaMethodId = auditEvent.MfaMethodId,
                EventType = auditEvent.EventType,
                Method = auditEvent.Method,
                Result = auditEvent.Result,
                FailureReason = auditEvent.FailureReason,
                Description = auditEvent.Description,
                IpAddress = auditEvent.IpAddress,
                UserAgent = auditEvent.UserAgent,
                ClientId = auditEvent.ClientId,
                SessionId = auditEvent.SessionId,
                RiskScore = auditEvent.RiskScore,
                GeoLocation = auditEvent.GeoLocation,
                DeviceFingerprint = auditEvent.DeviceFingerprint,
                AdditionalData = System.Text.Json.JsonSerializer.Serialize(auditEvent.AdditionalData)
            };

            await _auditRepository.CreateAsync(auditLog);
            
            _logger.LogDebug("MFA audit event logged: {EventType} for user: {UserId}", 
                auditEvent.EventType, auditEvent.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log MFA audit event for user: {UserId}", auditEvent.UserId);
            throw;
        }
    }

    public async Task<IEnumerable<MfaAuditLogEntity>> GetUserMfaAuditLogsAsync(string userId, int limit = 50)
    {
        return await _auditRepository.GetUserAuditLogsAsync(userId, limit);
    }

    public async Task<IEnumerable<MfaAuditLogEntity>> GetMfaEventsByTimeRangeAsync(DateTime startTime, DateTime endTime, string? userId = null)
    {
        return await _auditRepository.GetAuditLogsByTimeRangeAsync(startTime, endTime, userId);
    }

    public async Task<IEnumerable<SuspiciousActivity>> AnalyzeSuspiciousActivityAsync(string? userId = null, int hours = 24)
    {
        var startTime = DateTime.UtcNow.AddHours(-hours);
        var activities = await _auditRepository.GetSuspiciousActivitiesAsync(startTime, null, 50);
        
        return activities.Select(a => new SuspiciousActivity
        {
            UserId = a.UserId,
            ActivityType = a.ActivityType,
            Description = $"Suspicious activity detected: {a.ActivityType}",
            RiskScore = a.RiskScore,
            IpAddress = a.IpAddress,
            FirstOccurrence = a.FirstOccurrence,
            LastOccurrence = a.LastOccurrence,
            Frequency = a.EventCount,
            RelatedEvents = a.EventTypes
        });
    }

    public async Task<Services.MFA.MfaUsageStatistics> GetMfaUsageStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var repoStats = await _auditRepository.GetMfaUsageStatisticsAsync(startDate, endDate);
        
        return new Services.MFA.MfaUsageStatistics
        {
            TotalMfaUsers = repoStats.UniqueUsers,
            TotpUsers = repoStats.MethodUsage.GetValueOrDefault("TOTP", 0),
            SmsUsers = repoStats.MethodUsage.GetValueOrDefault("SMS", 0),
            EmailUsers = repoStats.MethodUsage.GetValueOrDefault("Email", 0),
            TotalVerifications = repoStats.TotalEvents,
            SuccessfulVerifications = repoStats.SuccessfulVerifications,
            FailedVerifications = repoStats.FailedVerifications,
            BackupCodeUsage = repoStats.MethodUsage.GetValueOrDefault("BackupCode", 0),
            SuccessRate = repoStats.SuccessRate,
            MethodUsage = repoStats.MethodUsage,
            FailureReasons = repoStats.FailureReasons
        };
    }

    public async Task<IEnumerable<MfaAuditLogEntity>> GetFailedMfaAttemptsAsync(string? ipAddress = null, int hours = 1)
    {
        var startTime = DateTime.UtcNow.AddHours(-hours);
        return await _auditRepository.GetFailedMfaAttemptsAsync(null, ipAddress, startTime);
    }

    public async Task<int> CalculateRiskScoreAsync(MfaVerificationContext context, string userId)
    {
        return await _auditRepository.CalculateUserRiskScoreAsync(userId, context.IpAddress, context.UserAgent);
    }

    public async Task<bool> IsAnomalousLoginAsync(string userId, MfaVerificationContext context)
    {
        return await _auditRepository.IsAnomalousLoginAsync(userId, context.IpAddress, context.UserAgent);
    }

    public async Task<int> CleanupOldAuditLogsAsync(int retentionDays = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        return await _auditRepository.CleanupOldAuditLogsAsync(cutoffDate);
    }
}