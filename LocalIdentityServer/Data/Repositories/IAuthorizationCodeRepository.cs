using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// 授權碼 Repository 介面 - 遵循介面隔離原則 (ISP)
/// </summary>
public interface IAuthorizationCodeRepository
{
    /// <summary>
    /// 儲存授權碼
    /// </summary>
    Task<AuthorizationCodeEntity> StoreAsync(AuthorizationCodeEntity authCode);

    /// <summary>
    /// 取得並標記為已使用 (單次使用原則)
    /// </summary>
    Task<AuthorizationCodeEntity?> TakeAsync(string code);

    /// <summary>
    /// 根據授權碼查詢 (不標記為已使用)
    /// </summary>
    Task<AuthorizationCodeEntity?> GetByCodeAsync(string code);

    /// <summary>
    /// 清理過期的授權碼
    /// </summary>
    Task CleanupExpiredAsync();

    /// <summary>
    /// 撤銷使用者的所有授權碼
    /// </summary>
    Task RevokeByUserAsync(string userId);

    /// <summary>
    /// 撤銷客戶端的所有授權碼
    /// </summary>
    Task RevokeByClientAsync(string clientId);
}
