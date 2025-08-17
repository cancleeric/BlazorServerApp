using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Services;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// 租戶服務介面
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// 根據 ID 取得租戶
    /// </summary>
    Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據 Slug 取得租戶
    /// </summary>
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據網域取得租戶
    /// </summary>
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有租戶
    /// </summary>
    Task<IEnumerable<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得分頁租戶列表
    /// </summary>
    Task<(IEnumerable<Tenant> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        string? searchTerm = null,
        TenantStatus? status = null,
        TenantType? tenantType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立租戶
    /// </summary>
    Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新租戶
    /// </summary>
    Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除租戶（軟刪除）
    /// </summary>
    Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 啟用租戶
    /// </summary>
    Task ActivateAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 暫停租戶
    /// </summary>
    Task SuspendAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停用租戶
    /// </summary>
    Task DisableAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查 Slug 是否已存在
    /// </summary>
    Task<bool> IsSlugExistsAsync(string slug, Guid? excludeTenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查網域是否已存在
    /// </summary>
    Task<bool> IsDomainExistsAsync(string domain, Guid? excludeTenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶階層
    /// </summary>
    Task<IEnumerable<Tenant>> GetTenantHierarchyAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得子租戶
    /// </summary>
    Task<IEnumerable<Tenant>> GetChildTenantsAsync(Guid parentTenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查租戶是否有效
    /// </summary>
    Task<bool> IsValidTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查租戶功能是否啟用
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default);
}

/// <summary>
/// 租戶上下文服務介面
/// </summary>
public interface ITenantContextService
{
    /// <summary>
    /// 取得當前租戶 ID
    /// </summary>
    Guid? GetCurrentTenantId();

    /// <summary>
    /// 取得當前租戶
    /// </summary>
    Task<Tenant?> GetCurrentTenantAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定當前租戶
    /// </summary>
    void SetCurrentTenant(Guid? tenantId);

    /// <summary>
    /// 檢查是否為超級管理員上下文
    /// </summary>
    bool IsSuperAdminContext();

    /// <summary>
    /// 檢查是否有租戶存取權限
    /// </summary>
    bool HasTenantAccess(Guid tenantId);

    /// <summary>
    /// 檢查功能是否啟用
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(string featureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 繞過租戶篩選執行操作
    /// </summary>
    Task<T> WithoutTenantFilterAsync<T>(Func<Task<T>> operation);

    /// <summary>
    /// 繞過租戶篩選執行操作
    /// </summary>
    Task WithoutTenantFilterAsync(Func<Task> operation);

    /// <summary>
    /// 建立租戶上下文管理器
    /// </summary>
    TenantContextManager CreateContextManager(TenantContext? tenantContext);

    /// <summary>
    /// 建立租戶上下文管理器
    /// </summary>
    TenantContextManager CreateContextManager(Guid? tenantId);
}

/// <summary>
/// 租戶解析服務介面
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// 從 HTTP 請求解析租戶
    /// </summary>
    Task<Tenant?> ResolveAsync(string? host = null, string? path = null, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 從子網域解析租戶
    /// </summary>
    Task<Tenant?> ResolveFromSubdomainAsync(string host, CancellationToken cancellationToken = default);

    /// <summary>
    /// 從路徑解析租戶
    /// </summary>
    Task<Tenant?> ResolveFromPathAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 從標頭解析租戶
    /// </summary>
    Task<Tenant?> ResolveFromHeaderAsync(IDictionary<string, string> headers, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得預設租戶
    /// </summary>
    Task<Tenant?> GetDefaultTenantAsync(CancellationToken cancellationToken = default);
}