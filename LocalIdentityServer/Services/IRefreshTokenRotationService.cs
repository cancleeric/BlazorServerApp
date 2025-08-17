using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Services;

/// <summary>
/// Refresh Token 輪替服務介面 - 遵循依賴反轉原則 (DIP)
/// 負責實作 Token 輪替、重用偵測與安全回應機制
/// </summary>
public interface IRefreshTokenRotationService
{
    /// <summary>
    /// 執行 Token 輪替 - 產生新 Token 並廢止舊 Token
    /// </summary>
    /// <param name="oldToken">舊的 Refresh Token</param>
    /// <param name="clientId">客戶端 ID</param>
    /// <param name="scopes">權限範圍</param>
    /// <returns>輪替結果</returns>
    Task<TokenRotationResult> RotateTokenAsync(string oldToken, string clientId, string[] scopes);

    /// <summary>
    /// 建立新的 Token 家族 (初次授權時使用)
    /// </summary>
    /// <param name="user">使用者</param>
    /// <param name="client">客戶端</param>
    /// <param name="scopes">權限範圍</param>
    /// <returns>新 Token 及家族 ID</returns>
    Task<TokenCreationResult> CreateTokenFamilyAsync(UserEntity user, ClientEntity client, string[] scopes);

    /// <summary>
    /// 檢查 Token 重用並觸發安全回應
    /// </summary>
    /// <param name="token">被檢查的 Token</param>
    /// <returns>安全檢查結果</returns>
    Task<SecurityCheckResult> CheckTokenReuseAsync(string token);

    /// <summary>
    /// 緊急撤銷 Token 家族 (安全事件時使用)
    /// </summary>
    /// <param name="tokenFamily">Token 家族 ID</param>
    /// <param name="reason">撤銷原因</param>
    Task EmergencyRevokeTokenFamilyAsync(string tokenFamily, string reason);

    /// <summary>
    /// 取得 Token 家族統計資訊
    /// </summary>
    /// <param name="tokenFamily">Token 家族 ID</param>
    /// <returns>家族統計</returns>
    Task<TokenFamilyStatistics> GetTokenFamilyStatisticsAsync(string tokenFamily);
}

/// <summary>
/// Token 輪替結果
/// </summary>
public class TokenRotationResult
{
    public bool Success { get; set; }
    public string? NewToken { get; set; }
    public string? ErrorMessage { get; set; }
    public SecurityCheckResult? SecurityCheck { get; set; }
}

/// <summary>
/// Token 建立結果
/// </summary>
public class TokenCreationResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? TokenFamily { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 安全檢查結果
/// </summary>
public class SecurityCheckResult
{
    public bool IsSecure { get; set; }
    public bool IsTokenReused { get; set; }
    public SecurityThreatLevel ThreatLevel { get; set; }
    public string? ThreatDescription { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// 安全威脅等級
/// </summary>
public enum SecurityThreatLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Token 家族統計資訊
/// </summary>
public class TokenFamilyStatistics
{
    public string TokenFamily { get; set; } = default!;
    public int TotalTokens { get; set; }
    public int ActiveTokens { get; set; }
    public int UsedTokens { get; set; }
    public int RevokedTokens { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public TimeSpan FamilyAge => DateTime.UtcNow - CreatedAt;
}