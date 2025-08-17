namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 群組角色映射
/// </summary>
public class GroupRoleMapping : TenantAwareEntity
{
    /// <summary>
    /// 群組 ID
    /// </summary>
    public Guid GroupId { get; set; }

    /// <summary>
    /// 群組
    /// </summary>
    public Group Group { get; set; } = null!;

    /// <summary>
    /// 角色 ID
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public Role Role { get; set; } = null!;

    /// <summary>
    /// 是否為活躍映射
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 映射優先級 (數字越大優先級越高)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// 是否繼承到子群組
    /// </summary>
    public bool InheritToChildGroups { get; set; } = false;

    /// <summary>
    /// 映射到期時間
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 映射條件 (JSON 格式的額外條件)
    /// </summary>
    public string? MappingConditions { get; set; }

    /// <summary>
    /// 映射來源 (Manual, LDAP, System)
    /// </summary>
    public string? MappingSource { get; set; }

    /// <summary>
    /// 外部識別碼
    /// </summary>
    public string? ExternalIdentifier { get; set; }

    /// <summary>
    /// 映射描述或原因
    /// </summary>
    public string? Description { get; set; }

    // 業務邏輯方法

    /// <summary>
    /// 檢查映射是否有效
    /// </summary>
    public bool IsValidMapping()
    {
        return IsActive && 
               !IsDeleted &&
               (!ExpiresAt.HasValue || ExpiresAt.Value > DateTime.UtcNow);
    }

    /// <summary>
    /// 檢查映射是否過期
    /// </summary>
    public bool IsExpired()
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    }

    /// <summary>
    /// 延長映射
    /// </summary>
    public void ExtendMapping(DateTime newExpiryDate)
    {
        ExpiresAt = newExpiryDate;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 停用映射
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 啟用映射
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 檢查是否應該繼承給指定群組
    /// </summary>
    public bool ShouldInheritToGroup(Group targetGroup)
    {
        if (!InheritToChildGroups || !IsValidMapping())
            return false;

        // 檢查目標群組是否為此映射群組的子群組
        return Group.ContainsGroup(targetGroup.Id);
    }

    /// <summary>
    /// 檢查是否來自 LDAP
    /// </summary>
    public bool IsFromLdap()
    {
        return MappingSource == "LDAP" || !string.IsNullOrEmpty(ExternalIdentifier);
    }

    /// <summary>
    /// 檢查是否為手動映射
    /// </summary>
    public bool IsManualMapping()
    {
        return MappingSource == "Manual" || string.IsNullOrEmpty(MappingSource);
    }

    /// <summary>
    /// 檢查是否為系統映射
    /// </summary>
    public bool IsSystemMapping()
    {
        return MappingSource == "System";
    }
}