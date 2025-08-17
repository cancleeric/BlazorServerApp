using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// LDAP 配置資料存取介面
/// </summary>
public interface ILdapConfigurationRepository
{
    #region 基本 CRUD 操作

    /// <summary>
    /// 取得 LDAP 配置 (依 ID)
    /// </summary>
    Task<LdapConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的 LDAP 配置
    /// </summary>
    Task<LdapConfiguration?> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得 LDAP 配置 (依名稱)
    /// </summary>
    Task<LdapConfiguration?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立 LDAP 配置
    /// </summary>
    Task<LdapConfiguration> CreateAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新 LDAP 配置
    /// </summary>
    Task<LdapConfiguration> UpdateAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除 LDAP 配置
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    #endregion

    #region 查詢操作

    /// <summary>
    /// 取得所有 LDAP 配置
    /// </summary>
    Task<IEnumerable<LdapConfiguration>> GetAllAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得啟用的 LDAP 配置
    /// </summary>
    Task<IEnumerable<LdapConfiguration>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得停用的 LDAP 配置
    /// </summary>
    Task<IEnumerable<LdapConfiguration>> GetDisabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 依伺服器類型取得配置
    /// </summary>
    Task<IEnumerable<LdapConfiguration>> GetByServerTypeAsync(LdapServerType serverType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 搜尋 LDAP 配置
    /// </summary>
    Task<IEnumerable<LdapConfiguration>> SearchAsync(string searchTerm, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    #endregion

    #region 同步相關

    /// <summary>
    /// 取得需要同步的配置
    /// </summary>
    Task<IEnumerable<LdapConfiguration>> GetConfigurationsRequiringSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得依同步狀態的配置
    /// </summary>
    Task<IEnumerable<LdapConfiguration>> GetBySyncStatusAsync(LdapSyncStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新同步狀態
    /// </summary>
    Task UpdateSyncStatusAsync(Guid configurationId, LdapSyncStatus status, string? error = null, string? statistics = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新最後同步時間
    /// </summary>
    Task UpdateLastSyncTimeAsync(Guid configurationId, DateTime syncTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得同步失敗的配置
    /// </summary>
    Task<IEnumerable<LdapConfiguration>> GetFailedSyncConfigurationsAsync(DateTime since, CancellationToken cancellationToken = default);

    #endregion

    #region 驗證與測試

    /// <summary>
    /// 測試連線配置
    /// </summary>
    Task<bool> TestConnectionAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證配置完整性
    /// </summary>
    Task<bool> ValidateConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查配置名稱是否存在
    /// </summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查配置名稱是否存在 (排除指定配置)
    /// </summary>
    Task<bool> ExistsAsync(string name, Guid excludeConfigurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查租戶是否已有配置
    /// </summary>
    Task<bool> TenantHasConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default);

    #endregion

    #region 安全性

    /// <summary>
    /// 加密敏感資料
    /// </summary>
    Task<LdapConfiguration> EncryptSensitiveDataAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解密敏感資料
    /// </summary>
    Task<LdapConfiguration> DecryptSensitiveDataAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得清理過的配置 (隱藏敏感資訊)
    /// </summary>
    Task<LdapConfiguration> GetSanitizedConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default);

    #endregion

    #region 統計與監控

    /// <summary>
    /// 取得配置總數
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得啟用配置數量
    /// </summary>
    Task<int> GetEnabledCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得同步統計資訊
    /// </summary>
    Task<Dictionary<string, object>> GetSyncStatisticsAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有配置的統計資訊
    /// </summary>
    Task<Dictionary<string, object>> GetOverallStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得健康狀態摘要
    /// </summary>
    Task<Dictionary<string, object>> GetHealthSummaryAsync(CancellationToken cancellationToken = default);

    #endregion

    #region 批次操作

    /// <summary>
    /// 批次更新同步狀態
    /// </summary>
    Task<int> BatchUpdateSyncStatusAsync(IEnumerable<(Guid ConfigurationId, LdapSyncStatus Status, string? Error)> updates, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次啟用配置
    /// </summary>
    Task<int> BatchEnableAsync(IEnumerable<Guid> configurationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次停用配置
    /// </summary>
    Task<int> BatchDisableAsync(IEnumerable<Guid> configurationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次刪除配置
    /// </summary>
    Task<int> BatchDeleteAsync(IEnumerable<Guid> configurationIds, CancellationToken cancellationToken = default);

    #endregion

    #region 設定管理

    /// <summary>
    /// 匯出配置 (排除敏感資訊)
    /// </summary>
    Task<string> ExportConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 匯入配置
    /// </summary>
    Task<LdapConfiguration> ImportConfigurationAsync(string configurationData, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 複製配置
    /// </summary>
    Task<LdapConfiguration> CloneConfigurationAsync(Guid sourceConfigurationId, string newName, Guid? newTenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重設配置到預設值
    /// </summary>
    Task<LdapConfiguration> ResetToDefaultsAsync(Guid configurationId, LdapServerType serverType, CancellationToken cancellationToken = default);

    #endregion
}