using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalIdentityServer.Data.Entities;

/// <summary>
/// 客戶端應用程式實體 - 遵循單一責任原則 (SRP)
/// 負責儲存 OAuth2/OIDC 客戶端應用程式設定
/// </summary>
[Table("Clients")]
public class ClientEntity
{
    [Key]
    [MaxLength(100)]
    public string ClientId { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string ClientName { get; set; } = default!;

    [Required]
    [MaxLength(500)]
    public string ClientSecret { get; set; } = default!;

    /// <summary>
    /// 允許的重導向 URI，以 JSON 字串儲存
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string RedirectUris { get; set; } = default!;

    /// <summary>
    /// 允許的權限範圍，以 JSON 字串儲存
    /// </summary>
    [MaxLength(1000)]
    public string AllowedScopes { get; set; } = "[]";

    /// <summary>
    /// 是否需要使用者同意
    /// </summary>
    public bool RequireConsent { get; set; } = true;

    /// <summary>
    /// 客戶端類型：confidential(機密) 或 public(公開)
    /// </summary>
    [MaxLength(20)]
    public string ClientType { get; set; } = "confidential";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // 導航屬性
    public virtual ICollection<AuthorizationCodeEntity> AuthorizationCodes { get; set; } = new List<AuthorizationCodeEntity>();
    public virtual ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = new List<RefreshTokenEntity>();
}
