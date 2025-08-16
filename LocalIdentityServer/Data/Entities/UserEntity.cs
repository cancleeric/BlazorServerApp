using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LocalIdentityServer.Data.Entities;

/// <summary>
/// 使用者實體 - 遵循單一責任原則 (SRP)
/// 負責儲存使用者基本資訊和認證資料
/// </summary>
[Table("Users")]
public class UserEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string UserName { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = default!;

    /// <summary>
    /// 雜湊後的密碼 - 不再儲存明文密碼
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = default!;

    /// <summary>
    /// 使用者角色，以 JSON 字串儲存
    /// </summary>
    [MaxLength(1000)]
    public string Roles { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // 導航屬性
    public virtual ICollection<AuthorizationCodeEntity> AuthorizationCodes { get; set; } = new List<AuthorizationCodeEntity>();
    public virtual ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = new List<RefreshTokenEntity>();
}
