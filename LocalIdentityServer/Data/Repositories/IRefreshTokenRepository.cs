using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// 刷新權杖 Repository 介面 - 遵循介面隔離原則 (ISP)
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// 儲存刷新權杖
    /// </summary>
    Task<RefreshTokenEntity> StoreAsync(RefreshTokenEntity refreshToken);

    /// <summary>
    /// 建立新的刷新權杖 (別名方法，與 StoreAsync 相同)
    /// </summary>
    Task<RefreshTokenEntity> CreateAsync(RefreshTokenEntity refreshToken);

    /// <summary>
    /// 更新刷新權杖
    /// </summary>
    Task<RefreshTokenEntity> UpdateAsync(RefreshTokenEntity refreshToken);

    /// <summary>
    /// 根據權杖查詢
    /// </summary>
    Task<RefreshTokenEntity?> GetByTokenAsync(string token);

    /// <summary>
    /// 使用權杖 (標記為已使用，實現權杖輪替)
    /// </summary>
    Task<RefreshTokenEntity?> UseTokenAsync(string token, string? replacedByToken = null);

    /// <summary>
    /// 撤銷權杖
    /// </summary>
    Task RevokeTokenAsync(string token, string reason);

    /// <summary>
    /// 撤銷權杖鏈 (當發現重放攻擊時)
    /// </summary>
    Task RevokeTokenChainAsync(string token, string reason);

    /// <summary>
    /// 撤銷整個 Token 家族 (重用偵測時使用)
    /// </summary>
    Task RevokeTokenFamilyAsync(string tokenFamily, string reason);

    /// <summary>
    /// 根據 Token 家族查詢所有 Token
    /// </summary>
    Task<List<RefreshTokenEntity>> GetByTokenFamilyAsync(string tokenFamily);

    /// <summary>
    /// 檢查 Token 是否已被重用 (用於偵測可疑活動)
    /// </summary>
    Task<bool> IsTokenReusedAsync(string token);

    /// <summary>
    /// 清理過期的權杖
    /// </summary>
    Task CleanupExpiredAsync();

    /// <summary>
    /// 撤銷使用者的所有權杖
    /// </summary>
    Task RevokeByUserAsync(string userId, string reason);

    /// <summary>
    /// 撤銷客戶端的所有權杖
    /// </summary>
    Task RevokeByClientAsync(string clientId, string reason);

    /// <summary>
    /// 檢查權杖是否有效 (未過期、未撤銷、未使用)
    /// </summary>
    Task<bool> IsValidTokenAsync(string token);
}
