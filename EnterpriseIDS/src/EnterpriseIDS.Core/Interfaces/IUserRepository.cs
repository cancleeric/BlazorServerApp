using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// 使用者資料存取介面
/// </summary>
public interface IUserRepository
{
    #region 基本 CRUD 操作

    /// <summary>
    /// 取得使用者 (依 ID)
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者 (依使用者名稱)
    /// </summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者 (依電子郵件)
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者 (依 LDAP DN)
    /// </summary>
    Task<User?> GetByLdapDnAsync(string ldapDn, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者 (依 LDAP 物件 GUID)
    /// </summary>
    Task<User?> GetByLdapObjectGuidAsync(string ldapObjectGuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立使用者
    /// </summary>
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新使用者
    /// </summary>
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除使用者
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    #endregion

    #region 查詢操作

    /// <summary>
    /// 搜尋使用者
    /// </summary>
    Task<IEnumerable<User>> SearchAsync(string searchTerm, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有使用者
    /// </summary>
    Task<IEnumerable<User>> GetAllAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得活躍使用者
    /// </summary>
    Task<IEnumerable<User>> GetActiveUsersAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的使用者
    /// </summary>
    Task<IEnumerable<User>> GetByTenantAsync(Guid tenantId, int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得部門的使用者
    /// </summary>
    Task<IEnumerable<User>> GetByDepartmentAsync(string department, int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得主管的直屬下屬
    /// </summary>
    Task<IEnumerable<User>> GetDirectReportsAsync(Guid managerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得來自 LDAP 的使用者
    /// </summary>
    Task<IEnumerable<User>> GetLdapUsersAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得需要 LDAP 同步的使用者
    /// </summary>
    Task<IEnumerable<User>> GetUsersRequiringSyncAsync(DateTime olderThan, CancellationToken cancellationToken = default);

    #endregion

    #region 統計與計數

    /// <summary>
    /// 取得使用者總數
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得活躍使用者數量
    /// </summary>
    Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的使用者數量
    /// </summary>
    Task<int> GetCountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得 LDAP 使用者數量
    /// </summary>
    Task<int> GetLdapUserCountAsync(CancellationToken cancellationToken = default);

    #endregion

    #region 認證相關

    /// <summary>
    /// 更新最後登入時間
    /// </summary>
    Task UpdateLastLoginAsync(Guid userId, DateTime loginTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 增加失敗登入次數
    /// </summary>
    Task IncrementFailedLoginAttemptsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重設失敗登入次數
    /// </summary>
    Task ResetFailedLoginAttemptsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 鎖定使用者
    /// </summary>
    Task LockUserAsync(Guid userId, DateTime lockoutEndTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解鎖使用者
    /// </summary>
    Task UnlockUserAsync(Guid userId, CancellationToken cancellationToken = default);

    #endregion

    #region LDAP 同步相關

    /// <summary>
    /// 更新 LDAP 同步資訊
    /// </summary>
    Task UpdateLdapSyncInfoAsync(Guid userId, string syncHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次更新 LDAP 同步資訊
    /// </summary>
    Task BatchUpdateLdapSyncInfoAsync(IEnumerable<(Guid UserId, string SyncHash)> updates, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停用不存在於 LDAP 的使用者
    /// </summary>
    Task DeactivateOrphanedLdapUsersAsync(IEnumerable<string> activeLdapObjectGuids, CancellationToken cancellationToken = default);

    #endregion

    #region 批次操作

    /// <summary>
    /// 批次建立使用者
    /// </summary>
    Task<IEnumerable<User>> BatchCreateAsync(IEnumerable<User> users, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次更新使用者
    /// </summary>
    Task<IEnumerable<User>> BatchUpdateAsync(IEnumerable<User> users, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次刪除使用者
    /// </summary>
    Task<int> BatchDeleteAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    #endregion

    #region 驗證方法

    /// <summary>
    /// 檢查使用者名稱是否存在
    /// </summary>
    Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查電子郵件是否存在
    /// </summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查電子郵件是否存在 (排除指定使用者)
    /// </summary>
    Task<bool> EmailExistsAsync(string email, Guid excludeUserId, CancellationToken cancellationToken = default);

    #endregion
}