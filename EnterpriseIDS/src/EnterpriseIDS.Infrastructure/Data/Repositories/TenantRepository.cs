using Microsoft.EntityFrameworkCore;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using System.Linq.Expressions;

namespace EnterpriseIDS.Infrastructure.Data.Repositories;

/// <summary>
/// 租戶儲存庫實作
/// </summary>
public class TenantRepository : BaseRepository<Tenant>, ITenantRepository
{
    public TenantRepository(EnterpriseIdentityDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 根據 Slug 取得租戶
    /// </summary>
    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(slug))
            return null;

        return await _dbSet
            .FirstOrDefaultAsync(t => t.Slug == slug.ToLowerInvariant(), cancellationToken);
    }

    /// <summary>
    /// 根據網域取得租戶
    /// </summary>
    public async Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(domain))
            return null;

        var normalizedDomain = domain.ToLowerInvariant();

        // 先檢查主要網域
        var tenant = await _dbSet
            .FirstOrDefaultAsync(t => t.PrimaryDomain == normalizedDomain, cancellationToken);

        if (tenant != null)
            return tenant;

        // 檢查允許的網域列表
        var tenantsWithDomains = await _dbSet
            .Where(t => t.AllowedDomainsJson != null && t.AllowedDomainsJson != "")
            .ToListAsync(cancellationToken);

        foreach (var t in tenantsWithDomains)
        {
            if (t.IsDomainAllowed(domain))
            {
                return t;
            }
        }

        return null;
    }

    /// <summary>
    /// 取得分頁租戶
    /// </summary>
    public async Task<(IEnumerable<Tenant> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<Tenant, bool>>? predicate = null,
        Expression<Func<Tenant, object>>? orderBy = null,
        bool orderByDescending = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        // 應用篩選條件
        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        // 計算總數
        var totalCount = await query.CountAsync(cancellationToken);

        // 應用排序
        if (orderBy != null)
        {
            query = orderByDescending
                ? query.OrderByDescending(orderBy)
                : query.OrderBy(orderBy);
        }
        else
        {
            // 預設按建立時間降序排列
            query = query.OrderByDescending(t => t.CreatedAt);
        }

        // 應用分頁
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// 檢查 Slug 是否存在
    /// </summary>
    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(slug))
            return false;

        var query = _dbSet.Where(t => t.Slug == slug.ToLowerInvariant());

        if (excludeId.HasValue)
        {
            query = query.Where(t => t.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// 檢查網域是否存在
    /// </summary>
    public async Task<bool> DomainExistsAsync(string domain, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(domain))
            return false;

        var normalizedDomain = domain.ToLowerInvariant();
        var query = _dbSet.Where(t => t.PrimaryDomain == normalizedDomain);

        if (excludeId.HasValue)
        {
            query = query.Where(t => t.Id != excludeId.Value);
        }

        var exists = await query.AnyAsync(cancellationToken);
        if (exists)
            return true;

        // 檢查允許的網域列表
        var tenantsWithDomains = await _dbSet
            .Where(t => t.AllowedDomainsJson != null && t.AllowedDomainsJson != "")
            .Where(t => !excludeId.HasValue || t.Id != excludeId.Value)
            .ToListAsync(cancellationToken);

        return tenantsWithDomains.Any(t => t.IsDomainAllowed(domain));
    }

    /// <summary>
    /// 取得子租戶
    /// </summary>
    public async Task<IEnumerable<Tenant>> GetChildTenantsAsync(Guid parentTenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.ParentTenantId == parentTenantId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得租戶階層（從子到父）
    /// </summary>
    public async Task<IEnumerable<Tenant>> GetTenantHierarchyAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var hierarchy = new List<Tenant>();
        var currentTenant = await GetByIdAsync(tenantId, cancellationToken);

        while (currentTenant != null)
        {
            hierarchy.Add(currentTenant);

            if (currentTenant.ParentTenantId.HasValue)
            {
                currentTenant = await GetByIdAsync(currentTenant.ParentTenantId.Value, cancellationToken);
            }
            else
            {
                break;
            }

            // 防止無限迴圈
            if (hierarchy.Count > 10)
            {
                break;
            }
        }

        return hierarchy;
    }

    /// <summary>
    /// 取得啟用的租戶
    /// </summary>
    public async Task<IEnumerable<Tenant>> GetActiveTenantsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.Status == TenantStatus.Active)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得預設租戶
    /// </summary>
    public async Task<Tenant?> GetDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        // 尋找標記為預設的租戶（透過 metadata 或特殊標識）
        var defaultTenant = await _dbSet
            .Where(t => t.Status == TenantStatus.Active)
            .Where(t => t.MetadataJson != null && t.MetadataJson.Contains("\"isDefault\":true"))
            .FirstOrDefaultAsync(cancellationToken);

        // 如果沒有找到預設租戶，返回第一個啟用的租戶
        if (defaultTenant == null)
        {
            defaultTenant = await _dbSet
                .Where(t => t.Status == TenantStatus.Active)
                .OrderBy(t => t.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return defaultTenant;
    }

    /// <summary>
    /// 批次更新租戶狀態
    /// </summary>
    public async Task BatchUpdateStatusAsync(IEnumerable<Guid> tenantIds, TenantStatus status, CancellationToken cancellationToken = default)
    {
        var tenants = await _dbSet
            .Where(t => tenantIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            tenant.Status = status;
            tenant.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 硬刪除租戶
    /// </summary>
    public async Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await GetByIdAsync(id, cancellationToken);
        if (tenant != null)
        {
            // 先刪除相關的配置
            var configurations = await _context.TenantConfigurations
                .Where(tc => tc.TenantId == id)
                .ToListAsync(cancellationToken);

            _context.TenantConfigurations.RemoveRange(configurations);

            // 檢查是否有子租戶
            var childTenants = await GetChildTenantsAsync(id, cancellationToken);
            if (childTenants.Any())
            {
                throw new InvalidOperationException("無法刪除有子租戶的租戶，請先處理子租戶");
            }

            // 刪除租戶
            _dbSet.Remove(tenant);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 搜尋租戶
    /// </summary>
    public async Task<IEnumerable<Tenant>> SearchAsync(
        string searchTerm,
        TenantStatus? status = null,
        TenantType? tenantType = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        // 搜尋條件
        if (!string.IsNullOrEmpty(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            query = query.Where(t =>
                t.Name.ToLowerInvariant().Contains(term) ||
                t.Slug.ToLowerInvariant().Contains(term) ||
                (t.ContactEmail != null && t.ContactEmail.ToLowerInvariant().Contains(term)) ||
                (t.PrimaryDomain != null && t.PrimaryDomain.ToLowerInvariant().Contains(term)));
        }

        // 狀態篩選
        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        // 類型篩選
        if (tenantType.HasValue)
        {
            query = query.Where(t => t.TenantType == tenantType.Value);
        }

        // 排序
        query = query.OrderBy(t => t.Name);

        // 限制結果數量
        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得租戶統計資訊
    /// </summary>
    public async Task<TenantStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var statistics = new TenantStatistics();

        // 總租戶數
        statistics.TotalTenants = await _dbSet.CountAsync(cancellationToken);

        // 按狀態分組統計
        var statusGroups = await _dbSet
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var group in statusGroups)
        {
            switch (group.Status)
            {
                case TenantStatus.Active:
                    statistics.ActiveTenants = group.Count;
                    break;
                case TenantStatus.Pending:
                    statistics.PendingTenants = group.Count;
                    break;
                case TenantStatus.Suspended:
                    statistics.SuspendedTenants = group.Count;
                    break;
                case TenantStatus.Disabled:
                    statistics.DisabledTenants = group.Count;
                    break;
            }
        }

        // 按類型分組統計
        var typeGroups = await _dbSet
            .GroupBy(t => t.TenantType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        statistics.TenantsByType = typeGroups.ToDictionary(g => g.Type, g => g.Count);

        // 最近建立的租戶數（30天內）
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        statistics.RecentlyCreatedTenants = await _dbSet
            .CountAsync(t => t.CreatedAt >= thirtyDaysAgo, cancellationToken);

        return statistics;
    }
}

/// <summary>
/// 租戶統計資訊
/// </summary>
public class TenantStatistics
{
    /// <summary>
    /// 總租戶數
    /// </summary>
    public int TotalTenants { get; set; }

    /// <summary>
    /// 啟用的租戶數
    /// </summary>
    public int ActiveTenants { get; set; }

    /// <summary>
    /// 待核准的租戶數
    /// </summary>
    public int PendingTenants { get; set; }

    /// <summary>
    /// 暫停的租戶數
    /// </summary>
    public int SuspendedTenants { get; set; }

    /// <summary>
    /// 停用的租戶數
    /// </summary>
    public int DisabledTenants { get; set; }

    /// <summary>
    /// 最近建立的租戶數（30天內）
    /// </summary>
    public int RecentlyCreatedTenants { get; set; }

    /// <summary>
    /// 按類型統計的租戶數
    /// </summary>
    public Dictionary<TenantType, int> TenantsByType { get; set; } = new();

    /// <summary>
    /// 計算啟用率
    /// </summary>
    public double ActiveRate => TotalTenants > 0 ? (double)ActiveTenants / TotalTenants * 100 : 0;

    /// <summary>
    /// 計算成長率（基於最近建立的租戶）
    /// </summary>
    public double GrowthRate => TotalTenants > 0 ? (double)RecentlyCreatedTenants / TotalTenants * 100 : 0;
}