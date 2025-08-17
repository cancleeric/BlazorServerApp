using System.ComponentModel.DataAnnotations;

namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// JWT Token 實體
/// </summary>
public class JwtToken : TenantAwareEntity
{
    /// <summary>
    /// JWT ID (jti claim)
    /// </summary>
    [Required]
    [StringLength(64)]
    public string JwtId { get; set; } = string.Empty;

    /// <summary>
    /// 使用者 ID
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Token 類型 (access_token, refresh_token, id_token)
    /// </summary>
    [Required]
    [StringLength(20)]
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    /// Token 值 (已加密或雜湊)
    /// </summary>
    [Required]
    [StringLength(4000)]
    public string TokenValue { get; set; } = string.Empty;

    /// <summary>
    /// Token 發行時間
    /// </summary>
    [Required]
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// Token 過期時間
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 發行者 (iss claim)
    /// </summary>
    [StringLength(200)]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// 接收者 (aud claim)
    /// </summary>
    [StringLength(500)]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// 主體 (sub claim)
    /// </summary>
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 客戶端 ID
    /// </summary>
    [StringLength(100)]
    public string? ClientId { get; set; }

    /// <summary>
    /// 授權範圍 (space-separated)
    /// </summary>
    [StringLength(1000)]
    public string? Scopes { get; set; }

    /// <summary>
    /// Token 狀態
    /// </summary>
    public TokenStatus Status { get; set; } = TokenStatus.Active;

    /// <summary>
    /// 撤銷時間
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// 撤銷原因
    /// </summary>
    [StringLength(500)]
    public string? RevokedReason { get; set; }

    /// <summary>
    /// 撤銷者使用者 ID
    /// </summary>
    public Guid? RevokedByUserId { get; set; }

    /// <summary>
    /// 最後使用時間
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// 使用次數
    /// </summary>
    public int UseCount { get; set; } = 0;

    /// <summary>
    /// 來源 IP 地址
    /// </summary>
    [StringLength(45)]
    public string? SourceIpAddress { get; set; }

    /// <summary>
    /// User Agent
    /// </summary>
    [StringLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// 關聯的 Refresh Token ID (用於 Access Token)
    /// </summary>
    public Guid? RefreshTokenId { get; set; }

    /// <summary>
    /// 父級 Token ID (用於 Token 鏈追蹤)
    /// </summary>
    public Guid? ParentTokenId { get; set; }

    /// <summary>
    /// 額外的 Metadata (JSON 格式)
    /// </summary>
    [StringLength(2000)]
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
    /// 關聯的 Refresh Token
    /// </summary>
    public JwtToken? RefreshToken { get; set; }

    /// <summary>
    /// 父級 Token
    /// </summary>
    public JwtToken? ParentToken { get; set; }

    /// <summary>
    /// 子 Token 集合
    /// </summary>
    public ICollection<JwtToken> ChildTokens { get; set; } = new List<JwtToken>();

    /// <summary>
    /// 撤銷者使用者
    /// </summary>
    public User? RevokedByUser { get; set; }

    // 業務邏輯方法

    /// <summary>
    /// 檢查 Token 是否有效
    /// </summary>
    public bool IsValid()
    {
        return Status == TokenStatus.Active && 
               !IsExpired() && 
               !IsDeleted;
    }

    /// <summary>
    /// 檢查 Token 是否過期
    /// </summary>
    public bool IsExpired()
    {
        return DateTime.UtcNow > ExpiresAt;
    }

    /// <summary>
    /// 檢查 Token 是否已撤銷
    /// </summary>
    public bool IsRevoked()
    {
        return Status == TokenStatus.Revoked || RevokedAt.HasValue;
    }

    /// <summary>
    /// 撤銷 Token
    /// </summary>
    public void Revoke(string reason, Guid? revokedByUserId = null)
    {
        Status = TokenStatus.Revoked;
        RevokedAt = DateTime.UtcNow;
        RevokedReason = reason;
        RevokedByUserId = revokedByUserId;
    }

    /// <summary>
    /// 標記為已使用
    /// </summary>
    public void MarkAsUsed(string? ipAddress = null, string? userAgent = null)
    {
        LastUsedAt = DateTime.UtcNow;
        UseCount++;
        
        if (!string.IsNullOrEmpty(ipAddress))
        {
            SourceIpAddress = ipAddress;
        }
        
        if (!string.IsNullOrEmpty(userAgent))
        {
            UserAgent = userAgent;
        }
    }

    /// <summary>
    /// 檢查是否為 Access Token
    /// </summary>
    public bool IsAccessToken()
    {
        return TokenType.Equals("access_token", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 檢查是否為 Refresh Token
    /// </summary>
    public bool IsRefreshToken()
    {
        return TokenType.Equals("refresh_token", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 檢查是否為 ID Token
    /// </summary>
    public bool IsIdToken()
    {
        return TokenType.Equals("id_token", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 取得 Token 剩餘有效時間
    /// </summary>
    public TimeSpan GetRemainingLifetime()
    {
        var remaining = ExpiresAt - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// 檢查 Token 是否即將過期 (預設 5 分鐘內)
    /// </summary>
    public bool IsNearExpiry(TimeSpan? threshold = null)
    {
        var thresholdTime = threshold ?? TimeSpan.FromMinutes(5);
        return GetRemainingLifetime() <= thresholdTime;
    }

    /// <summary>
    /// 解析 Scopes 為陣列
    /// </summary>
    public string[] GetScopesArray()
    {
        if (string.IsNullOrEmpty(Scopes))
            return Array.Empty<string>();

        return Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// 設定 Scopes
    /// </summary>
    public void SetScopes(IEnumerable<string> scopes)
    {
        Scopes = string.Join(" ", scopes ?? Array.Empty<string>());
    }

    /// <summary>
    /// 檢查是否包含特定 Scope
    /// </summary>
    public bool HasScope(string scope)
    {
        if (string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(Scopes))
            return false;

        return GetScopesArray().Contains(scope, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Token 狀態列舉
/// </summary>
public enum TokenStatus
{
    /// <summary>
    /// 有效狀態
    /// </summary>
    Active = 1,

    /// <summary>
    /// 已撤銷
    /// </summary>
    Revoked = 2,

    /// <summary>
    /// 已過期
    /// </summary>
    Expired = 3,

    /// <summary>
    /// 已使用 (一次性 Token)
    /// </summary>
    Used = 4,

    /// <summary>
    /// 暫停使用
    /// </summary>
    Suspended = 5
}