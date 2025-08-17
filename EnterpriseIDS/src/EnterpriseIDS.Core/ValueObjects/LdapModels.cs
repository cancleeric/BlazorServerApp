using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.ValueObjects;

/// <summary>
/// LDAP 使用者模型
/// </summary>
public class LdapUserModel
{
    public string DistinguishedName { get; set; } = string.Empty;
    public string ObjectGuid { get; set; } = string.Empty;
    public string SecurityIdentifier { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public string? PhoneNumber { get; set; }
    public string? MobileNumber { get; set; }
    public string? Office { get; set; }
    public string? ManagerDn { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public DateTime? PasswordLastSet { get; set; }
    public DateTime? AccountExpires { get; set; }
    public DateTime? LastLogon { get; set; }
    public List<string> GroupMemberships { get; set; } = new();
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();
}

/// <summary>
/// LDAP 群組模型
/// </summary>
public class LdapGroupModel
{
    public string DistinguishedName { get; set; } = string.Empty;
    public string ObjectGuid { get; set; } = string.Empty;
    public string SecurityIdentifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string GroupType { get; set; } = string.Empty;
    public List<string> Members { get; set; } = new();
    public List<string> NestedGroups { get; set; } = new();
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();
}

/// <summary>
/// LDAP 連線參數
/// </summary>
public class LdapConnectionParameters
{
    public string ServerUrl { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; } = false;
    public bool UseStartTls { get; set; } = false;
    public bool IgnoreSslErrors { get; set; } = false;
    public string? ServiceAccountDn { get; set; }
    public string? ServiceAccountPassword { get; set; }
    public int ConnectionTimeout { get; set; } = 30;
    public int SearchTimeout { get; set; } = 30;
}

/// <summary>
/// LDAP 搜尋參數
/// </summary>
public class LdapSearchParameters
{
    public string BaseDn { get; set; } = string.Empty;
    public string Filter { get; set; } = string.Empty;
    public string[] Attributes { get; set; } = Array.Empty<string>();
    public int? MaxResults { get; set; }
    public int SearchScope { get; set; } = 2; // Subtree
}

/// <summary>
/// LDAP 同步結果
/// </summary>
public class LdapSyncResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int UsersProcessed { get; set; }
    public int UsersAdded { get; set; }
    public int UsersUpdated { get; set; }
    public int UsersDeactivated { get; set; }
    public int GroupsProcessed { get; set; }
    public int GroupsAdded { get; set; }
    public int GroupsUpdated { get; set; }
    public int GroupsDeactivated { get; set; }
    public int MembershipsProcessed { get; set; }
    public int MembershipsAdded { get; set; }
    public int MembershipsRemoved { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}

/// <summary>
/// LDAP 認證結果
/// </summary>
public class LdapAuthenticationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public LdapUserModel? User { get; set; }
    public List<string> Groups { get; set; } = new();
    public Dictionary<string, object> AdditionalInfo { get; set; } = new();
}

/// <summary>
/// LDAP 連線測試結果
/// </summary>
public class LdapConnectionTestResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string ServerInfo { get; set; } = string.Empty;
    public List<string> SupportedFeatures { get; set; } = new();
    public Dictionary<string, object> ConnectionDetails { get; set; } = new();
}

/// <summary>
/// LDAP 健康檢查結果
/// </summary>
public class LdapHealthCheckResult
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public DateTime CheckTime { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// LDAP 衝突項目
/// </summary>
public class LdapConflictItem
{
    public string Type { get; set; } = string.Empty; // User, Group
    public string Identifier { get; set; } = string.Empty;
    public string ConflictReason { get; set; } = string.Empty;
    public object? LdapValue { get; set; }
    public object? LocalValue { get; set; }
    public DateTime DetectedAt { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// LDAP 同步選項
/// </summary>
public class LdapSyncOptions
{
    public bool SyncUsers { get; set; } = true;
    public bool SyncGroups { get; set; } = true;
    public bool SyncMemberships { get; set; } = true;
    public bool SyncDisabledUsers { get; set; } = false;
    public bool SyncNestedGroups { get; set; } = true;
    public bool DryRun { get; set; } = false;
    public int BatchSize { get; set; } = 100;
    public int MaxParallelism { get; set; } = 5;
    public string[]? SpecificUsers { get; set; }
    public string[]? SpecificGroups { get; set; }
    public DateTime? SyncFrom { get; set; }
    public ConflictResolutionStrategy ConflictResolution { get; set; } = ConflictResolutionStrategy.LdapWins;
}