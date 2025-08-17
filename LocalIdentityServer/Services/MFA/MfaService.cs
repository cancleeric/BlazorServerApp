using LocalIdentityServer.Data.Entities;
using LocalIdentityServer.Data.Repositories;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// MFA 主要服務實作 - 協調各種 MFA 方法的操作
/// 遵循單一責任原則 (SRP) - 專責處理 MFA 業務邏輯
/// 遵循依賴反轉原則 (DIP) - 透過介面依賴其他服務
/// </summary>
public class MfaService : IMfaService
{
    private readonly IMfaRepository _mfaRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly ISmsService _smsService;
    private readonly IEmailService _emailService;
    private readonly IMfaBackupCodeService _backupCodeService;
    private readonly IMfaEncryptionService _encryptionService;
    private readonly IMfaAuditService _auditService;
    private readonly ILogger<MfaService> _logger;
    private readonly IConfiguration _configuration;

    // 設定參數
    private readonly int _maxFailedAttempts;
    private readonly int _lockoutDurationMinutes;
    private readonly bool _requireMfaForNewUsers;

    public MfaService(
        IMfaRepository mfaRepository,
        IUserRepository userRepository,
        ITotpService totpService,
        ISmsService smsService,
        IEmailService emailService,
        IMfaBackupCodeService backupCodeService,
        IMfaEncryptionService encryptionService,
        IMfaAuditService auditService,
        ILogger<MfaService> logger,
        IConfiguration configuration)
    {
        _mfaRepository = mfaRepository;
        _userRepository = userRepository;
        _totpService = totpService;
        _smsService = smsService;
        _emailService = emailService;
        _backupCodeService = backupCodeService;
        _encryptionService = encryptionService;
        _auditService = auditService;
        _logger = logger;
        _configuration = configuration;

        // 從設定檔讀取參數
        _maxFailedAttempts = int.Parse(_configuration["Mfa:MaxFailedAttempts"] ?? "5");
        _lockoutDurationMinutes = int.Parse(_configuration["Mfa:LockoutDurationMinutes"] ?? "15");
        _requireMfaForNewUsers = bool.Parse(_configuration["Mfa:RequireForNewUsers"] ?? "false");
    }

    public async Task<bool> IsMfaEnabledAsync(string userId)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.IsMfaEnabled == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check MFA status for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> RequiresMfaVerificationAsync(string userId)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.RequireMfa == true || (await IsMfaEnabledAsync(userId) && _requireMfaForNewUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check MFA requirement for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<UserMfaEntity?> GetPrimaryMfaMethodAsync(string userId)
    {
        try
        {
            return await _mfaRepository.GetPrimaryMfaMethodAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get primary MFA method for user: {UserId}", userId);
            return null;
        }
    }

    public async Task<IEnumerable<UserMfaEntity>> GetUserMfaMethodsAsync(string userId)
    {
        try
        {
            return await _mfaRepository.GetByUserIdAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MFA methods for user: {UserId}", userId);
            return Enumerable.Empty<UserMfaEntity>();
        }
    }

    public async Task<bool> EnableMfaAsync(string userId, string method, string? deviceName = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be null or empty", nameof(method));

        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Attempted to enable MFA for non-existent user: {UserId}", userId);
                return false;
            }

            // 檢查方法是否已存在
            var existingMethod = await _mfaRepository.GetByUserIdAndMethodAsync(userId, method);
            if (existingMethod != null && existingMethod.IsEnabled)
            {
                _logger.LogInformation("MFA method {Method} already enabled for user: {UserId}", method, userId);
                return true;
            }

            var mfaMethod = existingMethod ?? new UserMfaEntity
            {
                UserId = userId,
                Method = method,
                DeviceName = deviceName ?? $"{method} Device",
                CreatedAt = DateTime.UtcNow
            };

            // 根據方法類型設定特定屬性
            switch (method.ToUpper())
            {
                case "TOTP":
                    await SetupTotpMethodAsync(mfaMethod);
                    break;
                case "SMS":
                    await SetupSmsMethodAsync(mfaMethod, user);
                    break;
                case "EMAIL":
                    await SetupEmailMethodAsync(mfaMethod, user);
                    break;
                default:
                    _logger.LogError("Unsupported MFA method: {Method}", method);
                    return false;
            }

            mfaMethod.IsEnabled = true;
            mfaMethod.UpdatedAt = DateTime.UtcNow;

            // 如果這是第一個 MFA 方法，設為主要方法
            var userMethods = await GetUserMfaMethodsAsync(userId);
            if (!userMethods.Any(m => m.IsPrimary))
            {
                mfaMethod.IsPrimary = true;
            }

            if (existingMethod == null)
            {
                await _mfaRepository.CreateAsync(mfaMethod);
            }
            else
            {
                await _mfaRepository.UpdateAsync(mfaMethod);
            }

            // 更新使用者的 MFA 狀態
            user.IsMfaEnabled = true;
            user.MfaSetupAt = DateTime.UtcNow;
            user.DefaultMfaMethod = method;
            await _userRepository.UpdateAsync(user);

            // 記錄審計日誌
            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                MfaMethodId = mfaMethod.Id,
                EventType = MfaEventTypes.Setup,
                Method = method,
                Result = MfaResults.Success,
                Description = $"MFA method {method} enabled successfully",
                AdditionalData = new Dictionary<string, object>
                {
                    ["DeviceName"] = mfaMethod.DeviceName,
                    ["IsPrimary"] = mfaMethod.IsPrimary
                }
            });

            _logger.LogInformation("MFA method {Method} enabled successfully for user: {UserId}", method, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable MFA method {Method} for user: {UserId}", method, userId);

            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                EventType = MfaEventTypes.Setup,
                Method = method,
                Result = MfaResults.Failed,
                FailureReason = "SystemError",
                Description = $"Failed to enable MFA method: {ex.Message}"
            });

            return false;
        }
    }

    public async Task<bool> DisableMfaAsync(string userId, string method)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be null or empty", nameof(method));

        try
        {
            var mfaMethod = await _mfaRepository.GetByUserIdAndMethodAsync(userId, method);
            if (mfaMethod == null || !mfaMethod.IsEnabled)
            {
                _logger.LogInformation("MFA method {Method} not found or already disabled for user: {UserId}", method, userId);
                return true;
            }

            // 停用方法
            mfaMethod.IsEnabled = false;
            mfaMethod.UpdatedAt = DateTime.UtcNow;
            await _mfaRepository.UpdateAsync(mfaMethod);

            // 檢查是否還有其他啟用的方法
            var activeMethods = await _mfaRepository.GetEnabledMfaMethodsAsync(userId);
            if (!activeMethods.Any())
            {
                // 如果沒有其他啟用的方法，停用使用者的 MFA
                var user = await _userRepository.GetByIdAsync(userId);
                if (user != null)
                {
                    user.IsMfaEnabled = false;
                    user.DefaultMfaMethod = null;
                    await _userRepository.UpdateAsync(user);
                }

                // 撤銷所有備用碼
                await _backupCodeService.RevokeAllBackupCodesAsync(userId, userId, "All MFA methods disabled");
            }

            // 記錄審計日誌
            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                MfaMethodId = mfaMethod.Id,
                EventType = MfaEventTypes.Disable,
                Method = method,
                Result = MfaResults.Success,
                Description = $"MFA method {method} disabled successfully"
            });

            _logger.LogInformation("MFA method {Method} disabled successfully for user: {UserId}", method, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable MFA method {Method} for user: {UserId}", method, userId);

            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                EventType = MfaEventTypes.Disable,
                Method = method,
                Result = MfaResults.Failed,
                FailureReason = "SystemError",
                Description = $"Failed to disable MFA method: {ex.Message}"
            });

            return false;
        }
    }

    public async Task<MfaVerificationResult> VerifyMfaCodeAsync(string userId, string method, string code, MfaVerificationContext context)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be null or empty", nameof(method));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or empty", nameof(code));

        try
        {
            // 檢查方法是否被鎖定
            if (await IsMfaMethodLockedAsync(userId, method))
            {
                var mfaMethod = await _mfaRepository.GetByUserIdAndMethodAsync(userId, method);
                return new MfaVerificationResult
                {
                    IsValid = false,
                    IsLocked = true,
                    LockedUntil = mfaMethod?.LockedUntil,
                    ErrorMessage = "MFA method is temporarily locked due to too many failed attempts"
                };
            }

            bool verificationResult = false;
            string? failureReason = null;

            // 根據方法類型進行驗證
            switch (method.ToUpper())
            {
                case "TOTP":
                    verificationResult = await VerifyTotpCodeAsync(userId, code);
                    break;
                case "SMS":
                case "EMAIL":
                    // SMS/Email 驗證邏輯需要額外的挑戰機制
                    verificationResult = await VerifyOtpCodeAsync(userId, method, code, context);
                    break;
                case "BACKUPCODE":
                    var backupResult = await _backupCodeService.VerifyBackupCodeAsync(userId, code, context);
                    verificationResult = backupResult.IsValid;
                    if (!verificationResult)
                    {
                        failureReason = backupResult.ErrorMessage ?? "Invalid backup code";
                    }
                    break;
                default:
                    failureReason = "Unsupported MFA method";
                    break;
            }

            if (verificationResult)
            {
                // 重設失敗次數
                await ResetFailedAttemptsAsync(userId, method);

                // 記錄成功的審計日誌
                await _auditService.LogMfaEventAsync(new MfaAuditEvent
                {
                    UserId = userId,
                    EventType = MfaEventTypes.Verify,
                    Method = method,
                    Result = MfaResults.Success,
                    Description = "MFA verification successful",
                    IpAddress = context.IpAddress,
                    UserAgent = context.UserAgent,
                    ClientId = context.ClientId,
                    SessionId = context.SessionId,
                    GeoLocation = context.GeoLocation,
                    DeviceFingerprint = context.DeviceFingerprint
                });

                return new MfaVerificationResult
                {
                    IsValid = true,
                    RiskScore = await _auditService.CalculateRiskScoreAsync(context, userId)
                };
            }
            else
            {
                // 增加失敗次數並檢查是否需要鎖定
                await IncrementFailedAttemptsAsync(userId, method);
                var remainingAttempts = await GetRemainingAttemptsAsync(userId, method);

                // 記錄失敗的審計日誌
                await _auditService.LogMfaEventAsync(new MfaAuditEvent
                {
                    UserId = userId,
                    EventType = MfaEventTypes.Verify,
                    Method = method,
                    Result = MfaResults.Failed,
                    FailureReason = failureReason ?? "InvalidCode",
                    Description = "MFA verification failed",
                    IpAddress = context.IpAddress,
                    UserAgent = context.UserAgent,
                    ClientId = context.ClientId,
                    SessionId = context.SessionId,
                    GeoLocation = context.GeoLocation,
                    DeviceFingerprint = context.DeviceFingerprint
                });

                return new MfaVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = failureReason ?? "Invalid verification code",
                    FailureReason = failureReason,
                    RemainingAttempts = remainingAttempts,
                    IsLocked = remainingAttempts <= 0,
                    RiskScore = await _auditService.CalculateRiskScoreAsync(context, userId)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify MFA code for user: {UserId}, method: {Method}", userId, method);

            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                EventType = MfaEventTypes.Verify,
                Method = method,
                Result = MfaResults.Failed,
                FailureReason = "SystemError",
                Description = $"MFA verification system error: {ex.Message}",
                IpAddress = context.IpAddress,
                UserAgent = context.UserAgent,
                ClientId = context.ClientId,
                SessionId = context.SessionId
            });

            return new MfaVerificationResult
            {
                IsValid = false,
                ErrorMessage = "Verification system error"
            };
        }
    }

    public async Task<MfaChallengeResult> GenerateMfaChallengeAsync(string userId, string method, MfaVerificationContext context)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be null or empty", nameof(method));

        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return new MfaChallengeResult
                {
                    Success = false,
                    ErrorMessage = "User not found"
                };
            }

            var mfaMethod = await _mfaRepository.GetByUserIdAndMethodAsync(userId, method);
            if (mfaMethod == null || !mfaMethod.IsEnabled)
            {
                return new MfaChallengeResult
                {
                    Success = false,
                    ErrorMessage = "MFA method not found or not enabled"
                };
            }

            // 檢查是否被鎖定
            if (await IsMfaMethodLockedAsync(userId, method))
            {
                return new MfaChallengeResult
                {
                    Success = false,
                    ErrorMessage = "MFA method is temporarily locked"
                };
            }

            var challengeId = Guid.NewGuid().ToString("N");
            var expiresAt = DateTime.UtcNow.AddMinutes(5); // 5分鐘有效期
            var code = _encryptionService.GenerateSecureRandomString(6, false); // 6位數數字

            bool challengeResult = false;
            string? maskedTarget = null;

            switch (method.ToUpper())
            {
                case "SMS":
                    if (!string.IsNullOrEmpty(mfaMethod.PhoneNumber))
                    {
                        var smsResult = await _smsService.SendMfaCodeAsync(mfaMethod.PhoneNumber, code, 5);
                        challengeResult = smsResult.IsSuccess;
                        maskedTarget = smsResult.MaskedPhoneNumber;
                    }
                    break;
                case "EMAIL":
                    if (!string.IsNullOrEmpty(mfaMethod.EmailAddress))
                    {
                        var emailResult = await _emailService.SendMfaCodeAsync(mfaMethod.EmailAddress, code, 5, user.UserName);
                        challengeResult = emailResult.IsSuccess;
                        maskedTarget = emailResult.MaskedEmailAddress;
                    }
                    break;
                default:
                    return new MfaChallengeResult
                    {
                        Success = false,
                        ErrorMessage = "Challenge not supported for this method"
                    };
            }

            if (challengeResult)
            {
                // 儲存挑戰資訊 (這裡簡化實作，實際應該存到快取或資料庫)
                var challengeData = new
                {
                    UserId = userId,
                    Method = method,
                    Code = code,
                    ExpiresAt = expiresAt,
                    Context = context
                };

                // 記錄審計日誌
                await _auditService.LogMfaEventAsync(new MfaAuditEvent
                {
                    UserId = userId,
                    MfaMethodId = mfaMethod.Id,
                    EventType = MfaEventTypes.ChallengeGenerated,
                    Method = method,
                    Result = MfaResults.Success,
                    Description = "MFA challenge generated successfully",
                    IpAddress = context.IpAddress,
                    UserAgent = context.UserAgent,
                    ClientId = context.ClientId,
                    SessionId = context.SessionId,
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["ChallengeId"] = challengeId,
                        ["ExpiresAt"] = expiresAt,
                        ["MaskedTarget"] = maskedTarget ?? ""
                    }
                });

                return new MfaChallengeResult
                {
                    Success = true,
                    ChallengeId = challengeId,
                    ExpiresAt = expiresAt,
                    DeliveryMethod = method,
                    MaskedTarget = maskedTarget
                };
            }
            else
            {
                return new MfaChallengeResult
                {
                    Success = false,
                    ErrorMessage = "Failed to send verification code"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate MFA challenge for user: {UserId}, method: {Method}", userId, method);
            return new MfaChallengeResult
            {
                Success = false,
                ErrorMessage = "Challenge generation failed"
            };
        }
    }

    public async Task<bool> ResetMfaAsync(string userId, string adminUserId, string reason)
    {
        try
        {
            // 停用所有 MFA 方法
            var userMethods = await GetUserMfaMethodsAsync(userId);
            foreach (var method in userMethods)
            {
                method.IsEnabled = false;
                method.UpdatedAt = DateTime.UtcNow;
                await _mfaRepository.UpdateAsync(method);
            }

            // 更新使用者狀態
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.IsMfaEnabled = false;
                user.DefaultMfaMethod = null;
                await _userRepository.UpdateAsync(user);
            }

            // 撤銷所有備用碼
            await _backupCodeService.RevokeAllBackupCodesAsync(userId, adminUserId, reason);

            // 記錄審計日誌
            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                EventType = MfaEventTypes.Reset,
                Method = "All",
                Result = MfaResults.Success,
                Description = $"MFA reset by admin: {adminUserId}. Reason: {reason}",
                AdditionalData = new Dictionary<string, object>
                {
                    ["AdminUserId"] = adminUserId,
                    ["Reason"] = reason,
                    ["MethodsDisabled"] = userMethods.Count()
                }
            });

            _logger.LogInformation("MFA reset successfully for user: {UserId} by admin: {AdminUserId}", userId, adminUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset MFA for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> IsMfaMethodLockedAsync(string userId, string method)
    {
        try
        {
            var mfaMethod = await _mfaRepository.GetByUserIdAndMethodAsync(userId, method);
            return mfaMethod?.LockedUntil > DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check MFA method lock status for user: {UserId}, method: {Method}", userId, method);
            return false;
        }
    }

    public async Task<bool> UnlockMfaMethodAsync(string userId, string method, string adminUserId)
    {
        try
        {
            var mfaMethod = await _mfaRepository.GetByUserIdAndMethodAsync(userId, method);
            if (mfaMethod == null)
                return false;

            mfaMethod.FailedAttempts = 0;
            mfaMethod.LockedUntil = null;
            mfaMethod.UpdatedAt = DateTime.UtcNow;
            await _mfaRepository.UpdateAsync(mfaMethod);

            // 記錄審計日誌
            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                MfaMethodId = mfaMethod.Id,
                EventType = MfaEventTypes.MethodUnlocked,
                Method = method,
                Result = MfaResults.Success,
                Description = $"MFA method unlocked by admin: {adminUserId}",
                AdditionalData = new Dictionary<string, object>
                {
                    ["AdminUserId"] = adminUserId
                }
            });

            _logger.LogInformation("MFA method {Method} unlocked for user: {UserId} by admin: {AdminUserId}", method, userId, adminUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlock MFA method {Method} for user: {UserId}", method, userId);
            return false;
        }
    }

    // 私有輔助方法

    private async Task SetupTotpMethodAsync(UserMfaEntity mfaMethod)
    {
        var secret = _totpService.GenerateSecret();
        var encryptedSecret = _encryptionService.EncryptSecret(secret, mfaMethod.UserId);
        mfaMethod.EncryptedSecret = encryptedSecret;
    }

    private async Task SetupSmsMethodAsync(UserMfaEntity mfaMethod, UserEntity user)
    {
        // SMS MFA 需要在呼叫 EnableMfaAsync 時提供手機號碼
        // 這裡不設定 PhoneNumber，因為它應該在呼叫端設定
        // 實際實作中需要額外的手機號碼驗證流程
        if (string.IsNullOrEmpty(mfaMethod.PhoneNumber))
        {
            throw new InvalidOperationException("Phone number is required for SMS MFA setup");
        }
    }

    private async Task SetupEmailMethodAsync(UserMfaEntity mfaMethod, UserEntity user)
    {
        mfaMethod.EmailAddress = user.Email;
    }

    private async Task<bool> VerifyTotpCodeAsync(string userId, string code)
    {
        var mfaMethod = await _mfaRepository.GetByUserIdAndMethodAsync(userId, "TOTP");
        if (mfaMethod?.EncryptedSecret == null)
            return false;

        try
        {
            var secret = _encryptionService.DecryptSecret(mfaMethod.EncryptedSecret, userId);
            var result = _totpService.VerifyTotpWithTolerance(secret, code, 1);
            return result.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify TOTP code for user: {UserId}", userId);
            return false;
        }
    }

    private async Task<bool> VerifyOtpCodeAsync(string userId, string method, string code, MfaVerificationContext context)
    {
        // 這裡需要實作 OTP 驗證邏輯
        // 實際實作中需要從快取或資料庫中取得先前發送的 OTP 代碼進行比對
        // 暫時返回 false，表示需要進一步實作
        return false;
    }

    private async Task IncrementFailedAttemptsAsync(string userId, string method)
    {
        var mfaMethod = await _mfaRepository.GetByUserIdAndMethodAsync(userId, method);
        if (mfaMethod == null)
            return;

        mfaMethod.FailedAttempts++;
        mfaMethod.UpdatedAt = DateTime.UtcNow;

        if (mfaMethod.FailedAttempts >= _maxFailedAttempts)
        {
            mfaMethod.LockedUntil = DateTime.UtcNow.AddMinutes(_lockoutDurationMinutes);

            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                MfaMethodId = mfaMethod.Id,
                EventType = MfaEventTypes.MethodLocked,
                Method = method,
                Result = MfaResults.Blocked,
                Description = $"MFA method locked due to {_maxFailedAttempts} failed attempts"
            });
        }

        await _mfaRepository.UpdateAsync(mfaMethod);
    }

    private async Task ResetFailedAttemptsAsync(string userId, string method)
    {
        var mfaMethod = await _mfaRepository.GetByUserIdAndMethodAsync(userId, method);
        if (mfaMethod == null)
            return;

        mfaMethod.FailedAttempts = 0;
        mfaMethod.LockedUntil = null;
        mfaMethod.LastUsedAt = DateTime.UtcNow;
        mfaMethod.UpdatedAt = DateTime.UtcNow;

        await _mfaRepository.UpdateAsync(mfaMethod);
    }

    private async Task<int> GetRemainingAttemptsAsync(string userId, string method)
    {
        var mfaMethod = await _mfaRepository.GetByUserIdAndMethodAsync(userId, method);
        if (mfaMethod == null)
            return _maxFailedAttempts;

        return Math.Max(0, _maxFailedAttempts - mfaMethod.FailedAttempts);
    }
}