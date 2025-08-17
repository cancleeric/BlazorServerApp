using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// JWT Token Repository 介面
/// </summary>
public interface IJwtTokenRepository : IRepository<JwtToken>
{
    /// <summary>
    /// 根據 JWT ID 取得 Token
    /// </summary>
    /// <param name="jwtId">JWT ID</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>Token 實體</returns>
    Task<JwtToken?> GetByJwtIdAsync(string jwtId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據 Token 值雜湊取得 Token
    /// </summary>
    /// <param name="tokenHash">Token 雜湊值</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>Token 實體</returns>
    Task<JwtToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者的 Token 清單
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="tokenType">Token 類型篩選</param>
    /// <param name="status">狀態篩選</param>
    /// <param name="includeExpired">是否包含已過期的</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>Token 清單</returns>
    Task<IEnumerable<JwtToken>> GetUserTokensAsync(
        Guid userId,
        string? tokenType = null,
        TokenStatus? status = null,
        bool includeExpired = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的 Token 清單
    /// </summary>
    /// <param name="tenantId">租戶 ID</param>
    /// <param name="tokenType">Token 類型篩選</param>
    /// <param name="status">狀態篩選</param>
    /// <param name="includeExpired">是否包含已過期的</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>Token 清單</returns>
    Task<IEnumerable<JwtToken>> GetTenantTokensAsync(
        Guid tenantId,
        string? tokenType = null,
        TokenStatus? status = null,
        bool includeExpired = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得即將過期的 Token
    /// </summary>
    /// <param name="withinMinutes">多少分鐘內過期</param>
    /// <param name="tokenType">Token 類型篩選</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>即將過期的 Token 清單</returns>
    Task<IEnumerable<JwtToken>> GetExpiringTokensAsync(
        int withinMinutes = 30,
        string? tokenType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得已過期的 Token
    /// </summary>
    /// <param name="olderThanDays">多少天前過期的</param>
    /// <param name="tokenType">Token 類型篩選</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>已過期的 Token 清單</returns>
    Task<IEnumerable<JwtToken>> GetExpiredTokensAsync(
        int olderThanDays = 1,
        string? tokenType = null,
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
    /// 撤銷租戶的所有 Token
    /// </summary>
    /// <param name="tenantId">租戶 ID</param>
    /// <param name="reason">撤銷原因</param>
    /// <param name="revokedByUserId">撤銷者使用者 ID</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>撤銷的 Token 數量</returns>
    Task<int> RevokeTenantTokensAsync(
        Guid tenantId,
        string reason,
        Guid? revokedByUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次刪除過期的 Token
    /// </summary>
    /// <param name="olderThanDays">多少天前過期的</param>
    /// <param name="batchSize">批次大小</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>刪除的 Token 數量</returns>
    Task<int> DeleteExpiredTokensAsync(
        int olderThanDays = 30,
        int batchSize = 1000,
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

    /// <summary>
    /// 檢查使用者是否有有效的 Token
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="tokenType">Token 類型</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>是否有有效 Token</returns>
    Task<bool> HasValidTokenAsync(
        Guid userId,
        string? tokenType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者的活躍會話數量
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>活躍會話數量</returns>
    Task<int> GetActiveSessionCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得關聯的 Token (透過 Refresh Token 關聯)
    /// </summary>
    /// <param name="refreshTokenId">Refresh Token ID</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>關聯的 Access Token 清單</returns>
    Task<IEnumerable<JwtToken>> GetAssociatedTokensAsync(
        Guid refreshTokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新 Token 使用資訊
    /// </summary>
    /// <param name="jwtId">JWT ID</param>
    /// <param name="ipAddress">IP 地址</param>
    /// <param name="userAgent">User Agent</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>更新結果</returns>
    Task<bool> UpdateTokenUsageAsync(
        string jwtId,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Token 黑名單 Repository 介面
/// </summary>
public interface ITokenBlacklistRepository : IRepository<TokenBlacklist>
{
    /// <summary>
    /// 根據 JWT ID 檢查是否在黑名單
    /// </summary>
    /// <param name="jwtId">JWT ID</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>是否在黑名單</returns>
    Task<bool> IsBlacklistedAsync(string jwtId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據 Token 雜湊檢查是否在黑名單
    /// </summary>
    /// <param name="tokenHash">Token 雜湊</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>是否在黑名單</returns>
    Task<bool> IsBlacklistedByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得黑名單項目
    /// </summary>
    /// <param name="jwtId">JWT ID</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>黑名單項目</returns>
    Task<TokenBlacklist?> GetBlacklistItemAsync(string jwtId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者的黑名單項目
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="includeExpired">是否包含已過期的</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>黑名單項目清單</returns>
    Task<IEnumerable<TokenBlacklist>> GetUserBlacklistAsync(
        Guid userId,
        bool includeExpired = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得可清理的黑名單項目
    /// </summary>
    /// <param name="olderThanDays">多少天前的項目</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>可清理的項目清單</returns>
    Task<IEnumerable<TokenBlacklist>> GetCleanupCandidatesAsync(
        int olderThanDays = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次刪除過期的黑名單項目
    /// </summary>
    /// <param name="olderThanDays">多少天前過期的</param>
    /// <param name="batchSize">批次大小</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>刪除的項目數量</returns>
    Task<int> DeleteExpiredBlacklistAsync(
        int olderThanDays = 30,
        int batchSize = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 將使用者的所有 Token 加入黑名單
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="reason">加入原因</param>
    /// <param name="blacklistType">黑名單類型</param>
    /// <param name="blacklistedByUserId">操作者使用者 ID</param>
    /// <param name="isPermanent">是否永久</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>加入黑名單的項目數量</returns>
    Task<int> BlacklistUserTokensAsync(
        Guid userId,
        string reason,
        BlacklistType blacklistType = BlacklistType.UserLocked,
        Guid? blacklistedByUserId = null,
        bool isPermanent = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得黑名單統計資訊
    /// </summary>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>統計資訊</returns>
    Task<BlacklistStatistics> GetBlacklistStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 黑名單統計資訊
/// </summary>
public class BlacklistStatistics
{
    /// <summary>
    /// 總黑名單項目數量
    /// </summary>
    public int TotalBlacklistItems { get; set; }

    /// <summary>
    /// 有效黑名單項目數量
    /// </summary>
    public int ActiveBlacklistItems { get; set; }

    /// <summary>
    /// 已過期黑名單項目數量
    /// </summary>
    public int ExpiredBlacklistItems { get; set; }

    /// <summary>
    /// 永久黑名單項目數量
    /// </summary>
    public int PermanentBlacklistItems { get; set; }

    /// <summary>
    /// 各類型黑名單項目數量
    /// </summary>
    public Dictionary<BlacklistType, int> CountByType { get; set; } = new();

    /// <summary>
    /// 最新黑名單項目時間
    /// </summary>
    public DateTime? LatestBlacklistAt { get; set; }

    /// <summary>
    /// 統計時間
    /// </summary>
    public DateTime StatisticsGeneratedAt { get; set; } = DateTime.UtcNow;
}