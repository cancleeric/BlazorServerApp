using System.ComponentModel.DataAnnotations;

namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 角色實體
/// </summary>
public class Role : TenantAwareEntity
{
    /// <summary>
    /// 角色名稱 (唯一識別)
    /// </summary>
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 角色顯示名稱
    /// </summary>
    [StringLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 角色描述
    /// </summary>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// 是否為系統角色 (無法刪除或修改)
    /// </summary>
    public bool IsSystemRole { get; set; } = false;

    /// <summary>
    /// 角色權重 (用於排序和層級控制)
    /// </summary>
    public int Weight { get; set; } = 0;

    /// <summary>
    /// 是否為預設角色 (新使用者自動分配)
    /// </summary>
    public bool IsDefaultRole { get; set; } = false;

    /// <summary>
    /// 角色顏色 (用於 UI 顯示)
    /// </summary>
    [StringLength(7)]
    public string? Color { get; set; }

    /// <summary>
    /// 角色圖示
    /// </summary>
    [StringLength(100)]
    public string? Icon { get; set; }

    // 導航屬性

    /// <summary>
    /// 使用者角色分配
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    /// <summary>
    /// 群組角色映射
    /// </summary>
    public ICollection<GroupRoleMapping> GroupMappings { get; set; } = new List<GroupRoleMapping>();

    /// <summary>
    /// 角色權限
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    // 業務邏輯方法

    /// <summary>
    /// 取得顯示名稱
    /// </summary>
    public string GetDisplayName()
    {
        return !string.IsNullOrEmpty(DisplayName) ? DisplayName : Name;
    }

    /// <summary>
    /// 檢查是否可以刪除
    /// </summary>
    public bool CanDelete()
    {
        return !IsSystemRole && !IsDefaultRole;
    }

    /// <summary>
    /// 檢查是否可以修改
    /// </summary>
    public bool CanModify()
    {
        return !IsSystemRole;
    }

    /// <summary>
    /// 取得角色的所有權限
    /// </summary>
    public IEnumerable<string> GetPermissions()
    {
        return RolePermissions
            .Where(rp => rp.IsActive && !rp.IsDeleted)
            .Select(rp => rp.Permission);
    }

    /// <summary>
    /// 檢查是否擁有指定權限
    /// </summary>
    public bool HasPermission(string permission)
    {
        return RolePermissions.Any(rp => 
            rp.Permission == permission && 
            rp.IsActive && 
            !rp.IsDeleted);
    }

    /// <summary>
    /// 添加權限
    /// </summary>
    public void AddPermission(string permission)
    {
        var existingPermission = RolePermissions.FirstOrDefault(rp => rp.Permission == permission);
        
        if (existingPermission != null)
        {
            existingPermission.IsActive = true;
            existingPermission.IsDeleted = false;
            existingPermission.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            RolePermissions.Add(new RolePermission
            {
                RoleId = Id,
                Permission = permission,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// 移除權限
    /// </summary>
    public void RemovePermission(string permission)
    {
        var rolePermission = RolePermissions.FirstOrDefault(rp => rp.Permission == permission);
        if (rolePermission != null)
        {
            rolePermission.IsActive = false;
            rolePermission.IsDeleted = true;
            rolePermission.UpdatedAt = DateTime.UtcNow;
        }
    }
}