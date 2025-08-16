using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalIdentityServer.Data.Entities;

/// <summary>
/// 授權碼實體 - 遵循單一責任原則 (SRP)
/// 負責儲存 OAuth2 授權碼相關資訊，確保單次使用
/// </summary>
[Table("AuthorizationCodes")]
public class AuthorizationCodeEntity
{
    [Key]
    [MaxLength(200)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string ClientId { get; set; } = default!;

    [Required]
    [MaxLength(500)]
    public string RedirectUri { get; set; } = default!;

    [MaxLength(500)]
    public string Scope { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Subject { get; set; } = default!;

    [MaxLength(100)]
    public string Username { get; set; } = default!;

    [MaxLength(200)]
    public string Email { get; set; } = default!;

    /// <summary>
    /// PKCE Code Challenge
    /// </summary>
    [MaxLength(200)]
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// PKCE Code Challenge Method (plain 或 S256)
    /// </summary>
    [MaxLength(10)]
    public string? CodeChallengeMethod { get; set; }

    /// <summary>
    /// OIDC Nonce 參數
    /// </summary>
    [MaxLength(100)]
    public string? Nonce { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 是否已被使用 - 確保單次使用原則
    /// </summary>
    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    // 外鍵關係
    [ForeignKey(nameof(ClientId))]
    public virtual ClientEntity Client { get; set; } = default!;

    [ForeignKey(nameof(Subject))]
    public virtual UserEntity User { get; set; } = default!;
}
