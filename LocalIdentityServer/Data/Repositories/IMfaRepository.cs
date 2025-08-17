using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// MFA 方法儲存庫介面
/// 負責使用者 MFA 方法的資料存取操作
/// </summary>
public interface IMfaRepository
{
    /// <summary>
    /// 建立新的 MFA 方法
    /// </summary>
    Task<UserMfaEntity> CreateAsync(UserMfaEntity mfaMethod);

    /// <summary>
    /// 更新 MFA 方法
    /// </summary>
    Task<UserMfaEntity> UpdateAsync(UserMfaEntity mfaMethod);

    /// <summary>
    /// 刪除 MFA 方法
    /// </summary>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// 根據 ID 取得 MFA 方法
    /// </summary>
    Task<UserMfaEntity?> GetByIdAsync(string id);

    /// <summary>
    /// 取得使用者的所有 MFA 方法
    /// </summary>
    Task<IEnumerable<UserMfaEntity>> GetByUserIdAsync(string userId);

    /// <summary>
    /// 取得使用者的特定 MFA 方法
    /// </summary>
    Task<UserMfaEntity?> GetByUserIdAndMethodAsync(string userId, string method);

    /// <summary>
    /// 取得使用者的主要 MFA 方法
    /// </summary>
    Task<UserMfaEntity?> GetPrimaryMfaMethodAsync(string userId);

    /// <summary>
    /// 取得使用者的已啟用 MFA 方法
    /// </summary>
    Task<IEnumerable<UserMfaEntity>> GetEnabledMfaMethodsAsync(string userId);

    /// <summary>
    /// 檢查使用者是否已啟用 MFA
    /// </summary>
    Task<bool> HasEnabledMfaAsync(string userId);

    /// <summary>
    /// 取得鎖定的 MFA 方法
    /// </summary>
    Task<IEnumerable<UserMfaEntity>> GetLockedMfaMethodsAsync(string? userId = null);

    /// <summary>
    /// 解鎖過期的 MFA 方法
    /// </summary>
    Task<int> UnlockExpiredMfaMethodsAsync();

    /// <summary>
    /// 取得 MFA 統計資訊
    /// </summary>
    Task<MfaStatistics> GetMfaStatisticsAsync();
}

/// <summary>
/// MFA 統計資訊
/// </summary>
public class MfaStatistics
{
    public int TotalMfaUsers { get; set; }
    public int TotpUsers { get; set; }
    public int SmsUsers { get; set; }
    public int EmailUsers { get; set; }
    public int LockedMethods { get; set; }
    public Dictionary<string, int> MethodCounts { get; set; } = new();
}