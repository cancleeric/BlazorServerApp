namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 使用者群組成員資格
/// </summary>
public class UserGroupMembership : TenantAwareEntity
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
    /// 群組 ID
    /// </summary>
    public Guid GroupId { get; set; }

    /// <summary>
    /// 群組
    /// </summary>
    public Group Group { get; set; } = null!;

    /// <summary>
    /// 是否為活躍成員
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 成員資格到期時間
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 是否為群組管理員
    /// </summary>
    public bool IsGroupAdmin { get; set; } = false;

    /// <summary>
    /// 來源類型 (LDAP, Manual, System)
    /// </summary>
    public string? SourceType { get; set; }

    /// <summary>
    /// 外部識別碼 (LDAP DN 等)
    /// </summary>
    public string? ExternalIdentifier { get; set; }

    // 業務邏輯方法

    /// <summary>
    /// 檢查成員資格是否有效
    /// </summary>
    public bool IsValidMembership()
    {
        return IsActive && 
               !IsDeleted &&
               (!ExpiresAt.HasValue || ExpiresAt.Value > DateTime.UtcNow);
    }

    /// <summary>
    /// 檢查成員資格是否過期
    /// </summary>
    public bool IsExpired()
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    }

    /// <summary>
    /// 延長成員資格
    /// </summary>
    public void ExtendMembership(DateTime newExpiryDate)
    {
        ExpiresAt = newExpiryDate;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 停用成員資格
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 啟用成員資格
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}