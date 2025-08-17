namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// 分散式會話管理服務介面
/// </summary>
public interface IDistributedSessionService
{
    /// <summary>
    /// 建立會話
    /// </summary>
    /// <param name="sessionId">會話 ID</param>
    /// <param name="userId">使用者 ID</param>
    /// <param name="tenantId">租戶 ID</param>
    /// <param name="metadata">會話元資料</param>
    /// <param name="expiration">會話過期時間，預設為 8 小時</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CreateSessionAsync(string sessionId, Guid userId, Guid tenantId, 
        Dictionary<string, object>? metadata = null, TimeSpan? expiration = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得會話資訊
    /// </summary>
    /// <param name="sessionId">會話 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>會話資訊，若不存在則返回 null</returns>
    Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新會話最後活動時間
    /// </summary>
    /// <param name="sessionId">會話 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RefreshSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定會話資料
    /// </summary>
    /// <param name="sessionId">會話 ID</param>
    /// <param name="key">資料鍵值</param>
    /// <param name="value">資料值</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetSessionDataAsync(string sessionId, string key, object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得會話資料
    /// </summary>
    /// <param name="sessionId">會話 ID</param>
    /// <param name="key">資料鍵值</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T?> GetSessionDataAsync<T>(string sessionId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除會話資料
    /// </summary>
    /// <param name="sessionId">會話 ID</param>
    /// <param name="key">資料鍵值</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveSessionDataAsync(string sessionId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 銷毀會話
    /// </summary>
    /// <param name="sessionId">會話 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DestroySessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者的所有會話
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IEnumerable<SessionInfo>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 銷毀使用者的所有會話
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="excludeSessionId">排除的會話 ID（當前會話）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DestroyUserSessionsAsync(Guid userId, string? excludeSessionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理過期會話
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得活躍會話統計
    /// </summary>
    /// <param name="tenantId">租戶 ID（可選）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<SessionStats> GetSessionStatsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// 會話資訊
/// </summary>
public class SessionInfo
{
    /// <summary>
    /// 會話 ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 使用者 ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 租戶 ID
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最後活動時間
    /// </summary>
    public DateTime LastActivityAt { get; set; }

    /// <summary>
    /// 過期時間
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 會話元資料
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 是否已過期
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// 是否為活躍會話（最後 30 分鐘內有活動）
    /// </summary>
    public bool IsActive => DateTime.UtcNow.Subtract(LastActivityAt).TotalMinutes <= 30;
}

/// <summary>
/// 會話統計資訊
/// </summary>
public class SessionStats
{
    /// <summary>
    /// 總會話數
    /// </summary>
    public int TotalSessions { get; set; }

    /// <summary>
    /// 活躍會話數
    /// </summary>
    public int ActiveSessions { get; set; }

    /// <summary>
    /// 過期會話數
    /// </summary>
    public int ExpiredSessions { get; set; }

    /// <summary>
    /// 租戶會話分佈
    /// </summary>
    public Dictionary<Guid, int> TenantSessionCounts { get; set; } = new();

    /// <summary>
    /// 統計時間
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}