using System.ComponentModel.DataAnnotations;

namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// Token 黑名單實體
/// 用於快速檢查已撤銷或無效的 Token
/// </summary>
public class TokenBlacklist : TenantAwareEntity
{
    /// <summary>
    /// JWT ID (jti claim)
    /// </summary>
    [Required]
    [StringLength(64)]
    public string JwtId { get; set; } = string.Empty;

    /// <summary>
    /// Token 雜湊值 (用於快速比對)
    /// </summary>
    [Required]
    [StringLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// 使用者 ID
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Token 類型
    /// </summary>
    [Required]
    [StringLength(20)]
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    /// 加入黑名單的時間
    /// </summary>
    [Required]
    public DateTime BlacklistedAt { get; set; }

    /// <summary>
    /// Token 原本的過期時間
    /// </summary>
    [Required]
    public DateTime OriginalExpiresAt { get; set; }

    /// <summary>
    /// 加入黑名單的原因
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 加入黑名單的使用者 ID
    /// </summary>
    public Guid? BlacklistedByUserId { get; set; }

    /// <summary>
    /// 黑名單類型
    /// </summary>
    public BlacklistType Type { get; set; } = BlacklistType.Revoked;

    /// <summary>
    /// 是否為永久黑名單
    /// </summary>
    public bool IsPermanent { get; set; } = false;

    /// <summary>
    /// 黑名單到期時間 (null 表示永久)
    /// </summary>
    public DateTime? BlacklistExpiresAt { get; set; }

    /// <summary>
    /// 額外的 Metadata (JSON 格式)
    /// </summary>
    [StringLength(1000)]
    public string? Metadata { get; set; }

    // 導航屬性

    /// <summary>
    /// 關聯的使用者
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// 關聯的租戶
    /// </summary>
    public new Tenant? Tenant { get; set; }

    /// <summary>
    /// 加入黑名單的使用者
    /// </summary>
    public User? BlacklistedByUser { get; set; }

    // 業務邏輯方法

    /// <summary>
    /// 檢查黑名單項目是否仍然有效
    /// </summary>
    public bool IsActive()
    {
        if (IsPermanent || !BlacklistExpiresAt.HasValue)
            return true;

        return DateTime.UtcNow <= BlacklistExpiresAt.Value;
    }

    /// <summary>
    /// 檢查是否可以清理 (Token 已過期且黑名單也過期)
    /// </summary>
    public bool CanBeCleanedUp()
    {
        // Token 本身已過期
        var tokenExpired = DateTime.UtcNow > OriginalExpiresAt;
        
        // 黑名單不是永久的且已過期
        var blacklistExpired = !IsPermanent && 
                               BlacklistExpiresAt.HasValue && 
                               DateTime.UtcNow > BlacklistExpiresAt.Value;

        return tokenExpired && (blacklistExpired || !IsPermanent);
    }

    /// <summary>
    /// 延長黑名單時間
    /// </summary>
    public void ExtendBlacklist(TimeSpan extension, string reason)
    {
        if (IsPermanent)
            return;

        if (BlacklistExpiresAt.HasValue)
        {
            BlacklistExpiresAt = BlacklistExpiresAt.Value.Add(extension);
        }
        else
        {
            BlacklistExpiresAt = DateTime.UtcNow.Add(extension);
        }

        Reason = $"{Reason}; Extended: {reason}";
    }

    /// <summary>
    /// 設為永久黑名單
    /// </summary>
    public void MakePermanent(string reason)
    {
        IsPermanent = true;
        BlacklistExpiresAt = null;
        Type = BlacklistType.Permanent;
        Reason = $"{Reason}; Made permanent: {reason}";
    }

    /// <summary>
    /// 計算黑名單剩餘時間
    /// </summary>
    public TimeSpan? GetRemainingBlacklistTime()
    {
        if (IsPermanent || !BlacklistExpiresAt.HasValue)
            return null;

        var remaining = BlacklistExpiresAt.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// 檢查指定的 Token 雜湊是否符合
    /// </summary>
    public bool MatchesTokenHash(string tokenHash)
    {
        return string.Equals(TokenHash, tokenHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 檢查指定的 JWT ID 是否符合
    /// </summary>
    public bool MatchesJwtId(string jwtId)
    {
        return string.Equals(JwtId, jwtId, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 黑名單類型列舉
/// </summary>
public enum BlacklistType
{
    /// <summary>
    /// 已撤銷
    /// </summary>
    Revoked = 1,

    /// <summary>
    /// 可疑活動
    /// </summary>
    Suspicious = 2,

    /// <summary>
    /// 使用者帳號被鎖定
    /// </summary>
    UserLocked = 3,

    /// <summary>
    /// 租戶被停用
    /// </summary>
    TenantDisabled = 4,

    /// <summary>
    /// 安全策略違規
    /// </summary>
    SecurityViolation = 5,

    /// <summary>
    /// 永久禁用
    /// </summary>
    Permanent = 6,

    /// <summary>
    /// 管理員手動撤銷
    /// </summary>
    AdminRevoked = 7,

    /// <summary>
    /// Token 洩露
    /// </summary>
    Compromised = 8
}