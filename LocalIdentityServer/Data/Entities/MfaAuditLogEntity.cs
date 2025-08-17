using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalIdentityServer.Data.Entities;

/// <summary>
/// MFA 審計日誌實體 - 遵循單一責任原則 (SRP)
/// 負責記錄所有 MFA 相關的安全事件
/// </summary>
[Table("MfaAuditLogs")]
public class MfaAuditLogEntity
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
    /// 關聯的 MFA 方法ID (可選)
    /// </summary>
    [MaxLength(50)]
    public string? MfaMethodId { get; set; }

    /// <summary>
    /// 事件類型 (Setup, Verify, Disable, BackupCodeUsed, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = default!;

    /// <summary>
    /// MFA 方法類型 (TOTP, SMS, Email, BackupCode)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Method { get; set; } = default!;

    /// <summary>
    /// 事件結果 (Success, Failed, Blocked)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Result { get; set; } = default!;

    /// <summary>
    /// 失敗原因 (InvalidCode, Expired, TooManyAttempts, etc.)
    /// </summary>
    [MaxLength(100)]
    public string? FailureReason { get; set; }

    /// <summary>
    /// 事件描述
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 客戶端 IP 地址
    /// </summary>
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User Agent
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// 客戶端ID (OAuth2)
    /// </summary>
    [MaxLength(100)]
    public string? ClientId { get; set; }

    /// <summary>
    /// 會話ID
    /// </summary>
    [MaxLength(100)]
    public string? SessionId { get; set; }

    /// <summary>
    /// 風險評分 (0-100)
    /// </summary>
    public int RiskScore { get; set; } = 0;

    /// <summary>
    /// 地理位置資訊 (JSON 格式)
    /// </summary>
    [MaxLength(500)]
    public string? GeoLocation { get; set; }

    /// <summary>
    /// 設備指紋
    /// </summary>
    [MaxLength(500)]
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    /// 事件發生時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 額外資料 (JSON 格式)
    /// </summary>
    [MaxLength(2000)]
    public string? AdditionalData { get; set; }

    // 導航屬性
    public virtual UserEntity User { get; set; } = default!;
    public virtual UserMfaEntity? MfaMethod { get; set; }
}