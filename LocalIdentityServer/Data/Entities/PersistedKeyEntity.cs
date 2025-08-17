using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalIdentityServer.Data.Entities;

/// <summary>
/// 持久化金鑰實體 - 遵循單一責任原則 (SRP)
/// 負責儲存 JWT 簽章金鑰，支援金鑰輪替
/// </summary>
[Table("PersistedKeys")]
public class PersistedKeyEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 金鑰識別碼
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string KeyId { get; set; } = default!;

    /// <summary>
    /// 金鑰演算法 (如 RS256, ES256)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Algorithm { get; set; } = default!;

    /// <summary>
    /// 金鑰用途 (sig=簽章, enc=加密)
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Use { get; set; } = "sig";

    /// <summary>
    /// 金鑰類型 (RSA, EC)
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string KeyType { get; set; } = default!;

    /// <summary>
    /// 加密後的金鑰資料 (使用 Data Protection API 加密)
    /// </summary>
    [Required]
    public string EncryptedKeyData { get; set; } = default!;

    /// <summary>
    /// 公鑰資料 (用於 JWKS，明文存儲)
    /// </summary>
    public string? PublicKeyData { get; set; }

    /// <summary>
    /// 金鑰版本號 (用於版本管理)
    /// </summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最後修改時間
    /// </summary>
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 金鑰啟用時間
    /// </summary>
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 金鑰到期時間
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 是否為主要金鑰 (用於簽章)
    /// </summary>
    public bool IsPrimary { get; set; } = false;

    /// <summary>
    /// 是否已撤銷
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// 軟刪除標記
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// 軟刪除時間
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
