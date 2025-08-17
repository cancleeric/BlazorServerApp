using EnterpriseIDS.Core.Entities;
using System.Linq.Expressions;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// 租戶儲存庫介面
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// 根據 ID 取得租戶
    /// </summary>
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

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
    /// 根據條件查詢租戶
    /// </summary>
    Task<IEnumerable<Tenant>> FindAsync(Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得分頁租戶
    /// </summary>
    Task<(IEnumerable<Tenant> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<Tenant, bool>>? predicate = null,
        Expression<Func<Tenant, object>>? orderBy = null,
        bool orderByDescending = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增租戶
    /// </summary>
    Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新租戶
    /// </summary>
    Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除租戶（軟刪除）
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 硬刪除租戶
    /// </summary>
    Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查是否存在
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查 Slug 是否存在
    /// </summary>
    Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查網域是否存在
    /// </summary>
    Task<bool> DomainExistsAsync(string domain, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得子租戶
    /// </summary>
    Task<IEnumerable<Tenant>> GetChildTenantsAsync(Guid parentTenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶階層（從子到父）
    /// </summary>
    Task<IEnumerable<Tenant>> GetTenantHierarchyAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得啟用的租戶
    /// </summary>
    Task<IEnumerable<Tenant>> GetActiveTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得預設租戶
    /// </summary>
    Task<Tenant?> GetDefaultTenantAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 計算租戶數量
    /// </summary>
    Task<int> CountAsync(Expression<Func<Tenant, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次更新租戶狀態
    /// </summary>
    Task BatchUpdateStatusAsync(IEnumerable<Guid> tenantIds, TenantStatus status, CancellationToken cancellationToken = default);
}

/// <summary>
/// 租戶配置儲存庫介面
/// </summary>
public interface ITenantConfigurationRepository
{
    /// <summary>
    /// 根據 ID 取得配置
    /// </summary>
    Task<TenantConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的特定配置
    /// </summary>
    Task<TenantConfiguration?> GetByKeyAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的所有配置
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的分類配置
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據條件查詢配置
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> FindAsync(Expression<Func<TenantConfiguration, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增配置
    /// </summary>
    Task<TenantConfiguration> AddAsync(TenantConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次新增配置
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> AddRangeAsync(IEnumerable<TenantConfiguration> configurations, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新配置
    /// </summary>
    Task<TenantConfiguration> UpdateAsync(TenantConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除配置
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除租戶的特定配置
    /// </summary>
    Task DeleteByKeyAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除租戶的所有配置
    /// </summary>
    Task DeleteAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查配置是否存在
    /// </summary>
    Task<bool> ExistsAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得配置歷史
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> GetHistoryAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得有效配置（考慮生效日期和過期日期）
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> GetEffectiveConfigurationsAsync(Guid tenantId, DateTime? effectiveDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 複製配置到其他租戶
    /// </summary>
    Task CopyConfigurationsAsync(Guid sourceTenantId, Guid targetTenantId, string[]? configKeys = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次更新配置
    /// </summary>
    Task BatchUpdateAsync(IEnumerable<TenantConfiguration> configurations, CancellationToken cancellationToken = default);

    /// <summary>
    /// 搜尋配置
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> SearchAsync(string searchTerm, Guid? tenantId = null, string? category = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// 基礎儲存庫介面
/// </summary>
/// <typeparam name="TEntity">實體類型</typeparam>
public interface IBaseRepository<TEntity> where TEntity : BaseEntity
{
    /// <summary>
    /// 根據 ID 取得實體
    /// </summary>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有實體
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據條件查詢實體
    /// </summary>
    Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增實體
    /// </summary>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新實體
    /// </summary>
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除實體
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查實體是否存在
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 計算實體數量
    /// </summary>
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 儲存變更
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 支援租戶感知的儲存庫介面
/// </summary>
/// <typeparam name="TEntity">實體類型</typeparam>
public interface ITenantAwareRepository<TEntity> : IBaseRepository<TEntity> where TEntity : TenantAwareEntity
{
    /// <summary>
    /// 取得當前租戶的所有實體
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得指定租戶的所有實體
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據條件查詢當前租戶的實體
    /// </summary>
    Task<IEnumerable<TEntity>> FindForCurrentTenantAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據條件查詢指定租戶的實體
    /// </summary>
    Task<IEnumerable<TEntity>> FindForTenantAsync(Guid tenantId, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}