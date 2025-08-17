using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalIdentityServer.Data.Entities;

/// <summary>
/// MFA 備用碼實體 - 遵循單一責任原則 (SRP)
/// 負責儲存使用者的 MFA 備用恢復碼
/// </summary>
[Table("MfaBackupCodes")]
public class MfaBackupCodeEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 關聯的使用者ID
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// 備用碼 (8位數字，經過雜湊處理)
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string CodeHash { get; set; } = default!;

    /// <summary>
    /// 是否已使用
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// 使用時間
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// 使用時的 IP 地址
    /// </summary>
    [MaxLength(45)]
    public string? UsedFromIp { get; set; }

    /// <summary>
    /// 使用時的 User Agent
    /// </summary>
    [MaxLength(500)]
    public string? UsedFromUserAgent { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 到期時間 (建議90天)
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(90);

    /// <summary>
    /// 備用碼批次ID (同一次生成的備用碼具有相同批次ID)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string BatchId { get; set; } = default!;

    // 導航屬性
    public virtual UserEntity User { get; set; } = default!;
}