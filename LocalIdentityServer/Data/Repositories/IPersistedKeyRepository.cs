using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// 持久化金鑰 Repository 介面 - 遵循介面隔離原則 (ISP)
/// </summary>
public interface IPersistedKeyRepository
{
    /// <summary>
    /// 根據金鑰 ID 查詢
    /// </summary>
    Task<PersistedKeyEntity?> GetByKeyIdAsync(string keyId);

    /// <summary>
    /// 取得主要簽章金鑰
    /// </summary>
    Task<PersistedKeyEntity?> GetPrimarySigningKeyAsync();

    /// <summary>
    /// 取得所有有效的簽章金鑰 (用於 JWKS)
    /// </summary>
    Task<IEnumerable<PersistedKeyEntity>> GetValidSigningKeysAsync();

    /// <summary>
    /// 儲存新金鑰
    /// </summary>
    Task<PersistedKeyEntity> StoreAsync(PersistedKeyEntity key);

    /// <summary>
    /// 設定主要金鑰 (同時取消其他金鑰的主要狀態)
    /// </summary>
    Task SetPrimaryKeyAsync(string keyId);

    /// <summary>
    /// 撤銷金鑰
    /// </summary>
    Task RevokeKeyAsync(string keyId);

    /// <summary>
    /// 清理過期的金鑰
    /// </summary>
    Task CleanupExpiredKeysAsync();

    /// <summary>
    /// 檢查是否需要金鑰輪替
    /// </summary>
    Task<bool> ShouldRotateKeyAsync();
}
