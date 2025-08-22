using System.ComponentModel.DataAnnotations;

namespace LocalIdentityServer.Data.Entities;

/// <summary>
/// Token 黑名單實體 - 用於存儲已撤銷的 Access Token
/// </summary>
public class TokenBlacklistEntity
{
    /// <summary>
    /// 主鍵
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// JWT Token ID (jti claim)
    /// </summary>
    [Required]
    [StringLength(64)]
    public string TokenId { get; set; } = string.Empty;

    /// <summary>
    /// Token 雜湊值 (用於快速查詢)
    /// </summary>
    [Required]
    [StringLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Token 過期時間
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 撤銷時間
    /// </summary>
    public DateTime RevokedAt { get; set; }

    /// <summary>
    /// 撤銷原因
    /// </summary>
    [StringLength(500)]
    public string? RevocationReason { get; set; }

    /// <summary>
    /// 客戶端 ID
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 使用者 ID (如果適用)
    /// </summary>
    [StringLength(100)]
    public string? Subject { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}