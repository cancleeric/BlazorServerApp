using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// Token 黑名單 Repository 介面
/// </summary>
public interface ITokenBlacklistRepository
{
    /// <summary>
    /// 將 Token 加入黑名單
    /// </summary>
    /// <param name="tokenId">Token ID (jti)</param>
    /// <param name="tokenHash">Token 雜湊值</param>
    /// <param name="expiresAt">Token 過期時間</param>
    /// <param name="clientId">客戶端 ID</param>
    /// <param name="subject">使用者 ID</param>
    /// <param name="reason">撤銷原因</param>
    Task AddToBlacklistAsync(string tokenId, string tokenHash, DateTime expiresAt, 
        string clientId, string? subject, string? reason);

    /// <summary>
    /// 檢查 Token 是否在黑名單中
    /// </summary>
    /// <param name="tokenId">Token ID (jti)</param>
    /// <returns>是否被撤銷</returns>
    Task<bool> IsTokenRevokedAsync(string tokenId);

    /// <summary>
    /// 檢查 Token Hash 是否在黑名單中 (用於快速查詢)
    /// </summary>
    /// <param name="tokenHash">Token 雜湊值</param>
    /// <returns>是否被撤銷</returns>
    Task<bool> IsTokenHashRevokedAsync(string tokenHash);

    /// <summary>
    /// 撤銷使用者的所有 Token
    /// </summary>
    /// <param name="subject">使用者 ID</param>
    /// <param name="reason">撤銷原因</param>
    Task RevokeAllUserTokensAsync(string subject, string reason);

    /// <summary>
    /// 撤銷客戶端的所有 Token
    /// </summary>
    /// <param name="clientId">客戶端 ID</param>
    /// <param name="reason">撤銷原因</param>
    Task RevokeAllClientTokensAsync(string clientId, string reason);

    /// <summary>
    /// 清理過期的黑名單條目
    /// </summary>
    Task CleanupExpiredAsync();

    /// <summary>
    /// 取得黑名單統計資訊
    /// </summary>
    Task<int> GetBlacklistCountAsync();
}