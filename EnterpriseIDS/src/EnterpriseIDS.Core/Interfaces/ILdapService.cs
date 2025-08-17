using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.ValueObjects;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// LDAP 服務介面
/// </summary>
public interface ILdapService
{
    #region 連線與配置管理

    /// <summary>
    /// 測試 LDAP 連線
    /// </summary>
    Task<LdapConnectionTestResult> TestConnectionAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 測試 LDAP 連線 (使用連線參數)
    /// </summary>
    Task<LdapConnectionTestResult> TestConnectionAsync(LdapConnectionParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查 LDAP 連線健康狀態
    /// </summary>
    Task<LdapHealthCheckResult> CheckHealthAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得 LDAP 伺服器資訊
    /// </summary>
    Task<Dictionary<string, object>> GetServerInfoAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default);

    #endregion

    #region 使用者認證

    /// <summary>
    /// 驗證使用者認證
    /// </summary>
    Task<LdapAuthenticationResult> AuthenticateAsync(string username, string password, Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證使用者認證 (使用特定配置)
    /// </summary>
    Task<LdapAuthenticationResult> AuthenticateAsync(string username, string password, LdapConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查使用者是否存在
    /// </summary>
    Task<bool> UserExistsAsync(string username, Guid configurationId, CancellationToken cancellationToken = default);

    #endregion

    #region 使用者操作

    /// <summary>
    /// 搜尋使用者
    /// </summary>
    Task<IEnumerable<LdapUserModel>> SearchUsersAsync(string searchTerm, Guid configurationId, int maxResults = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者詳細資訊
    /// </summary>
    Task<LdapUserModel?> GetUserAsync(string username, Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者 (使用 DN)
    /// </summary>
    Task<LdapUserModel?> GetUserByDnAsync(string distinguishedName, Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有使用者
    /// </summary>
    Task<IEnumerable<LdapUserModel>> GetAllUsersAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者的群組成員資格
    /// </summary>
    Task<IEnumerable<string>> GetUserGroupsAsync(string username, Guid configurationId, bool includeNested = true, CancellationToken cancellationToken = default);

    #endregion

    #region 群組操作

    /// <summary>
    /// 搜尋群組
    /// </summary>
    Task<IEnumerable<LdapGroupModel>> SearchGroupsAsync(string searchTerm, Guid configurationId, int maxResults = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組詳細資訊
    /// </summary>
    Task<LdapGroupModel?> GetGroupAsync(string groupName, Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組 (使用 DN)
    /// </summary>
    Task<LdapGroupModel?> GetGroupByDnAsync(string distinguishedName, Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有群組
    /// </summary>
    Task<IEnumerable<LdapGroupModel>> GetAllGroupsAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組成員
    /// </summary>
    Task<IEnumerable<string>> GetGroupMembersAsync(string groupName, Guid configurationId, bool includeNested = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組的子群組
    /// </summary>
    Task<IEnumerable<LdapGroupModel>> GetChildGroupsAsync(string groupName, Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組的父群組
    /// </summary>
    Task<IEnumerable<LdapGroupModel>> GetParentGroupsAsync(string groupName, Guid configurationId, CancellationToken cancellationToken = default);

    #endregion

    #region 同步操作

    /// <summary>
    /// 執行完整同步
    /// </summary>
    Task<LdapSyncResult> SynchronizeAsync(Guid configurationId, LdapSyncOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 執行增量同步
    /// </summary>
    Task<LdapSyncResult> IncrementalSyncAsync(Guid configurationId, DateTime fromDate, LdapSyncOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步特定使用者
    /// </summary>
    Task<LdapSyncResult> SyncUserAsync(string username, Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步特定群組
    /// </summary>
    Task<LdapSyncResult> SyncGroupAsync(string groupName, Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查並解決同步衝突
    /// </summary>
    Task<IEnumerable<LdapConflictItem>> DetectConflictsAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解決同步衝突
    /// </summary>
    Task<bool> ResolveConflictAsync(LdapConflictItem conflict, ConflictResolutionStrategy strategy, CancellationToken cancellationToken = default);

    #endregion

    #region 架構與屬性

    /// <summary>
    /// 取得 LDAP 架構資訊
    /// </summary>
    Task<Dictionary<string, object>> GetSchemaAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得可用的屬性清單
    /// </summary>
    Task<IEnumerable<string>> GetAvailableAttributesAsync(Guid configurationId, string objectClass, CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證屬性映射
    /// </summary>
    Task<bool> ValidateAttributeMappingAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default);

    #endregion

    #region 監控與統計

    /// <summary>
    /// 取得同步統計資訊
    /// </summary>
    Task<Dictionary<string, object>> GetSyncStatisticsAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得連線統計資訊
    /// </summary>
    Task<Dictionary<string, object>> GetConnectionStatisticsAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理過期的連線和快取
    /// </summary>
    Task CleanupExpiredResourcesAsync(CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// LDAP 配置服務介面
/// </summary>
public interface ILdapConfigurationService
{
    /// <summary>
    /// 取得租戶的 LDAP 配置
    /// </summary>
    Task<LdapConfiguration?> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得指定的 LDAP 配置
    /// </summary>
    Task<LdapConfiguration?> GetConfigurationByIdAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立或更新 LDAP 配置
    /// </summary>
    Task<LdapConfiguration> SaveConfigurationAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除 LDAP 配置
    /// </summary>
    Task<bool> DeleteConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有啟用的配置
    /// </summary>
    Task<IEnumerable<LdapConfiguration>> GetEnabledConfigurationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 加密敏感資訊
    /// </summary>
    Task<string> EncryptSensitiveDataAsync(string data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解密敏感資訊
    /// </summary>
    Task<string> DecryptSensitiveDataAsync(string encryptedData, CancellationToken cancellationToken = default);
}

/// <summary>
/// LDAP 同步服務介面
/// </summary>
public interface ILdapSyncService
{
    /// <summary>
    /// 取得需要同步的配置
    /// </summary>
    Task<IEnumerable<LdapConfiguration>> GetConfigurationsRequiringSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 執行排程同步
    /// </summary>
    Task ExecuteScheduledSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步使用者到本地資料庫
    /// </summary>
    Task<User> SyncUserToLocalAsync(LdapUserModel ldapUser, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步群組到本地資料庫
    /// </summary>
    Task<Group> SyncGroupToLocalAsync(LdapGroupModel ldapGroup, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步群組成員資格
    /// </summary>
    Task SyncGroupMembershipsAsync(LdapGroupModel ldapGroup, Group localGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// 處理角色映射
    /// </summary>
    Task ProcessRoleMappingsAsync(User user, List<string> ldapGroups, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停用不存在於 LDAP 的使用者
    /// </summary>
    Task DeactivateOrphanedUsersAsync(Guid configurationId, IEnumerable<string> activeLdapUsers, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停用不存在於 LDAP 的群組
    /// </summary>
    Task DeactivateOrphanedGroupsAsync(Guid configurationId, IEnumerable<string> activeLdapGroups, CancellationToken cancellationToken = default);
}