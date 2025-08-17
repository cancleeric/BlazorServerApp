namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 使用者角色分配
/// </summary>
public class UserRole : TenantAwareEntity
{
    /// <summary>
    /// 使用者 ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 使用者
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 角色 ID
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public Role Role { get; set; } = null!;

    /// <summary>
    /// 是否為活躍分配
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 角色分配到期時間
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 分配來源 (Direct, Group, LDAP)
    /// </summary>
    public string? AssignmentSource { get; set; }

    /// <summary>
    /// 來源群組 ID (如果通過群組獲得)
    /// </summary>
    public Guid? SourceGroupId { get; set; }

    /// <summary>
    /// 外部識別碼
    /// </summary>
    public string? ExternalIdentifier { get; set; }

    /// <summary>
    /// 分配原因或註記
    /// </summary>
    public string? AssignmentReason { get; set; }

    // 業務邏輯方法

    /// <summary>
    /// 檢查角色分配是否有效
    /// </summary>
    public bool IsValidAssignment()
    {
        return IsActive && 
               !IsDeleted &&
               (!ExpiresAt.HasValue || ExpiresAt.Value > DateTime.UtcNow);
    }

    /// <summary>
    /// 檢查角色分配是否過期
    /// </summary>
    public bool IsExpired()
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    }

    /// <summary>
    /// 延長角色分配
    /// </summary>
    public void ExtendAssignment(DateTime newExpiryDate)
    {
        ExpiresAt = newExpiryDate;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 停用角色分配
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 啟用角色分配
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 檢查是否為直接分配 (非通過群組)
    /// </summary>
    public bool IsDirectAssignment()
    {
        return AssignmentSource == "Direct" || !SourceGroupId.HasValue;
    }

    /// <summary>
    /// 檢查是否為群組繼承
    /// </summary>
    public bool IsFromGroup()
    {
        return AssignmentSource == "Group" && SourceGroupId.HasValue;
    }

    /// <summary>
    /// 檢查是否來自 LDAP
    /// </summary>
    public bool IsFromLdap()
    {
        return AssignmentSource == "LDAP" || !string.IsNullOrEmpty(ExternalIdentifier);
    }
}