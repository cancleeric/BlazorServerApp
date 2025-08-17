using System.ComponentModel.DataAnnotations;

namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// LDAP 配置實體
/// </summary>
public class LdapConfiguration : TenantAwareEntity
{
    /// <summary>
    /// 配置名稱
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 配置描述
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// LDAP 伺服器類型
    /// </summary>
    public LdapServerType ServerType { get; set; } = LdapServerType.ActiveDirectory;

    /// <summary>
    /// LDAP 伺服器位址
    /// </summary>
    [Required]
    [StringLength(500)]
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// LDAP 連接埠
    /// </summary>
    public int Port { get; set; } = 389;

    /// <summary>
    /// 是否使用 SSL/TLS
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// 是否使用 StartTLS
    /// </summary>
    public bool UseStartTls { get; set; } = false;

    /// <summary>
    /// 是否忽略 SSL 憑證錯誤
    /// </summary>
    public bool IgnoreSslErrors { get; set; } = false;

    /// <summary>
    /// 基礎 DN (Distinguished Name)
    /// </summary>
    [Required]
    [StringLength(500)]
    public string BaseDn { get; set; } = string.Empty;

    /// <summary>
    /// 使用者搜尋基礎 DN
    /// </summary>
    [StringLength(500)]
    public string? UserBaseDn { get; set; }

    /// <summary>
    /// 群組搜尋基礎 DN
    /// </summary>
    [StringLength(500)]
    public string? GroupBaseDn { get; set; }

    /// <summary>
    /// 使用者搜尋篩選器
    /// </summary>
    [StringLength(500)]
    public string UserSearchFilter { get; set; } = "(&(objectClass=user)(sAMAccountName={0}))";

    /// <summary>
    /// 群組搜尋篩選器
    /// </summary>
    [StringLength(500)]
    public string GroupSearchFilter { get; set; } = "(objectClass=group)";

    /// <summary>
    /// 使用者名稱屬性
    /// </summary>
    [StringLength(100)]
    public string UsernameAttribute { get; set; } = "sAMAccountName";

    /// <summary>
    /// 電子郵件屬性
    /// </summary>
    [StringLength(100)]
    public string EmailAttribute { get; set; } = "mail";

    /// <summary>
    /// 名字屬性
    /// </summary>
    [StringLength(100)]
    public string FirstNameAttribute { get; set; } = "givenName";

    /// <summary>
    /// 姓氏屬性
    /// </summary>
    [StringLength(100)]
    public string LastNameAttribute { get; set; } = "sn";

    /// <summary>
    /// 顯示名稱屬性
    /// </summary>
    [StringLength(100)]
    public string DisplayNameAttribute { get; set; } = "displayName";

    /// <summary>
    /// 群組名稱屬性
    /// </summary>
    [StringLength(100)]
    public string GroupNameAttribute { get; set; } = "cn";

    /// <summary>
    /// 群組成員屬性
    /// </summary>
    [StringLength(100)]
    public string GroupMemberAttribute { get; set; } = "member";

    /// <summary>
    /// 認證方式
    /// </summary>
    public LdapAuthenticationType AuthenticationType { get; set; } = LdapAuthenticationType.Simple;

    /// <summary>
    /// 服務帳號 DN
    /// </summary>
    [StringLength(500)]
    public string? ServiceAccountDn { get; set; }

    /// <summary>
    /// 服務帳號密碼 (加密儲存)
    /// </summary>
    [StringLength(500)]
    public string? ServiceAccountPassword { get; set; }

    /// <summary>
    /// 連線逾時 (秒)
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// 搜尋逾時 (秒)
    /// </summary>
    public int SearchTimeout { get; set; } = 30;

    /// <summary>
    /// 最大連線數
    /// </summary>
    public int MaxConnections { get; set; } = 10;

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 同步頻率 (分鐘)
    /// </summary>
    public int SyncFrequencyMinutes { get; set; } = 60;

    /// <summary>
    /// 是否啟用增量同步
    /// </summary>
    public bool EnableIncrementalSync { get; set; } = true;

    /// <summary>
    /// 衝突解決策略
    /// </summary>
    public ConflictResolutionStrategy ConflictResolution { get; set; } = ConflictResolutionStrategy.LdapWins;

    /// <summary>
    /// 是否同步停用的使用者
    /// </summary>
    public bool SyncDisabledUsers { get; set; } = false;

    /// <summary>
    /// 是否同步群組
    /// </summary>
    public bool SyncGroups { get; set; } = true;

    /// <summary>
    /// 是否同步嵌套群組
    /// </summary>
    public bool SyncNestedGroups { get; set; } = true;

    /// <summary>
    /// 最大嵌套層級
    /// </summary>
    public int MaxNestingLevel { get; set; } = 10;

    /// <summary>
    /// 額外屬性映射 (JSON 格式)
    /// </summary>
    public string? AdditionalAttributeMapping { get; set; }

    /// <summary>
    /// 最後同步時間
    /// </summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>
    /// 最後同步狀態
    /// </summary>
    public LdapSyncStatus LastSyncStatus { get; set; } = LdapSyncStatus.Never;

    /// <summary>
    /// 最後同步錯誤訊息
    /// </summary>
    public string? LastSyncError { get; set; }

    /// <summary>
    /// 同步統計資訊 (JSON 格式)
    /// </summary>
    public string? SyncStatistics { get; set; }

    // 業務邏輯方法

    /// <summary>
    /// 取得完整的 LDAP URL
    /// </summary>
    public string GetLdapUrl()
    {
        var protocol = UseSsl ? "ldaps" : "ldap";
        var defaultPort = UseSsl ? 636 : 389;
        
        if (Port != defaultPort)
        {
            return $"{protocol}://{ServerUrl}:{Port}";
        }
        
        return $"{protocol}://{ServerUrl}";
    }

    /// <summary>
    /// 取得使用者搜尋基礎 DN
    /// </summary>
    public string GetUserSearchBaseDn()
    {
        return !string.IsNullOrEmpty(UserBaseDn) ? UserBaseDn : BaseDn;
    }

    /// <summary>
    /// 取得群組搜尋基礎 DN
    /// </summary>
    public string GetGroupSearchBaseDn()
    {
        return !string.IsNullOrEmpty(GroupBaseDn) ? GroupBaseDn : BaseDn;
    }

    /// <summary>
    /// 檢查是否需要認證
    /// </summary>
    public bool RequiresAuthentication()
    {
        return AuthenticationType != LdapAuthenticationType.Anonymous;
    }

    /// <summary>
    /// 檢查連線設定是否有效
    /// </summary>
    public bool IsValidConfiguration()
    {
        if (string.IsNullOrEmpty(ServerUrl) || string.IsNullOrEmpty(BaseDn))
            return false;

        if (RequiresAuthentication() && string.IsNullOrEmpty(ServiceAccountDn))
            return false;

        return true;
    }

    /// <summary>
    /// 檢查是否應該同步
    /// </summary>
    public bool ShouldSync()
    {
        if (!IsEnabled || IsDeleted)
            return false;

        if (!LastSyncAt.HasValue)
            return true;

        var nextSyncTime = LastSyncAt.Value.AddMinutes(SyncFrequencyMinutes);
        return DateTime.UtcNow >= nextSyncTime;
    }

    /// <summary>
    /// 更新同步狀態
    /// </summary>
    public void UpdateSyncStatus(LdapSyncStatus status, string? error = null, string? statistics = null)
    {
        LastSyncAt = DateTime.UtcNow;
        LastSyncStatus = status;
        LastSyncError = error;
        
        if (!string.IsNullOrEmpty(statistics))
        {
            SyncStatistics = statistics;
        }
        
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 清理敏感資訊 (用於日誌)
    /// </summary>
    public LdapConfiguration GetSanitizedCopy()
    {
        var copy = new LdapConfiguration();
        
        // 複製所有屬性除了敏感資訊
        copy.Id = Id;
        copy.Name = Name;
        copy.Description = Description;
        copy.ServerType = ServerType;
        copy.ServerUrl = ServerUrl;
        copy.Port = Port;
        copy.BaseDn = BaseDn;
        copy.ServiceAccountDn = ServiceAccountDn;
        copy.ServiceAccountPassword = "***"; // 隱藏密碼
        
        return copy;
    }
}