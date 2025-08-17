using System.ComponentModel.DataAnnotations;

namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 角色權限
/// </summary>
public class RolePermission : TenantAwareEntity
{
    /// <summary>
    /// 角色 ID
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public Role Role { get; set; } = null!;

    /// <summary>
    /// 權限名稱 (如: users.read, users.write, admin.system)
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// 是否為活躍權限
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 權限來源 (System, LDAP, Custom)
    /// </summary>
    [StringLength(50)]
    public string? PermissionSource { get; set; }

    /// <summary>
    /// 權限範圍限制 (JSON 格式)
    /// </summary>
    public string? PermissionScope { get; set; }

    /// <summary>
    /// 權限條件 (JSON 格式的額外條件)
    /// </summary>
    public string? PermissionConditions { get; set; }

    /// <summary>
    /// 權限過期時間
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 外部識別碼
    /// </summary>
    [StringLength(200)]
    public string? ExternalIdentifier { get; set; }

    /// <summary>
    /// 權限描述
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    // 業務邏輯方法

    /// <summary>
    /// 檢查權限是否有效
    /// </summary>
    public bool IsValidPermission()
    {
        return IsActive && 
               !IsDeleted &&
               (!ExpiresAt.HasValue || ExpiresAt.Value > DateTime.UtcNow);
    }

    /// <summary>
    /// 檢查權限是否過期
    /// </summary>
    public bool IsExpired()
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    }

    /// <summary>
    /// 延長權限
    /// </summary>
    public void ExtendPermission(DateTime newExpiryDate)
    {
        ExpiresAt = newExpiryDate;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 停用權限
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 啟用權限
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 檢查是否為系統權限
    /// </summary>
    public bool IsSystemPermission()
    {
        return PermissionSource == "System" || Permission.StartsWith("system.");
    }

    /// <summary>
    /// 檢查是否來自 LDAP
    /// </summary>
    public bool IsFromLdap()
    {
        return PermissionSource == "LDAP" || !string.IsNullOrEmpty(ExternalIdentifier);
    }

    /// <summary>
    /// 取得權限類別
    /// </summary>
    public string GetPermissionCategory()
    {
        var parts = Permission.Split('.');
        return parts.Length > 0 ? parts[0] : "unknown";
    }

    /// <summary>
    /// 取得權限動作
    /// </summary>
    public string GetPermissionAction()
    {
        var parts = Permission.Split('.');
        return parts.Length > 1 ? parts[1] : "unknown";
    }

    /// <summary>
    /// 檢查權限是否匹配指定模式
    /// </summary>
    public bool MatchesPattern(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        // 支援萬用字元匹配
        if (pattern.EndsWith("*"))
        {
            var prefix = pattern.TrimEnd('*');
            return Permission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return Permission.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}