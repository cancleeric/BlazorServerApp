using Microsoft.EntityFrameworkCore;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using System.Linq.Expressions;

namespace EnterpriseIDS.Infrastructure.Data.Repositories;

/// <summary>
/// 基礎儲存庫實作
/// </summary>
/// <typeparam name="TEntity">實體類型</typeparam>
public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : BaseEntity
{
    protected readonly EnterpriseIdentityDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    public BaseRepository(EnterpriseIdentityDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    /// <summary>
    /// 根據 ID 取得實體
    /// </summary>
    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    /// <summary>
    /// 取得所有實體
    /// </summary>
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根據條件查詢實體
    /// </summary>
    public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 新增實體
    /// </summary>
    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var entry = await _dbSet.AddAsync(entity, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    /// <summary>
    /// 更新實體
    /// </summary>
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        await SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <summary>
    /// 刪除實體
    /// </summary>
    public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 檢查實體是否存在
    /// </summary>
    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(e => e.Id == id, cancellationToken);
    }

    /// <summary>
    /// 計算實體數量
    /// </summary>
    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        if (predicate != null)
        {
            return await _dbSet.CountAsync(predicate, cancellationToken);
        }
        return await _dbSet.CountAsync(cancellationToken);
    }

    /// <summary>
    /// 儲存變更
    /// </summary>
    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 取得查詢物件
    /// </summary>
    protected virtual IQueryable<TEntity> GetQueryable()
    {
        return _dbSet.AsQueryable();
    }

    /// <summary>
    /// 取得不追蹤的查詢物件
    /// </summary>
    protected virtual IQueryable<TEntity> GetQueryableNoTracking()
    {
        return _dbSet.AsNoTracking();
    }
}

/// <summary>
/// 支援租戶感知的儲存庫實作
/// </summary>
/// <typeparam name="TEntity">實體類型</typeparam>
public class TenantAwareRepository<TEntity> : BaseRepository<TEntity>, IRepository<TEntity>, ITenantAwareRepository<TEntity> 
    where TEntity : TenantAwareEntity
{
    protected readonly ITenantContextService _tenantContextService;

    public TenantAwareRepository(
        EnterpriseIdentityDbContext context,
        ITenantContextService tenantContextService) 
        : base(context)
    {
        _tenantContextService = tenantContextService;
    }

    /// <summary>
    /// 取得當前租戶的所有實體
    /// </summary>
    public virtual async Task<IEnumerable<TEntity>> GetAllForCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var currentTenantId = _tenantContextService.GetCurrentTenantId();
        if (currentTenantId == null)
        {
            return new List<TEntity>();
        }

        return await GetAllForTenantAsync(currentTenantId.Value, cancellationToken);
    }

    /// <summary>
    /// 取得指定租戶的所有實體
    /// </summary>
    public virtual async Task<IEnumerable<TEntity>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(e => e.TenantId == tenantId).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根據條件查詢當前租戶的實體
    /// </summary>
    public virtual async Task<IEnumerable<TEntity>> FindForCurrentTenantAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var currentTenantId = _tenantContextService.GetCurrentTenantId();
        if (currentTenantId == null)
        {
            return new List<TEntity>();
        }

        return await FindForTenantAsync(currentTenantId.Value, predicate, cancellationToken);
    }

    /// <summary>
    /// 根據條件查詢指定租戶的實體
    /// </summary>
    public virtual async Task<IEnumerable<TEntity>> FindForTenantAsync(Guid tenantId, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(e => e.TenantId == tenantId)
            .Where(predicate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 新增實體（自動設定租戶 ID）
    /// </summary>
    public override async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        // 自動設定租戶 ID（如果尚未設定）
        if (entity.TenantId == Guid.Empty)
        {
            var currentTenantId = _tenantContextService.GetCurrentTenantId();
            if (currentTenantId.HasValue)
            {
                entity.TenantId = currentTenantId.Value;
            }
            else if (!_tenantContextService.IsSuperAdminContext())
            {
                throw new InvalidOperationException("無法確定當前租戶，無法新增實體");
            }
        }

        return await base.AddAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 更新實體（檢查租戶權限）
    /// </summary>
    public override async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        // 檢查租戶存取權限
        if (!_tenantContextService.IsSuperAdminContext() && 
            !_tenantContextService.HasTenantAccess(entity.TenantId))
        {
            throw new UnauthorizedAccessException("沒有權限修改此租戶的資料");
        }

        return await base.UpdateAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 刪除實體（檢查租戶權限）
    /// </summary>
    public override async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            // 檢查租戶存取權限
            if (!_tenantContextService.IsSuperAdminContext() && 
                !_tenantContextService.HasTenantAccess(entity.TenantId))
            {
                throw new UnauthorizedAccessException("沒有權限刪除此租戶的資料");
            }

            await base.DeleteAsync(id, cancellationToken);
        }
    }

    /// <summary>
    /// 取得租戶感知的查詢物件
    /// </summary>
    protected virtual IQueryable<TEntity> GetTenantQueryable()
    {
        var currentTenantId = _tenantContextService.GetCurrentTenantId();
        
        if (_tenantContextService.IsSuperAdminContext() || currentTenantId == null)
        {
            return _dbSet.AsQueryable();
        }

        return _dbSet.Where(e => e.TenantId == currentTenantId.Value);
    }

    /// <summary>
    /// 取得租戶感知的不追蹤查詢物件
    /// </summary>
    protected virtual IQueryable<TEntity> GetTenantQueryableNoTracking()
    {
        return GetTenantQueryable().AsNoTracking();
    }

    /// <summary>
    /// 繞過租戶篩選執行操作
    /// </summary>
    protected virtual async Task<T> WithoutTenantFilterAsync<T>(Func<Task<T>> operation)
    {
        return await _context.WithoutTenantFilterAsync(operation);
    }

    /// <summary>
    /// 繞過租戶篩選執行操作
    /// </summary>
    protected virtual async Task WithoutTenantFilterAsync(Func<Task> operation)
    {
        await _context.WithoutTenantFilterAsync(operation);
    }

    #region IRepository 額外的介面方法實作

    /// <summary>
    /// 根據條件取得第一個實體
    /// </summary>
    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// 檢查是否存在符合條件的實體
    /// </summary>
    public virtual async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .AnyAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// 取得分頁實體
    /// </summary>
    public virtual async Task<(IEnumerable<TEntity> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        bool orderByDescending = false,
        CancellationToken cancellationToken = default)
    {
        var query = GetTenantQueryableNoTracking();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (orderBy != null)
        {
            query = orderByDescending
                ? query.OrderByDescending(orderBy)
                : query.OrderBy(orderBy);
        }

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// 批次新增實體
    /// </summary>
    public virtual async Task<IEnumerable<TEntity>> AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        var currentTenantId = _tenantContextService.GetCurrentTenantId();

        foreach (var entity in entityList)
        {
            if (entity.TenantId == Guid.Empty)
            {
                if (currentTenantId.HasValue)
                {
                    entity.TenantId = currentTenantId.Value;
                }
                else if (!_tenantContextService.IsSuperAdminContext())
                {
                    throw new InvalidOperationException("無法確定當前租戶，無法批次新增實體");
                }
            }
        }

        await _dbSet.AddRangeAsync(entityList, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return entityList;
    }

    /// <summary>
    /// 批次更新實體
    /// </summary>
    public virtual async Task<IEnumerable<TEntity>> UpdateRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();

        foreach (var entity in entityList)
        {
            if (!_tenantContextService.IsSuperAdminContext() && 
                !_tenantContextService.HasTenantAccess(entity.TenantId))
            {
                throw new UnauthorizedAccessException($"沒有權限修改租戶 {entity.TenantId} 的實體");
            }
        }

        _dbSet.UpdateRange(entityList);
        await SaveChangesAsync(cancellationToken);
        return entityList;
    }

    /// <summary>
    /// 刪除實體
    /// </summary>
    public virtual async Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (!_tenantContextService.IsSuperAdminContext() && 
            !_tenantContextService.HasTenantAccess(entity.TenantId))
        {
            throw new UnauthorizedAccessException($"沒有權限刪除租戶 {entity.TenantId} 的實體");
        }

        _dbSet.Remove(entity);
        await SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// 根據 ID 刪除實體
    /// </summary>
    public virtual async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetTenantQueryable()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        return await DeleteAsync(entity, cancellationToken);
    }

    /// <summary>
    /// 批次刪除實體
    /// </summary>
    public virtual async Task<int> DeleteRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();

        foreach (var entity in entityList)
        {
            if (!_tenantContextService.IsSuperAdminContext() && 
                !_tenantContextService.HasTenantAccess(entity.TenantId))
            {
                throw new UnauthorizedAccessException($"沒有權限刪除租戶 {entity.TenantId} 的實體");
            }
        }

        _dbSet.RemoveRange(entityList);
        await SaveChangesAsync(cancellationToken);
        return entityList.Count;
    }

    /// <summary>
    /// 根據條件批次刪除實體
    /// </summary>
    public virtual async Task<int> DeleteWhereAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entities = await GetTenantQueryable()
            .Where(predicate)
            .ToListAsync(cancellationToken);

        return await DeleteRangeAsync(entities, cancellationToken);
    }

    #endregion
}