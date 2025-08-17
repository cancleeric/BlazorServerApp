using LocalIdentityServer.Data.Repositories;
using LocalIdentityServer.Data.Entities;
using System.Security.Cryptography;

namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// MFA 備用碼服務實作
/// 負責產生、驗證和管理 MFA 備用恢復碼
/// </summary>
public class MfaBackupCodeService : IMfaBackupCodeService
{
    private readonly IMfaBackupCodeRepository _backupCodeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMfaAuditService _auditService;
    private readonly ILogger<MfaBackupCodeService> _logger;
    private readonly int _codeLength;
    private readonly int _defaultExpirationDays;

    public MfaBackupCodeService(
        IMfaBackupCodeRepository backupCodeRepository,
        IUserRepository userRepository,
        IMfaAuditService auditService,
        ILogger<MfaBackupCodeService> logger,
        IConfiguration configuration)
    {
        _backupCodeRepository = backupCodeRepository;
        _userRepository = userRepository;
        _auditService = auditService;
        _logger = logger;
        
        _codeLength = int.Parse(configuration["Mfa:BackupCodes:Length"] ?? "8");
        _defaultExpirationDays = int.Parse(configuration["Mfa:BackupCodes:ExpirationDays"] ?? "90");
    }

    public async Task<BackupCodeGenerationResult> GenerateBackupCodesAsync(string userId, int count = 10)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new BackupCodeGenerationResult
            {
                Success = false,
                ErrorMessage = "User ID cannot be null or empty"
            };
        }

        if (count <= 0 || count > 20)
        {
            return new BackupCodeGenerationResult
            {
                Success = false,
                ErrorMessage = "Backup code count must be between 1 and 20"
            };
        }

        try
        {
            // 檢查使用者是否存在
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return new BackupCodeGenerationResult
                {
                    Success = false,
                    ErrorMessage = "User not found"
                };
            }

            // 撤銷舊的備用碼
            var existingCodes = await _backupCodeRepository.GetAvailableBackupCodesAsync(userId);
            var previousCodesRevoked = 0;
            
            foreach (var existingCode in existingCodes)
            {
                existingCode.IsUsed = true;
                existingCode.UsedAt = DateTime.UtcNow;
                await _backupCodeRepository.UpdateAsync(existingCode);
                previousCodesRevoked++;
            }

            // 產生新的備用碼
            var batchId = Guid.NewGuid().ToString("N");
            var expiresAt = DateTime.UtcNow.AddDays(_defaultExpirationDays);
            var plainTextCodes = new List<string>();
            var backupCodeEntities = new List<MfaBackupCodeEntity>();

            for (int i = 0; i < count; i++)
            {
                var plainCode = GenerateBackupCode();
                plainTextCodes.Add(plainCode);

                var backupCodeEntity = new MfaBackupCodeEntity
                {
                    UserId = userId,
                    CodeHash = BCrypt.Net.BCrypt.HashPassword(plainCode),
                    BatchId = batchId,
                    ExpiresAt = expiresAt,
                    IsUsed = false
                };

                backupCodeEntities.Add(backupCodeEntity);
            }

            // 儲存到資料庫
            foreach (var entity in backupCodeEntities)
            {
                await _backupCodeRepository.CreateAsync(entity);
            }

            // 記錄審計日誌
            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                EventType = MfaEventTypes.BackupCodeGenerated,
                Method = "BackupCode",
                Result = MfaResults.Success,
                Description = $"Generated {count} backup codes, revoked {previousCodesRevoked} previous codes",
                AdditionalData = new Dictionary<string, object>
                {
                    ["BatchId"] = batchId,
                    ["Count"] = count,
                    ["PreviousCodesRevoked"] = previousCodesRevoked,
                    ["ExpiresAt"] = expiresAt
                }
            });

            _logger.LogInformation("Generated {Count} backup codes for user: {UserId}, batch: {BatchId}", 
                count, userId, batchId);

            return new BackupCodeGenerationResult
            {
                Success = true,
                BackupCodes = plainTextCodes.ToArray(),
                BatchId = batchId,
                ExpiresAt = expiresAt,
                PreviousCodesRevoked = previousCodesRevoked
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate backup codes for user: {UserId}", userId);
            
            return new BackupCodeGenerationResult
            {
                Success = false,
                ErrorMessage = "Failed to generate backup codes"
            };
        }
    }

    public async Task<BackupCodeVerificationResult> VerifyBackupCodeAsync(string userId, string code, MfaVerificationContext context)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new BackupCodeVerificationResult
            {
                IsValid = false,
                ErrorMessage = "User ID cannot be null or empty"
            };
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return new BackupCodeVerificationResult
            {
                IsValid = false,
                ErrorMessage = "Backup code cannot be null or empty"
            };
        }

        if (!ValidateBackupCodeFormat(code))
        {
            return new BackupCodeVerificationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid backup code format"
            };
        }

        try
        {
            // 取得使用者的可用備用碼
            var availableCodes = await _backupCodeRepository.GetAvailableBackupCodesAsync(userId);
            
            MfaBackupCodeEntity? matchedCode = null;
            
            // 驗證每個可用的備用碼
            foreach (var backupCode in availableCodes)
            {
                if (BCrypt.Net.BCrypt.Verify(code, backupCode.CodeHash))
                {
                    matchedCode = backupCode;
                    break;
                }
            }

            if (matchedCode == null)
            {
                // 記錄失敗的審計日誌
                await _auditService.LogMfaEventAsync(new MfaAuditEvent
                {
                    UserId = userId,
                    EventType = MfaEventTypes.Verify,
                    Method = "BackupCode",
                    Result = MfaResults.Failed,
                    FailureReason = "InvalidCode",
                    Description = "Invalid backup code provided",
                    IpAddress = context.IpAddress,
                    UserAgent = context.UserAgent,
                    ClientId = context.ClientId,
                    SessionId = context.SessionId
                });

                return new BackupCodeVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid or expired backup code"
                };
            }

            // 檢查是否已過期
            if (matchedCode.ExpiresAt <= DateTime.UtcNow)
            {
                await _auditService.LogMfaEventAsync(new MfaAuditEvent
                {
                    UserId = userId,
                    EventType = MfaEventTypes.Verify,
                    Method = "BackupCode",
                    Result = MfaResults.Failed,
                    FailureReason = "Expired",
                    Description = "Expired backup code used",
                    IpAddress = context.IpAddress,
                    UserAgent = context.UserAgent,
                    ClientId = context.ClientId,
                    SessionId = context.SessionId
                });

                return new BackupCodeVerificationResult
                {
                    IsValid = false,
                    IsExpired = true,
                    ErrorMessage = "Backup code has expired"
                };
            }

            // 標記備用碼為已使用
            matchedCode.IsUsed = true;
            matchedCode.UsedAt = DateTime.UtcNow;
            matchedCode.UsedFromIp = context.IpAddress;
            matchedCode.UsedFromUserAgent = context.UserAgent;
            
            await _backupCodeRepository.UpdateAsync(matchedCode);

            // 計算剩餘備用碼數量
            var remainingCodes = await GetAvailableBackupCodesCountAsync(userId);

            // 記錄成功的審計日誌
            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                EventType = MfaEventTypes.BackupCodeUsed,
                Method = "BackupCode",
                Result = MfaResults.Success,
                Description = $"Backup code used successfully, {remainingCodes} codes remaining",
                IpAddress = context.IpAddress,
                UserAgent = context.UserAgent,
                ClientId = context.ClientId,
                SessionId = context.SessionId,
                AdditionalData = new Dictionary<string, object>
                {
                    ["RemainingCodes"] = remainingCodes,
                    ["BatchId"] = matchedCode.BatchId
                }
            });

            _logger.LogInformation("Backup code used successfully for user: {UserId}, remaining codes: {RemainingCodes}", 
                userId, remainingCodes);

            return new BackupCodeVerificationResult
            {
                IsValid = true,
                RemainingCodes = remainingCodes,
                UsedAt = matchedCode.UsedAt,
                UsedFromIp = matchedCode.UsedFromIp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify backup code for user: {UserId}", userId);
            
            return new BackupCodeVerificationResult
            {
                IsValid = false,
                ErrorMessage = "Backup code verification failed"
            };
        }
    }

    public async Task<int> GetAvailableBackupCodesCountAsync(string userId)
    {
        try
        {
            return await _backupCodeRepository.GetAvailableBackupCodesCountAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available backup codes count for user: {UserId}", userId);
            return 0;
        }
    }

    public async Task<BackupCodeStatus> GetBackupCodeStatusAsync(string userId)
    {
        try
        {
            var allCodes = await _backupCodeRepository.GetUserBackupCodesAsync(userId);
            var availableCodes = allCodes.Where(c => !c.IsUsed && c.ExpiresAt > DateTime.UtcNow);
            var usedCodes = allCodes.Where(c => c.IsUsed);
            var expiredCodes = allCodes.Where(c => !c.IsUsed && c.ExpiresAt <= DateTime.UtcNow);

            var latestBatch = allCodes.OrderByDescending(c => c.CreatedAt).FirstOrDefault();

            return new BackupCodeStatus
            {
                TotalGenerated = allCodes.Count(),
                AvailableCodes = availableCodes.Count(),
                UsedCodes = usedCodes.Count(),
                LastGeneratedAt = latestBatch?.CreatedAt,
                LastUsedAt = usedCodes.OrderByDescending(c => c.UsedAt).FirstOrDefault()?.UsedAt,
                CurrentBatchId = latestBatch?.BatchId,
                ExpiresAt = latestBatch?.ExpiresAt,
                HasExpiredCodes = expiredCodes.Any()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup code status for user: {UserId}", userId);
            return new BackupCodeStatus();
        }
    }

    public async Task<bool> RevokeAllBackupCodesAsync(string userId, string adminUserId, string reason)
    {
        try
        {
            var availableCodes = await _backupCodeRepository.GetAvailableBackupCodesAsync(userId);
            
            foreach (var code in availableCodes)
            {
                code.IsUsed = true;
                code.UsedAt = DateTime.UtcNow;
                await _backupCodeRepository.UpdateAsync(code);
            }

            // 記錄審計日誌
            await _auditService.LogMfaEventAsync(new MfaAuditEvent
            {
                UserId = userId,
                EventType = "BackupCodesRevoked",
                Method = "BackupCode",
                Result = MfaResults.Success,
                Description = $"All backup codes revoked by admin: {adminUserId}. Reason: {reason}",
                AdditionalData = new Dictionary<string, object>
                {
                    ["AdminUserId"] = adminUserId,
                    ["Reason"] = reason,
                    ["CodesRevoked"] = availableCodes.Count()
                }
            });

            _logger.LogInformation("All backup codes revoked for user: {UserId} by admin: {AdminUserId}", 
                userId, adminUserId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke backup codes for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<int> CleanupExpiredBackupCodesAsync()
    {
        try
        {
            var expiredCodes = await _backupCodeRepository.GetExpiredBackupCodesAsync();
            var cleanedCount = 0;

            foreach (var code in expiredCodes)
            {
                await _backupCodeRepository.DeleteAsync(code.Id);
                cleanedCount++;
            }

            _logger.LogInformation("Cleaned up {Count} expired backup codes", cleanedCount);
            
            return cleanedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired backup codes");
            return 0;
        }
    }

    public string GenerateBackupCode()
    {
        try
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            
            var randomNumber = BitConverter.ToUInt32(bytes, 0);
            var code = (randomNumber % (uint)Math.Pow(10, _codeLength)).ToString($"D{_codeLength}");
            
            return code;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate backup code");
            throw new InvalidOperationException("Failed to generate backup code", ex);
        }
    }

    public bool ValidateBackupCodeFormat(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        // 檢查是否為指定長度的數字
        return System.Text.RegularExpressions.Regex.IsMatch(code, $@"^\d{{{_codeLength}}}$");
    }
}