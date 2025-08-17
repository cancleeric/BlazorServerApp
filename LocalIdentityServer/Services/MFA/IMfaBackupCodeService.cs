namespace LocalIdentityServer.Services.MFA;

/// <summary>
/// MFA 備用碼服務介面
/// 負責產生、驗證和管理 MFA 備用恢復碼
/// </summary>
public interface IMfaBackupCodeService
{
    /// <summary>
    /// 為使用者產生新的備用碼
    /// </summary>
    /// <param name="userId">使用者ID</param>
    /// <param name="count">產生的備用碼數量 (預設10個)</param>
    /// <returns>備用碼產生結果</returns>
    Task<BackupCodeGenerationResult> GenerateBackupCodesAsync(string userId, int count = 10);

    /// <summary>
    /// 驗證備用碼
    /// </summary>
    /// <param name="userId">使用者ID</param>
    /// <param name="code">8位數備用碼</param>
    /// <param name="context">驗證上下文</param>
    /// <returns>驗證結果</returns>
    Task<BackupCodeVerificationResult> VerifyBackupCodeAsync(string userId, string code, MfaVerificationContext context);

    /// <summary>
    /// 取得使用者的可用備用碼數量
    /// </summary>
    /// <param name="userId">使用者ID</param>
    /// <returns>可用備用碼數量</returns>
    Task<int> GetAvailableBackupCodesCountAsync(string userId);

    /// <summary>
    /// 取得使用者的備用碼狀態
    /// </summary>
    /// <param name="userId">使用者ID</param>
    /// <returns>備用碼狀態資訊</returns>
    Task<BackupCodeStatus> GetBackupCodeStatusAsync(string userId);

    /// <summary>
    /// 撤銷使用者的所有備用碼
    /// </summary>
    /// <param name="userId">使用者ID</param>
    /// <param name="adminUserId">管理員使用者ID</param>
    /// <param name="reason">撤銷原因</param>
    /// <returns>撤銷結果</returns>
    Task<bool> RevokeAllBackupCodesAsync(string userId, string adminUserId, string reason);

    /// <summary>
    /// 清理過期的備用碼
    /// </summary>
    /// <returns>清理的備用碼數量</returns>
    Task<int> CleanupExpiredBackupCodesAsync();

    /// <summary>
    /// 產生單一 8 位數備用碼
    /// </summary>
    /// <returns>8位數備用碼</returns>
    string GenerateBackupCode();

    /// <summary>
    /// 驗證備用碼格式
    /// </summary>
    /// <param name="code">備用碼</param>
    /// <returns>是否為有效格式</returns>
    bool ValidateBackupCodeFormat(string code);
}

/// <summary>
/// 備用碼產生結果
/// </summary>
public class BackupCodeGenerationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string[] BackupCodes { get; set; } = Array.Empty<string>();
    public string BatchId { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public int PreviousCodesRevoked { get; set; }
}

/// <summary>
/// 備用碼驗證結果
/// </summary>
public class BackupCodeVerificationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsUsed { get; set; }
    public bool IsExpired { get; set; }
    public int RemainingCodes { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedFromIp { get; set; }
}

/// <summary>
/// 備用碼狀態
/// </summary>
public class BackupCodeStatus
{
    public int TotalGenerated { get; set; }
    public int AvailableCodes { get; set; }
    public int UsedCodes { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? CurrentBatchId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool HasExpiredCodes { get; set; }
}