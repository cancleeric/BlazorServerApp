namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// LDAP 伺服器類型
/// </summary>
public enum LdapServerType
{
    /// <summary>
    /// Microsoft Active Directory
    /// </summary>
    ActiveDirectory = 1,

    /// <summary>
    /// OpenLDAP
    /// </summary>
    OpenLdap = 2,

    /// <summary>
    /// Oracle Directory Server
    /// </summary>
    OracleDirectory = 3,

    /// <summary>
    /// 一般 LDAP 伺服器
    /// </summary>
    Generic = 4
}

/// <summary>
/// LDAP 認證方式
/// </summary>
public enum LdapAuthenticationType
{
    /// <summary>
    /// 匿名綁定
    /// </summary>
    Anonymous = 1,

    /// <summary>
    /// 簡單綁定
    /// </summary>
    Simple = 2,

    /// <summary>
    /// SASL 認證
    /// </summary>
    Sasl = 3
}

/// <summary>
/// LDAP 同步狀態
/// </summary>
public enum LdapSyncStatus
{
    /// <summary>
    /// 從未同步
    /// </summary>
    Never = 1,

    /// <summary>
    /// 同步中
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// 同步成功
    /// </summary>
    Success = 3,

    /// <summary>
    /// 同步失敗
    /// </summary>
    Failed = 4,

    /// <summary>
    /// 部分成功
    /// </summary>
    PartialSuccess = 5
}

/// <summary>
/// 使用者狀態
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// 啟用
    /// </summary>
    Active = 1,

    /// <summary>
    /// 停用
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// 鎖定
    /// </summary>
    Locked = 3,

    /// <summary>
    /// 密碼過期
    /// </summary>
    PasswordExpired = 4,

    /// <summary>
    /// 等待啟用
    /// </summary>
    PendingActivation = 5
}

/// <summary>
/// 群組類型
/// </summary>
public enum GroupType
{
    /// <summary>
    /// 安全群組
    /// </summary>
    Security = 1,

    /// <summary>
    /// 通訊群組
    /// </summary>
    Distribution = 2,

    /// <summary>
    /// 應用程式群組
    /// </summary>
    Application = 3
}

/// <summary>
/// 衝突解決策略
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// LDAP 優先
    /// </summary>
    LdapWins = 1,

    /// <summary>
    /// 本地優先
    /// </summary>
    LocalWins = 2,

    /// <summary>
    /// 手動解決
    /// </summary>
    Manual = 3,

    /// <summary>
    /// 略過衝突
    /// </summary>
    Skip = 4
}