using EnterpriseIDS.Core.ValueObjects;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// 租戶上下文分散式快取介面
/// </summary>
public interface ITenantContextCache
{
    /// <summary>
    /// 設定租戶上下文快取
    /// </summary>
    /// <param name="key">快取鍵值</param>
    /// <param name="context">租戶上下文</param>
    /// <param name="expiration">過期時間，預設為 30 分鐘</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAsync(string key, TenantContext context, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶上下文快取
    /// </summary>
    /// <param name="key">快取鍵值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>租戶上下文，若不存在則返回 null</returns>
    Task<TenantContext?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除租戶上下文快取
    /// </summary>
    /// <param name="key">快取鍵值</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查快取鍵值是否存在
    /// </summary>
    /// <param name="key">快取鍵值</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 延長快取過期時間
    /// </summary>
    /// <param name="key">快取鍵值</param>
    /// <param name="expiration">新的過期時間</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RefreshAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得符合模式的快取鍵值列表
    /// </summary>
    /// <param name="pattern">搜尋模式</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IEnumerable<string>> GetKeysAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次設定快取
    /// </summary>
    /// <param name="items">快取項目字典</param>
    /// <param name="expiration">過期時間</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetManyAsync(Dictionary<string, TenantContext> items, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次取得快取
    /// </summary>
    /// <param name="keys">快取鍵值列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Dictionary<string, TenantContext?>> GetManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
}