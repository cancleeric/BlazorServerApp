using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LocalIdentityServer.Services.MFA;
using LocalIdentityServer.Models.Requests;
using LocalIdentityServer.Models.Responses;
using System.Security.Claims;

namespace LocalIdentityServer.Controllers;

/// <summary>
/// MFA 管理控制器 - 管理員專用
/// 負責處理 MFA 的管理功能，如重設、解鎖、統計等
/// </summary>
[ApiController]
[Route("api/admin/mfa")]
[Authorize(Roles = "Admin")] // 僅管理員可存取
public class MfaAdminController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly IMfaBackupCodeService _backupCodeService;
    private readonly IMfaAuditService _auditService;
    private readonly ILogger<MfaAdminController> _logger;

    public MfaAdminController(
        IMfaService mfaService,
        IMfaBackupCodeService backupCodeService,
        IMfaAuditService auditService,
        ILogger<MfaAdminController> logger)
    {
        _mfaService = mfaService;
        _backupCodeService = backupCodeService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// 重設使用者的 MFA 設定
    /// </summary>
    [HttpPost("reset")]
    public async Task<ActionResult<MfaAdminResponse>> ResetUserMfa([FromBody] MfaResetRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (string.IsNullOrEmpty(adminUserId))
            return Unauthorized();

        try
        {
            var success = await _mfaService.ResetMfaAsync(request.UserId, adminUserId, request.Reason);

            var response = new MfaAdminResponse
            {
                Success = success,
                Message = success ? "User MFA has been reset successfully" : "Failed to reset user MFA",
                ActionPerformed = "MFA Reset",
                TargetUserId = request.UserId,
                AdminUserId = adminUserId,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Admin {AdminId} reset MFA for user {UserId}. Reason: {Reason}", 
                adminUserId, request.UserId, request.Reason);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset MFA for user: {UserId} by admin: {AdminId}", 
                request.UserId, adminUserId);
            return StatusCode(500, "Failed to reset user MFA");
        }
    }

    /// <summary>
    /// 解鎖使用者的 MFA 方法
    /// </summary>
    [HttpPost("unlock")]
    public async Task<ActionResult<MfaAdminResponse>> UnlockUserMfaMethod([FromBody] MfaUnlockRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (string.IsNullOrEmpty(adminUserId))
            return Unauthorized();

        try
        {
            var success = await _mfaService.UnlockMfaMethodAsync(request.UserId, request.Method, adminUserId);

            var response = new MfaAdminResponse
            {
                Success = success,
                Message = success ? $"User {request.Method} MFA method has been unlocked successfully" : "Failed to unlock user MFA method",
                ActionPerformed = "MFA Method Unlock",
                TargetUserId = request.UserId,
                AdminUserId = adminUserId,
                Timestamp = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    ["Method"] = request.Method
                }
            };

            _logger.LogInformation("Admin {AdminId} unlocked {Method} MFA for user {UserId}", 
                adminUserId, request.Method, request.UserId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlock {Method} MFA for user: {UserId} by admin: {AdminId}", 
                request.Method, request.UserId, adminUserId);
            return StatusCode(500, "Failed to unlock user MFA method");
        }
    }

    /// <summary>
    /// 取得使用者的詳細 MFA 狀態 (管理員檢視)
    /// </summary>
    [HttpGet("users/{userId}/status")]
    public async Task<ActionResult<MfaAdminUserStatusResponse>> GetUserMfaStatus(string userId)
    {
        try
        {
            var isEnabled = await _mfaService.IsMfaEnabledAsync(userId);
            var methods = await _mfaService.GetUserMfaMethodsAsync(userId);
            var backupCodeStatus = await _backupCodeService.GetBackupCodeStatusAsync(userId);
            var recentLogs = await _auditService.GetUserMfaAuditLogsAsync(userId, 10);

            var response = new MfaAdminUserStatusResponse
            {
                UserId = userId,
                IsEnabled = isEnabled,
                RequiresMfa = await _mfaService.RequiresMfaVerificationAsync(userId),
                Methods = methods.Select(m => new MfaAdminMethodInfo
                {
                    Id = m.Id,
                    Method = m.Method,
                    DeviceName = m.DeviceName,
                    IsEnabled = m.IsEnabled,
                    IsPrimary = m.IsPrimary,
                    IsLocked = m.LockedUntil > DateTime.UtcNow,
                    LockedUntil = m.LockedUntil,
                    FailedAttempts = m.FailedAttempts,
                    LastUsedAt = m.LastUsedAt,
                    CreatedAt = m.CreatedAt,
                    PhoneNumber = m.PhoneNumber,
                    EmailAddress = m.EmailAddress
                }).ToList(),
                BackupCodesStatus = new BackupCodesAdminInfo
                {
                    TotalGenerated = backupCodeStatus.TotalGenerated,
                    AvailableCodes = backupCodeStatus.AvailableCodes,
                    UsedCodes = backupCodeStatus.UsedCodes,
                    LastGeneratedAt = backupCodeStatus.LastGeneratedAt,
                    LastUsedAt = backupCodeStatus.LastUsedAt,
                    ExpiresAt = backupCodeStatus.ExpiresAt,
                    HasExpiredCodes = backupCodeStatus.HasExpiredCodes,
                    CurrentBatchId = backupCodeStatus.CurrentBatchId
                },
                RecentActivity = recentLogs.Take(5).Select(log => new MfaAuditLogInfo
                {
                    EventType = log.EventType,
                    Method = log.Method,
                    Result = log.Result,
                    Description = log.Description,
                    IpAddress = log.IpAddress,
                    CreatedAt = log.CreatedAt,
                    FailureReason = log.FailureReason,
                    RiskScore = log.RiskScore
                }).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA status for user: {UserId}", userId);
            return StatusCode(500, "Failed to get user MFA status");
        }
    }

    /// <summary>
    /// 取得 MFA 使用統計
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<MfaStatisticsResponse>> GetMfaStatistics([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var stats = await _auditService.GetMfaUsageStatisticsAsync(startDate, endDate);

            var response = new MfaStatisticsResponse
            {
                TotalMfaUsers = stats.TotalMfaUsers,
                TotpUsers = stats.TotpUsers,
                SmsUsers = stats.SmsUsers,
                EmailUsers = stats.EmailUsers,
                TotalVerifications = stats.TotalVerifications,
                SuccessfulVerifications = stats.SuccessfulVerifications,
                FailedVerifications = stats.FailedVerifications,
                BackupCodeUsage = stats.BackupCodeUsage,
                SuccessRate = stats.SuccessRate,
                MethodUsage = stats.MethodUsage,
                FailureReasons = stats.FailureReasons,
                DateRange = new
                {
                    StartDate = startDate,
                    EndDate = endDate
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA statistics");
            return StatusCode(500, "Failed to get MFA statistics");
        }
    }

    /// <summary>
    /// 取得可疑的 MFA 活動
    /// </summary>
    [HttpGet("suspicious-activity")]
    public async Task<ActionResult<SuspiciousActivityResponse>> GetSuspiciousActivity([FromQuery] int hours = 24)
    {
        try
        {
            var activities = await _auditService.AnalyzeSuspiciousActivityAsync(null, hours);

            var response = new SuspiciousActivityResponse
            {
                Activities = activities.Select(a => new SuspiciousActivityInfo
                {
                    UserId = a.UserId,
                    ActivityType = a.ActivityType,
                    Description = a.Description,
                    RiskScore = a.RiskScore,
                    IpAddress = a.IpAddress,
                    FirstOccurrence = a.FirstOccurrence,
                    LastOccurrence = a.LastOccurrence,
                    Frequency = a.Frequency,
                    RelatedEvents = a.RelatedEvents
                }).ToList(),
                AnalysisTimeRange = hours,
                GeneratedAt = DateTime.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get suspicious MFA activity");
            return StatusCode(500, "Failed to get suspicious activity");
        }
    }

    /// <summary>
    /// 取得失敗的 MFA 嘗試
    /// </summary>
    [HttpGet("failed-attempts")]
    public async Task<ActionResult<FailedAttemptsResponse>> GetFailedAttempts([FromQuery] string? ipAddress = null, [FromQuery] int hours = 1)
    {
        try
        {
            var failedAttempts = await _auditService.GetFailedMfaAttemptsAsync(ipAddress, hours);

            var response = new FailedAttemptsResponse
            {
                FailedAttempts = failedAttempts.Select(log => new FailedAttemptInfo
                {
                    UserId = log.UserId,
                    Method = log.Method,
                    IpAddress = log.IpAddress,
                    UserAgent = log.UserAgent,
                    FailureReason = log.FailureReason,
                    AttemptTime = log.CreatedAt,
                    RiskScore = log.RiskScore
                }).ToList(),
                TimeRange = hours,
                FilteredByIp = ipAddress,
                TotalCount = failedAttempts.Count()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get failed MFA attempts");
            return StatusCode(500, "Failed to get failed attempts");
        }
    }

    /// <summary>
    /// 清理過期的備用碼
    /// </summary>
    [HttpPost("cleanup/backup-codes")]
    public async Task<ActionResult<MfaAdminResponse>> CleanupExpiredBackupCodes()
    {
        var adminUserId = GetCurrentUserId();
        if (string.IsNullOrEmpty(adminUserId))
            return Unauthorized();

        try
        {
            var cleanedCount = await _backupCodeService.CleanupExpiredBackupCodesAsync();

            var response = new MfaAdminResponse
            {
                Success = true,
                Message = $"Cleaned up {cleanedCount} expired backup codes",
                ActionPerformed = "Backup Codes Cleanup",
                AdminUserId = adminUserId,
                Timestamp = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    ["CleanedCount"] = cleanedCount
                }
            };

            _logger.LogInformation("Admin {AdminId} cleaned up {Count} expired backup codes", 
                adminUserId, cleanedCount);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired backup codes");
            return StatusCode(500, "Failed to cleanup expired backup codes");
        }
    }

    /// <summary>
    /// 清理舊的審計日誌
    /// </summary>
    [HttpPost("cleanup/audit-logs")]
    public async Task<ActionResult<MfaAdminResponse>> CleanupOldAuditLogs([FromQuery] int retentionDays = 90)
    {
        var adminUserId = GetCurrentUserId();
        if (string.IsNullOrEmpty(adminUserId))
            return Unauthorized();

        try
        {
            var cleanedCount = await _auditService.CleanupOldAuditLogsAsync(retentionDays);

            var response = new MfaAdminResponse
            {
                Success = true,
                Message = $"Cleaned up {cleanedCount} old audit logs (older than {retentionDays} days)",
                ActionPerformed = "Audit Logs Cleanup",
                AdminUserId = adminUserId,
                Timestamp = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    ["CleanedCount"] = cleanedCount,
                    ["RetentionDays"] = retentionDays
                }
            };

            _logger.LogInformation("Admin {AdminId} cleaned up {Count} old audit logs", 
                adminUserId, cleanedCount);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old audit logs");
            return StatusCode(500, "Failed to cleanup old audit logs");
        }
    }

    // 私有輔助方法
    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}