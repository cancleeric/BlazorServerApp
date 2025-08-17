using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// MFA 備用碼儲存庫介面
/// 負責 MFA 備用碼的資料存取操作
/// </summary>
public interface IMfaBackupCodeRepository
{
    /// <summary>
    /// 建立新的備用碼
    /// </summary>
    Task<MfaBackupCodeEntity> CreateAsync(MfaBackupCodeEntity backupCode);

    /// <summary>
    /// 更新備用碼
    /// </summary>
    Task<MfaBackupCodeEntity> UpdateAsync(MfaBackupCodeEntity backupCode);

    /// <summary>
    /// 刪除備用碼
    /// </summary>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// 根據 ID 取得備用碼
    /// </summary>
    Task<MfaBackupCodeEntity?> GetByIdAsync(string id);

    /// <summary>
    /// 取得使用者的所有備用碼
    /// </summary>
    Task<IEnumerable<MfaBackupCodeEntity>> GetUserBackupCodesAsync(string userId);

    /// <summary>
    /// 取得使用者的可用備用碼
    /// </summary>
    Task<IEnumerable<MfaBackupCodeEntity>> GetAvailableBackupCodesAsync(string userId);

    /// <summary>
    /// 取得使用者的可用備用碼數量
    /// </summary>
    Task<int> GetAvailableBackupCodesCountAsync(string userId);

    /// <summary>
    /// 取得特定批次的備用碼
    /// </summary>
    Task<IEnumerable<MfaBackupCodeEntity>> GetBackupCodesByBatchAsync(string batchId);

    /// <summary>
    /// 取得過期的備用碼
    /// </summary>
    Task<IEnumerable<MfaBackupCodeEntity>> GetExpiredBackupCodesAsync();

    /// <summary>
    /// 撤銷使用者的所有可用備用碼
    /// </summary>
    Task<int> RevokeAllUserBackupCodesAsync(string userId);

    /// <summary>
    /// 清理過期的備用碼
    /// </summary>
    Task<int> CleanupExpiredBackupCodesAsync(DateTime beforeDate);

    /// <summary>
    /// 取得備用碼使用統計
    /// </summary>
    Task<BackupCodeStatistics> GetBackupCodeStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
}

/// <summary>
/// 備用碼統計資訊
/// </summary>
public class BackupCodeStatistics
{
    public int TotalGenerated { get; set; }
    public int TotalUsed { get; set; }
    public int TotalExpired { get; set; }
    public int TotalAvailable { get; set; }
    public int UsersWithBackupCodes { get; set; }
    public DateTime? EarliestCreated { get; set; }
    public DateTime? LatestCreated { get; set; }
    public DateTime? LatestUsed { get; set; }
}