using EnterpriseIDS.Core.Entities;
using System.Security.Claims;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// JWT Token 服務介面
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// 生成 Access Token
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="tenantId">租戶 ID</param>
    /// <param name="claims">額外的 Claims</param>
    /// <param name="scopes">授權範圍</param>
    /// <param name="clientId">客戶端 ID</param>
    /// <param name="audience">接收者</param>
    /// <param name="expirationMinutes">過期時間(分鐘)，null 使用預設值</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>Access Token 資訊</returns>
    Task<TokenResult> GenerateAccessTokenAsync(
        Guid userId,
        Guid tenantId,
        IEnumerable<Claim>? claims = null,
        IEnumerable<string>? scopes = null,
        string? clientId = null,
        string? audience = null,
        int? expirationMinutes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成 Refresh Token
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="tenantId">租戶 ID</param>
    /// <param name="clientId">客戶端 ID</param>
    /// <param name="scopes">授權範圍</param>
    /// <param name="expirationDays">過期時間(天)，null 使用預設值</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>Refresh Token 資訊</returns>
    Task<TokenResult> GenerateRefreshTokenAsync(
        Guid userId,
        Guid tenantId,
        string? clientId = null,
        IEnumerable<string>? scopes = null,
        int? expirationDays = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成 ID Token
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="tenantId">租戶 ID</param>
    /// <param name="audience">接收者</param>
    /// <param name="nonce">隨機數</param>
    /// <param name="authTime">認證時間</param>
    /// <param name="additionalClaims">額外的 Claims</param>
    /// <param name="expirationMinutes">過期時間(分鐘)，null 使用預設值</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>ID Token 資訊</returns>
    Task<TokenResult> GenerateIdTokenAsync(
        Guid userId,
        Guid tenantId,
        string audience,
        string? nonce = null,
        DateTime? authTime = null,
        IEnumerable<Claim>? additionalClaims = null,
        int? expirationMinutes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證 JWT Token
    /// </summary>
    /// <param name="token">Token 值</param>
    /// <param name="validateLifetime">是否驗證生命週期</param>
    /// <param name="validateAudience">是否驗證接收者</param>
    /// <param name="validateIssuer">是否驗證發行者</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>驗證結果</returns>
    Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        bool validateLifetime = true,
        bool validateAudience = true,
        bool validateIssuer = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 刷新 Access Token
    /// </summary>
    /// <param name="refreshToken">Refresh Token</param>
    /// <param name="newScopes">新的授權範圍 (null 保持原有)</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>新的 Token 對</returns>
    Task<RefreshTokenResult> RefreshAccessTokenAsync(
        string refreshToken,
        IEnumerable<string>? newScopes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤銷 Token
    /// </summary>
    /// <param name="token">要撤銷的 Token</param>
    /// <param name="reason">撤銷原因</param>
    /// <param name="revokedByUserId">撤銷者使用者 ID</param>
    /// <param name="revokeAssociatedTokens">是否同時撤銷關聯的 Token</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>撤銷結果</returns>
    Task<bool> RevokeTokenAsync(
        string token,
        string reason,
        Guid? revokedByUserId = null,
        bool revokeAssociatedTokens = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤銷使用者的所有 Token
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="reason">撤銷原因</param>
    /// <param name="revokedByUserId">撤銷者使用者 ID</param>
    /// <param name="excludeTokenIds">排除的 Token ID</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>撤銷的 Token 數量</returns>
    Task<int> RevokeUserTokensAsync(
        Guid userId,
        string reason,
        Guid? revokedByUserId = null,
        IEnumerable<Guid>? excludeTokenIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查 Token 是否在黑名單中
    /// </summary>
    /// <param name="jwtId">JWT ID</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>是否在黑名單中</returns>
    Task<bool> IsTokenBlacklistedAsync(
        string jwtId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 將 Token 加入黑名單
    /// </summary>
    /// <param name="token">Token 資訊</param>
    /// <param name="reason">加入原因</param>
    /// <param name="blacklistType">黑名單類型</param>
    /// <param name="blacklistedByUserId">操作者使用者 ID</param>
    /// <param name="isPermanent">是否永久</param>
    /// <param name="blacklistDuration">黑名單持續時間</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>操作結果</returns>
    Task<bool> BlacklistTokenAsync(
        JwtToken token,
        string reason,
        BlacklistType blacklistType = BlacklistType.Revoked,
        Guid? blacklistedByUserId = null,
        bool isPermanent = false,
        TimeSpan? blacklistDuration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者的 Token 清單
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="tokenType">Token 類型 (null 為全部)</param>
    /// <param name="includeExpired">是否包含已過期的</param>
    /// <param name="includeRevoked">是否包含已撤銷的</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>Token 清單</returns>
    Task<IEnumerable<JwtToken>> GetUserTokensAsync(
        Guid userId,
        string? tokenType = null,
        bool includeExpired = false,
        bool includeRevoked = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理過期的 Token 和黑名單項目
    /// </summary>
    /// <param name="olderThanDays">清理多少天前的項目</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>清理的項目數量</returns>
    Task<CleanupResult> CleanupExpiredTokensAsync(
        int olderThanDays = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得 Token 統計資訊
    /// </summary>
    /// <param name="userId">使用者 ID (null 為全部)</param>
    /// <param name="tenantId">租戶 ID (null 為全部)</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>統計資訊</returns>
    Task<TokenStatistics> GetTokenStatisticsAsync(
        Guid? userId = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Token 結果
/// </summary>
public class TokenResult
{
    /// <summary>
    /// Token 值
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// JWT ID
    /// </summary>
    public string JwtId { get; set; } = string.Empty;

    /// <summary>
    /// Token 類型
    /// </summary>
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    /// 過期時間
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 發行時間
    /// </summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// 有效期間 (秒)
    /// </summary>
    public int ExpiresIn => (int)(ExpiresAt - DateTime.UtcNow).TotalSeconds;

    /// <summary>
    /// 授權範圍
    /// </summary>
    public IEnumerable<string> Scopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Token 實體 ID
    /// </summary>
    public Guid TokenId { get; set; }
}

/// <summary>
/// Token 驗證結果
/// </summary>
public class TokenValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 驗證失敗原因
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 錯誤代碼
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 解析出的 Claims
    /// </summary>
    public IEnumerable<Claim> Claims { get; set; } = Array.Empty<Claim>();

    /// <summary>
    /// JWT ID
    /// </summary>
    public string? JwtId { get; set; }

    /// <summary>
    /// 使用者 ID
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 租戶 ID
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Token 類型
    /// </summary>
    public string? TokenType { get; set; }

    /// <summary>
    /// 過期時間
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 是否即將過期
    /// </summary>
    public bool IsNearExpiry { get; set; }
}

/// <summary>
/// 刷新 Token 結果
/// </summary>
public class RefreshTokenResult
{
    /// <summary>
    /// 新的 Access Token
    /// </summary>
    public TokenResult AccessToken { get; set; } = new();

    /// <summary>
    /// 新的 Refresh Token (如果輪替)
    /// </summary>
    public TokenResult? RefreshToken { get; set; }

    /// <summary>
    /// 是否進行了 Refresh Token 輪替
    /// </summary>
    public bool RefreshTokenRotated { get; set; }
}

/// <summary>
/// 清理結果
/// </summary>
public class CleanupResult
{
    /// <summary>
    /// 清理的 Token 數量
    /// </summary>
    public int TokensCleanedUp { get; set; }

    /// <summary>
    /// 清理的黑名單項目數量
    /// </summary>
    public int BlacklistItemsCleanedUp { get; set; }

    /// <summary>
    /// 清理的總項目數量
    /// </summary>
    public int TotalItemsCleanedUp => TokensCleanedUp + BlacklistItemsCleanedUp;

    /// <summary>
    /// 清理開始時間
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 清理完成時間
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// 清理花費時間
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;
}

/// <summary>
/// Token 統計資訊
/// </summary>
public class TokenStatistics
{
    /// <summary>
    /// 總 Token 數量
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// 有效 Token 數量
    /// </summary>
    public int ActiveTokens { get; set; }

    /// <summary>
    /// 已過期 Token 數量
    /// </summary>
    public int ExpiredTokens { get; set; }

    /// <summary>
    /// 已撤銷 Token 數量
    /// </summary>
    public int RevokedTokens { get; set; }

    /// <summary>
    /// Access Token 數量
    /// </summary>
    public int AccessTokens { get; set; }

    /// <summary>
    /// Refresh Token 數量
    /// </summary>
    public int RefreshTokens { get; set; }

    /// <summary>
    /// ID Token 數量
    /// </summary>
    public int IdTokens { get; set; }

    /// <summary>
    /// 黑名單項目數量
    /// </summary>
    public int BlacklistItems { get; set; }

    /// <summary>
    /// 最新 Token 發行時間
    /// </summary>
    public DateTime? LatestTokenIssuedAt { get; set; }

    /// <summary>
    /// 統計時間
    /// </summary>
    public DateTime StatisticsGeneratedAt { get; set; } = DateTime.UtcNow;
}