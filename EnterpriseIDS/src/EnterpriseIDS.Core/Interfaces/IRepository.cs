using EnterpriseIDS.Core.Entities;
using System.Linq.Expressions;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// 基礎儲存庫介面
/// </summary>
/// <typeparam name="TEntity">實體類型</typeparam>
public interface IRepository<TEntity> where TEntity : BaseEntity
{
    /// <summary>
    /// 根據 ID 取得實體
    /// </summary>
    /// <param name="id">實體 ID</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>實體</returns>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有實體
    /// </summary>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>實體集合</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據條件查詢實體
    /// </summary>
    /// <param name="predicate">查詢條件</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>符合條件的實體集合</returns>
    Task<IEnumerable<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據條件取得第一個實體
    /// </summary>
    /// <param name="predicate">查詢條件</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>第一個符合條件的實體</returns>
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查是否存在符合條件的實體
    /// </summary>
    /// <param name="predicate">查詢條件</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>是否存在</returns>
    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 計算符合條件的實體數量
    /// </summary>
    /// <param name="predicate">查詢條件</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>實體數量</returns>
    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得分頁實體
    /// </summary>
    /// <param name="pageNumber">頁碼 (從 1 開始)</param>
    /// <param name="pageSize">每頁大小</param>
    /// <param name="predicate">查詢條件</param>
    /// <param name="orderBy">排序表達式</param>
    /// <param name="orderByDescending">是否降序排列</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>分頁結果</returns>
    Task<(IEnumerable<TEntity> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        bool orderByDescending = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增實體
    /// </summary>
    /// <param name="entity">要新增的實體</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>新增的實體</returns>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次新增實體
    /// </summary>
    /// <param name="entities">要新增的實體集合</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>新增的實體集合</returns>
    Task<IEnumerable<TEntity>> AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新實體
    /// </summary>
    /// <param name="entity">要更新的實體</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>更新的實體</returns>
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次更新實體
    /// </summary>
    /// <param name="entities">要更新的實體集合</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>更新的實體集合</returns>
    Task<IEnumerable<TEntity>> UpdateRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除實體
    /// </summary>
    /// <param name="entity">要刪除的實體</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>是否成功刪除</returns>
    Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據 ID 刪除實體
    /// </summary>
    /// <param name="id">實體 ID</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>是否成功刪除</returns>
    Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次刪除實體
    /// </summary>
    /// <param name="entities">要刪除的實體集合</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>刪除的實體數量</returns>
    Task<int> DeleteRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據條件批次刪除實體
    /// </summary>
    /// <param name="predicate">刪除條件</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>刪除的實體數量</returns>
    Task<int> DeleteWhereAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 儲存變更
    /// </summary>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>影響的實體數量</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}