using System.ComponentModel.DataAnnotations;

namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 群組實體
/// </summary>
public class Group : TenantAwareEntity
{
    /// <summary>
    /// 群組名稱
    /// </summary>
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 群組顯示名稱
    /// </summary>
    [StringLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 群組描述
    /// </summary>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// 群組類型
    /// </summary>
    public GroupType GroupType { get; set; } = GroupType.Security;

    /// <summary>
    /// 是否為系統群組
    /// </summary>
    public bool IsSystemGroup { get; set; } = false;

    /// <summary>
    /// 父群組 ID (支援嵌套群組)
    /// </summary>
    public Guid? ParentGroupId { get; set; }

    /// <summary>
    /// 父群組
    /// </summary>
    public Group? ParentGroup { get; set; }

    /// <summary>
    /// 子群組
    /// </summary>
    public ICollection<Group> ChildGroups { get; set; } = new List<Group>();

    // LDAP 相關屬性

    /// <summary>
    /// LDAP 辨別名稱 (Distinguished Name)
    /// </summary>
    [StringLength(500)]
    public string? LdapDistinguishedName { get; set; }

    /// <summary>
    /// LDAP 物件 GUID
    /// </summary>
    public string? LdapObjectGuid { get; set; }

    /// <summary>
    /// LDAP 安全識別碼 (SID)
    /// </summary>
    [StringLength(200)]
    public string? LdapSecurityIdentifier { get; set; }

    /// <summary>
    /// 是否來自 LDAP
    /// </summary>
    public bool IsFromLdap { get; set; } = false;

    /// <summary>
    /// 最後 LDAP 同步時間
    /// </summary>
    public DateTime? LastLdapSyncAt { get; set; }

    /// <summary>
    /// LDAP 同步雜湊 (用於偵測變更)
    /// </summary>
    [StringLength(64)]
    public string? LdapSyncHash { get; set; }

    // 導航屬性

    /// <summary>
    /// 群組成員
    /// </summary>
    public ICollection<UserGroupMembership> Members { get; set; } = new List<UserGroupMembership>();

    /// <summary>
    /// 群組角色映射
    /// </summary>
    public ICollection<GroupRoleMapping> RoleMappings { get; set; } = new List<GroupRoleMapping>();

    // 業務邏輯方法

    /// <summary>
    /// 取得顯示名稱
    /// </summary>
    public string GetDisplayName()
    {
        return !string.IsNullOrEmpty(DisplayName) ? DisplayName : Name;
    }

    /// <summary>
    /// 檢查是否為根群組
    /// </summary>
    public bool IsRootGroup()
    {
        return !ParentGroupId.HasValue;
    }

    /// <summary>
    /// 取得群組階層路徑
    /// </summary>
    public List<Group> GetHierarchyPath()
    {
        var path = new List<Group>();
        var current = this;
        
        while (current != null)
        {
            path.Insert(0, current);
            current = current.ParentGroup;
        }
        
        return path;
    }

    /// <summary>
    /// 取得所有子群組 (遞迴)
    /// </summary>
    public IEnumerable<Group> GetAllDescendants()
    {
        var descendants = new List<Group>();
        
        foreach (var child in ChildGroups)
        {
            descendants.Add(child);
            descendants.AddRange(child.GetAllDescendants());
        }
        
        return descendants;
    }

    /// <summary>
    /// 檢查是否包含指定群組 (考慮嵌套)
    /// </summary>
    public bool ContainsGroup(Guid groupId)
    {
        if (Id == groupId)
            return true;
            
        return GetAllDescendants().Any(g => g.Id == groupId);
    }

    /// <summary>
    /// 更新 LDAP 同步資訊
    /// </summary>
    public void UpdateLdapSync(string syncHash)
    {
        LastLdapSyncAt = DateTime.UtcNow;
        LdapSyncHash = syncHash;
    }

    /// <summary>
    /// 檢查是否需要 LDAP 同步
    /// </summary>
    public bool RequiresLdapSync(string currentHash)
    {
        return !IsFromLdap || 
               string.IsNullOrEmpty(LdapSyncHash) || 
               LdapSyncHash != currentHash ||
               !LastLdapSyncAt.HasValue ||
               LastLdapSyncAt.Value < DateTime.UtcNow.AddHours(-1);
    }

    /// <summary>
    /// 檢查是否為指定使用者的有效群組
    /// </summary>
    public bool IsValidForUser(Guid userId)
    {
        return Members.Any(m => m.UserId == userId && m.IsActive && !m.IsDeleted);
    }

    /// <summary>
    /// 添加成員
    /// </summary>
    public void AddMember(Guid userId, DateTime? expiresAt = null)
    {
        var existingMembership = Members.FirstOrDefault(m => m.UserId == userId);
        
        if (existingMembership != null)
        {
            existingMembership.IsActive = true;
            existingMembership.IsDeleted = false;
            existingMembership.ExpiresAt = expiresAt;
            existingMembership.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            Members.Add(new UserGroupMembership
            {
                UserId = userId,
                GroupId = Id,
                IsActive = true,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// 移除成員
    /// </summary>
    public void RemoveMember(Guid userId)
    {
        var membership = Members.FirstOrDefault(m => m.UserId == userId);
        if (membership != null)
        {
            membership.IsActive = false;
            membership.IsDeleted = true;
            membership.UpdatedAt = DateTime.UtcNow;
        }
    }
}