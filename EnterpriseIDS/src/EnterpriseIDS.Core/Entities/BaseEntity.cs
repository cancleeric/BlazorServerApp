using System.ComponentModel.DataAnnotations;

namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 基礎實體類別，包含通用欄位
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// 實體唯一識別碼
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 建立者 ID
    /// </summary>
    public string? CreatedById { get; set; }
    
    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// 最後更新者 ID
    /// </summary>
    public string? UpdatedById { get; set; }
    
    /// <summary>
    /// 版本號（用於樂觀鎖定）
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// 支援軟刪除的基礎實體
/// </summary>
public abstract class BaseEntityWithSoftDelete : BaseEntity
{
    /// <summary>
    /// 是否已刪除
    /// </summary>
    public bool IsDeleted { get; set; } = false;
    
    /// <summary>
    /// 刪除時間
    /// </summary>
    public DateTime? DeletedAt { get; set; }
    
    /// <summary>
    /// 刪除者 ID
    /// </summary>
    public string? DeletedById { get; set; }
}

/// <summary>
/// 支援多租戶的基礎實體
/// </summary>
public abstract class TenantAwareEntity : BaseEntityWithSoftDelete
{
    /// <summary>
    /// 租戶 ID
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// 租戶導航屬性
    /// </summary>
    public virtual Tenant? Tenant { get; set; }
}