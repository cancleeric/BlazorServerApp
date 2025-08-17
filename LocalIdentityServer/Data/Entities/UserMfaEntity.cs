using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalIdentityServer.Data.Entities;

/// <summary>
/// 使用者多因子認證實體 - 遵循單一責任原則 (SRP)
/// 負責儲存使用者的 MFA 設定和密鑰資訊
/// </summary>
[Table("UserMfa")]
public class UserMfaEntity
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
    /// MFA 方法類型 (TOTP, SMS, Email)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Method { get; set; } = default!;

    /// <summary>
    /// 是否已啟用此 MFA 方法
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// 是否為主要 MFA 方法
    /// </summary>
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// 加密後的 MFA 密鑰 (TOTP Secret)
    /// </summary>
    [MaxLength(1000)]
    public string? EncryptedSecret { get; set; }

    /// <summary>
    /// 手機號碼 (SMS 驗證用)
    /// </summary>
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Email 地址 (Email 驗證用)
    /// </summary>
    [MaxLength(200)]
    public string? EmailAddress { get; set; }

    /// <summary>
    /// 設備名稱 (用於識別)
    /// </summary>
    [MaxLength(100)]
    public string? DeviceName { get; set; }

    /// <summary>
    /// 最後使用時間
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新時間
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 失敗嘗試次數 (用於防暴力破解)
    /// </summary>
    public int FailedAttempts { get; set; } = 0;

    /// <summary>
    /// 鎖定到期時間 (防暴力破解)
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// 額外設定 (JSON 格式)
    /// </summary>
    [MaxLength(1000)]
    public string? Settings { get; set; } = "{}";

    // 導航屬性
    public virtual UserEntity User { get; set; } = default!;
    public virtual ICollection<MfaAuditLogEntity> AuditLogs { get; set; } = new List<MfaAuditLogEntity>();
}