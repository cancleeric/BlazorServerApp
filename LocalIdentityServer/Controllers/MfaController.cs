using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LocalIdentityServer.Services.MFA;
using LocalIdentityServer.Models.Requests;
using LocalIdentityServer.Models.Responses;
using System.Security.Claims;

namespace LocalIdentityServer.Controllers;

/// <summary>
/// MFA 控制器 - 遵循 RESTful API 設計原則
/// 負責處理多因子認證的設定、驗證和管理
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // 需要先通過基本認證才能管理 MFA
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly ITotpService _totpService;
    private readonly IQrCodeService _qrCodeService;
    private readonly IMfaBackupCodeService _backupCodeService;
    private readonly IMfaAuditService _auditService;
    private readonly ILogger<MfaController> _logger;

    public MfaController(
        IMfaService mfaService,
        ITotpService totpService,
        IQrCodeService qrCodeService,
        IMfaBackupCodeService backupCodeService,
        IMfaAuditService auditService,
        ILogger<MfaController> logger)
    {
        _mfaService = mfaService;
        _totpService = totpService;
        _qrCodeService = qrCodeService;
        _backupCodeService = backupCodeService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// 取得使用者的 MFA 狀態
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<MfaStatusResponse>> GetMfaStatus()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var isEnabled = await _mfaService.IsMfaEnabledAsync(userId);
            var methods = await _mfaService.GetUserMfaMethodsAsync(userId);
            var primaryMethod = await _mfaService.GetPrimaryMfaMethodAsync(userId);
            var backupCodeStatus = await _backupCodeService.GetBackupCodeStatusAsync(userId);

            var response = new MfaStatusResponse
            {
                IsEnabled = isEnabled,
                RequiresMfa = await _mfaService.RequiresMfaVerificationAsync(userId),
                PrimaryMethod = primaryMethod?.Method,
                EnabledMethods = methods.Where(m => m.IsEnabled).Select(m => new MfaMethodInfo
                {
                    Id = m.Id,
                    Method = m.Method,
                    DeviceName = m.DeviceName,
                    IsEnabled = m.IsEnabled,
                    IsPrimary = m.IsPrimary,
                    LastUsedAt = m.LastUsedAt,
                    IsLocked = m.LockedUntil > DateTime.UtcNow
                }).ToList(),
                BackupCodesAvailable = backupCodeStatus.AvailableCodes,
                BackupCodesTotal = backupCodeStatus.TotalGenerated
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA status for user: {UserId}", userId);
            return StatusCode(500, "Failed to retrieve MFA status");
        }
    }

    /// <summary>
    /// 開始 TOTP 設定流程
    /// </summary>
    [HttpPost("setup/totp")]
    public async Task<ActionResult<TotpSetupResponse>> SetupTotp([FromBody] TotpSetupRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            // 產生 TOTP 密鑰
            var secret = _totpService.GenerateSecret();
            
            // 產生 QR Code URI
            var uri = _totpService.GenerateTotpUri(userId, secret, "LocalIdentityServer", request.AccountName);
            
            // 產生 QR Code 圖像
            var qrCodeImage = _qrCodeService.GenerateQrCodeBase64(uri);
            
            // 臨時儲存密鑰 (實際應用中應該使用安全的臨時儲存)
            // 這裡簡化處理，實際應該存到快取或資料庫中
            var setupToken = Guid.NewGuid().ToString("N");

            var response = new TotpSetupResponse
            {
                SetupToken = setupToken,
                Secret = secret,
                QrCodeUri = uri,
                QrCodeImage = qrCodeImage,
                ManualEntryKey = FormatManualEntryKey(secret)
            };

            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                EventType = "TotpSetupStarted",
                Method = "TOTP",
                Result = MfaResults.Success,
                Description = "TOTP setup process initiated",
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent()
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup TOTP for user: {UserId}", userId);
            return StatusCode(500, "Failed to setup TOTP");
        }
    }

    /// <summary>
    /// 確認並完成 TOTP 設定
    /// </summary>
    [HttpPost("setup/totp/confirm")]
    public async Task<ActionResult<MfaSetupConfirmResponse>> ConfirmTotpSetup([FromBody] TotpConfirmRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            // 驗證 TOTP 代碼 (這裡需要從臨時儲存中取得密鑰)
            // 實際實作中應該從快取或資料庫中取得對應的密鑰
            var result = _totpService.VerifyTotpWithTolerance(request.Secret, request.Code, 1);
            
            if (!result.IsValid)
            {
                await _auditService.LogMfaEventAsync(new MfaAuditEvent
                {
                    UserId = userId,
                    EventType = "TotpSetupFailed",
                    Method = "TOTP",
                    Result = MfaResults.Failed,
                    FailureReason = "InvalidCode",
                    Description = "TOTP setup verification failed",
                    IpAddress = GetClientIpAddress(),
                    UserAgent = GetUserAgent()
                });

                return BadRequest(new { message = "Invalid verification code" });
            }

            // 啟用 TOTP 方法
            var success = await _mfaService.EnableMfaAsync(userId, "TOTP", request.DeviceName);
            
            if (!success)
            {
                return StatusCode(500, "Failed to enable TOTP");
            }

            // 產生備用碼
            var backupCodes = await _backupCodeService.GenerateBackupCodesAsync(userId, 10);

            var response = new MfaSetupConfirmResponse
            {
                Success = true,
                BackupCodes = backupCodes.Success ? backupCodes.BackupCodes : Array.Empty<string>(),
                Message = "TOTP has been successfully enabled"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm TOTP setup for user: {UserId}", userId);
            return StatusCode(500, "Failed to confirm TOTP setup");
        }
    }

    /// <summary>
    /// 設定 SMS MFA
    /// </summary>
    [HttpPost("setup/sms")]
    public async Task<ActionResult<MfaSetupResponse>> SetupSms([FromBody] SmsSetupRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            // 驗證手機號碼格式
            // 實際實作中需要發送驗證碼到手機號碼進行驗證
            
            var success = await _mfaService.EnableMfaAsync(userId, "SMS", request.DeviceName);
            
            if (!success)
            {
                return StatusCode(500, "Failed to enable SMS MFA");
            }

            var response = new MfaSetupResponse
            {
                Success = true,
                Message = "SMS MFA has been successfully enabled"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup SMS MFA for user: {UserId}", userId);
            return StatusCode(500, "Failed to setup SMS MFA");
        }
    }

    /// <summary>
    /// 設定 Email MFA
    /// </summary>
    [HttpPost("setup/email")]
    public async Task<ActionResult<MfaSetupResponse>> SetupEmail([FromBody] EmailSetupRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var success = await _mfaService.EnableMfaAsync(userId, "EMAIL", request.DeviceName);
            
            if (!success)
            {
                return StatusCode(500, "Failed to enable Email MFA");
            }

            var response = new MfaSetupResponse
            {
                Success = true,
                Message = "Email MFA has been successfully enabled"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup Email MFA for user: {UserId}", userId);
            return StatusCode(500, "Failed to setup Email MFA");
        }
    }

    /// <summary>
    /// 停用 MFA 方法
    /// </summary>
    [HttpPost("disable/{method}")]
    public async Task<ActionResult<MfaDisableResponse>> DisableMfa(string method)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var success = await _mfaService.DisableMfaAsync(userId, method.ToUpper());
            
            var response = new MfaDisableResponse
            {
                Success = success,
                Message = success ? $"{method} MFA has been disabled" : "Failed to disable MFA method"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable MFA method {Method} for user: {UserId}", method, userId);
            return StatusCode(500, "Failed to disable MFA method");
        }
    }

    /// <summary>
    /// 驗證 MFA 代碼
    /// </summary>
    [HttpPost("verify")]
    [AllowAnonymous] // 驗證過程中可能還未完全認證
    public async Task<ActionResult<MfaVerifyResponse>> VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        try
        {
            var context = new MfaVerificationContext
            {
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                ClientId = request.ClientId,
                SessionId = request.SessionId
            };

            var result = await _mfaService.VerifyMfaCodeAsync(request.UserId, request.Method, request.Code, context);

            var response = new MfaVerifyResponse
            {
                IsValid = result.IsValid,
                IsLocked = result.IsLocked,
                RemainingAttempts = result.RemainingAttempts,
                LockedUntil = result.LockedUntil,
                Message = result.ErrorMessage
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify MFA code for user: {UserId}", request.UserId);
            return StatusCode(500, "Failed to verify MFA code");
        }
    }

    /// <summary>
    /// 產生 MFA 挑戰 (發送 SMS/Email)
    /// </summary>
    [HttpPost("challenge")]
    [AllowAnonymous]
    public async Task<ActionResult<MfaChallengeResponse>> GenerateChallenge([FromBody] MfaChallengeRequest request)
    {
        try
        {
            var context = new MfaVerificationContext
            {
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                ClientId = request.ClientId,
                SessionId = request.SessionId
            };

            var result = await _mfaService.GenerateMfaChallengeAsync(request.UserId, request.Method, context);

            var response = new MfaChallengeResponse
            {
                Success = result.Success,
                ChallengeId = result.ChallengeId,
                ExpiresAt = result.ExpiresAt,
                MaskedTarget = result.MaskedTarget,
                Message = result.ErrorMessage
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate MFA challenge for user: {UserId}", request.UserId);
            return StatusCode(500, "Failed to generate MFA challenge");
        }
    }

    /// <summary>
    /// 產生新的備用碼
    /// </summary>
    [HttpPost("backup-codes/generate")]
    public async Task<ActionResult<BackupCodesResponse>> GenerateBackupCodes()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var result = await _backupCodeService.GenerateBackupCodesAsync(userId, 10);
            
            var response = new BackupCodesResponse
            {
                Success = result.Success,
                BackupCodes = result.Success ? result.BackupCodes : Array.Empty<string>(),
                ExpiresAt = result.ExpiresAt,
                Message = result.ErrorMessage
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate backup codes for user: {UserId}", userId);
            return StatusCode(500, "Failed to generate backup codes");
        }
    }

    /// <summary>
    /// 取得備用碼狀態
    /// </summary>
    [HttpGet("backup-codes/status")]
    public async Task<ActionResult<BackupCodesStatusResponse>> GetBackupCodesStatus()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var status = await _backupCodeService.GetBackupCodeStatusAsync(userId);
            
            var response = new BackupCodesStatusResponse
            {
                TotalGenerated = status.TotalGenerated,
                AvailableCodes = status.AvailableCodes,
                UsedCodes = status.UsedCodes,
                LastGeneratedAt = status.LastGeneratedAt,
                LastUsedAt = status.LastUsedAt,
                ExpiresAt = status.ExpiresAt,
                HasExpiredCodes = status.HasExpiredCodes
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup codes status for user: {UserId}", userId);
            return StatusCode(500, "Failed to get backup codes status");
        }
    }

    /// <summary>
    /// 取得 MFA 審計日誌
    /// </summary>
    [HttpGet("audit-logs")]
    public async Task<ActionResult<MfaAuditLogsResponse>> GetAuditLogs([FromQuery] int limit = 50)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var logs = await _auditService.GetUserMfaAuditLogsAsync(userId, limit);
            
            var response = new MfaAuditLogsResponse
            {
                Logs = logs.Select(log => new MfaAuditLogInfo
                {
                    EventType = log.EventType,
                    Method = log.Method,
                    Result = log.Result,
                    Description = log.Description,
                    IpAddress = log.IpAddress,
                    CreatedAt = log.CreatedAt,
                    FailureReason = log.FailureReason
                }).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA audit logs for user: {UserId}", userId);
            return StatusCode(500, "Failed to get audit logs");
        }
    }

    // 私有輔助方法

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private string GetClientIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }
        
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    private string GetUserAgent()
    {
        return Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
    }

    private string FormatManualEntryKey(string secret)
    {
        // 將密鑰格式化為易於手動輸入的格式 (每4字符一組)
        var formatted = string.Empty;
        for (int i = 0; i < secret.Length; i += 4)
        {
            if (i > 0) formatted += " ";
            formatted += secret.Substring(i, Math.Min(4, secret.Length - i));
        }
        return formatted;
    }
}