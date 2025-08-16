using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalIdentityServer.Data.Entities;

/// <summary>
/// 刷新權杖實體 - 遵循單一責任原則 (SRP)
/// 負責儲存刷新權杖資訊，支援權杖輪替防重放攻擊
/// </summary>
[Table("RefreshTokens")]
public class RefreshTokenEntity
{
    [Key]
    [MaxLength(200)]
    public string Token { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string ClientId { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string Subject { get; set; } = default!;

    [MaxLength(100)]
    public string Username { get; set; } = default!;

    [MaxLength(200)]
    public string Email { get; set; } = default!;

    [MaxLength(500)]
    public string Scope { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 是否已被使用 - 支援權杖輪替
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// 被取代的父權杖 - 實現權杖輪替鏈
    /// </summary>
    [MaxLength(200)]
    public string? ReplacedByToken { get; set; }

    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// 是否已撤銷
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// 撤銷原因
    /// </summary>
    [MaxLength(500)]
    public string? RevokeReason { get; set; }

    // 外鍵關係
    [ForeignKey(nameof(ClientId))]
    public virtual ClientEntity Client { get; set; } = default!;

    [ForeignKey(nameof(Subject))]
    public virtual UserEntity User { get; set; } = default!;
}
