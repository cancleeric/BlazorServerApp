using Microsoft.EntityFrameworkCore;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using System.Linq.Expressions;

namespace EnterpriseIDS.Infrastructure.Data.Repositories;

/// <summary>
/// 使用者資料存取實作
/// </summary>
public class UserRepository : TenantAwareRepository<User>, IUserRepository
{
    public UserRepository(
        EnterpriseIdentityDbContext context,
        ITenantContextService tenantContextService) 
        : base(context, tenantContextService)
    {
    }

    #region 基本 CRUD 操作

    /// <summary>
    /// 取得使用者 (依使用者名稱)
    /// </summary>
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    /// <summary>
    /// 取得使用者 (依電子郵件)
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    /// <summary>
    /// 取得使用者 (依 LDAP DN)
    /// </summary>
    public async Task<User?> GetByLdapDnAsync(string ldapDn, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .FirstOrDefaultAsync(u => u.LdapDistinguishedName == ldapDn, cancellationToken);
    }

    /// <summary>
    /// 取得使用者 (依 LDAP 物件 GUID)
    /// </summary>
    public async Task<User?> GetByLdapObjectGuidAsync(string ldapObjectGuid, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .FirstOrDefaultAsync(u => u.LdapObjectGuid == ldapObjectGuid, cancellationToken);
    }

    /// <summary>
    /// 建立使用者
    /// </summary>
    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        return await AddAsync(user, cancellationToken);
    }

    /// <summary>
    /// 更新使用者
    /// </summary>
    public new async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        return await base.UpdateAsync(user, cancellationToken);
    }

    /// <summary>
    /// 刪除使用者
    /// </summary>
    public new async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await base.DeleteAsync(id, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 查詢操作

    /// <summary>
    /// 搜尋使用者
    /// </summary>
    public async Task<IEnumerable<User>> SearchAsync(string searchTerm, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var query = GetTenantQueryableNoTracking();
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            var searchLower = searchTerm.ToLower();
            query = query.Where(u => 
                u.Username.ToLower().Contains(searchLower) ||
                u.Email.ToLower().Contains(searchLower) ||
                u.FirstName.ToLower().Contains(searchLower) ||
                u.LastName.ToLower().Contains(searchLower) ||
                u.DisplayName.ToLower().Contains(searchLower) ||
                (u.Department != null && u.Department.ToLower().Contains(searchLower)));
        }

        return await query
            .OrderBy(u => u.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得所有使用者
    /// </summary>
    public async Task<IEnumerable<User>> GetAllAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .OrderBy(u => u.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得活躍使用者
    /// </summary>
    public async Task<IEnumerable<User>> GetActiveUsersAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .Where(u => u.Status == UserStatus.Active && 
                       (!u.LockoutEndAt.HasValue || u.LockoutEndAt.Value <= DateTime.UtcNow))
            .OrderBy(u => u.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得租戶的使用者
    /// </summary>
    public async Task<IEnumerable<User>> GetByTenantAsync(Guid tenantId, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        return await GetAllForTenantAsync(tenantId, cancellationToken);
    }

    /// <summary>
    /// 取得部門的使用者
    /// </summary>
    public async Task<IEnumerable<User>> GetByDepartmentAsync(string department, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .Where(u => u.Department == department)
            .OrderBy(u => u.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得主管的直屬下屬
    /// </summary>
    public async Task<IEnumerable<User>> GetDirectReportsAsync(Guid managerId, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .Where(u => u.ManagerId == managerId)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得來自 LDAP 的使用者
    /// </summary>
    public async Task<IEnumerable<User>> GetLdapUsersAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .Where(u => !string.IsNullOrEmpty(u.LdapDistinguishedName))
            .OrderBy(u => u.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 取得需要 LDAP 同步的使用者
    /// </summary>
    public async Task<IEnumerable<User>> GetUsersRequiringSyncAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .Where(u => !string.IsNullOrEmpty(u.LdapDistinguishedName) && 
                       (u.LastLdapSyncAt == null || u.LastLdapSyncAt < olderThan))
            .OrderBy(u => u.LastLdapSyncAt)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region 統計與計數

    /// <summary>
    /// 取得使用者總數
    /// </summary>
    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking().CountAsync(cancellationToken);
    }

    /// <summary>
    /// 取得活躍使用者數量
    /// </summary>
    public async Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .CountAsync(u => u.Status == UserStatus.Active && 
                           (!u.LockoutEndAt.HasValue || u.LockoutEndAt.Value <= DateTime.UtcNow), 
                       cancellationToken);
    }

    /// <summary>
    /// 取得租戶的使用者數量
    /// </summary>
    public async Task<int> GetCountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await WithoutTenantFilterAsync(async () =>
        {
            return await _dbSet
                .Where(u => u.TenantId == tenantId)
                .CountAsync(cancellationToken);
        });
    }

    /// <summary>
    /// 取得 LDAP 使用者數量
    /// </summary>
    public async Task<int> GetLdapUserCountAsync(CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .CountAsync(u => !string.IsNullOrEmpty(u.LdapDistinguishedName), cancellationToken);
    }

    #endregion

    #region 認證相關

    /// <summary>
    /// 更新最後登入時間
    /// </summary>
    public async Task UpdateLastLoginAsync(Guid userId, DateTime loginTime, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user != null)
        {
            user.LastLoginAt = loginTime;
            await UpdateAsync(user, cancellationToken);
        }
    }

    /// <summary>
    /// 增加失敗登入次數
    /// </summary>
    public async Task IncrementFailedLoginAttemptsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user != null)
        {
            user.FailedLoginAttempts++;
            await UpdateAsync(user, cancellationToken);
        }
    }

    /// <summary>
    /// 重設失敗登入次數
    /// </summary>
    public async Task ResetFailedLoginAttemptsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user != null)
        {
            user.ResetFailedLoginAttempts();
            await UpdateAsync(user, cancellationToken);
        }
    }

    /// <summary>
    /// 鎖定使用者
    /// </summary>
    public async Task LockUserAsync(Guid userId, DateTime lockoutEndTime, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user != null)
        {
            user.Status = UserStatus.Locked;
            user.LockedAt = DateTime.UtcNow;
            user.LockoutEndAt = lockoutEndTime;
            await UpdateAsync(user, cancellationToken);
        }
    }

    /// <summary>
    /// 解鎖使用者
    /// </summary>
    public async Task UnlockUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user != null)
        {
            user.Status = UserStatus.Active;
            user.LockedAt = null;
            user.LockoutEndAt = null;
            user.FailedLoginAttempts = 0;
            await UpdateAsync(user, cancellationToken);
        }
    }

    #endregion

    #region LDAP 同步相關

    /// <summary>
    /// 更新 LDAP 同步資訊
    /// </summary>
    public async Task UpdateLdapSyncInfoAsync(Guid userId, string syncHash, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user != null)
        {
            user.LdapSyncHash = syncHash;
            user.LastLdapSyncAt = DateTime.UtcNow;
            await UpdateAsync(user, cancellationToken);
        }
    }

    /// <summary>
    /// 批次更新 LDAP 同步資訊
    /// </summary>
    public async Task BatchUpdateLdapSyncInfoAsync(IEnumerable<(Guid UserId, string SyncHash)> updates, CancellationToken cancellationToken = default)
    {
        var userIds = updates.Select(u => u.UserId).ToList();
        var users = await GetTenantQueryable()
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        var updateDict = updates.ToDictionary(u => u.UserId, u => u.SyncHash);
        var currentTime = DateTime.UtcNow;

        foreach (var user in users)
        {
            if (updateDict.TryGetValue(user.Id, out var syncHash))
            {
                user.LdapSyncHash = syncHash;
                user.LastLdapSyncAt = currentTime;
            }
        }

        await SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 停用不存在於 LDAP 的使用者
    /// </summary>
    public async Task DeactivateOrphanedLdapUsersAsync(IEnumerable<string> activeLdapObjectGuids, CancellationToken cancellationToken = default)
    {
        var activeGuids = activeLdapObjectGuids.ToList();
        
        var orphanedUsers = await GetTenantQueryable()
            .Where(u => !string.IsNullOrEmpty(u.LdapObjectGuid) && 
                       !activeGuids.Contains(u.LdapObjectGuid) &&
                       u.Status == UserStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var user in orphanedUsers)
        {
            user.Status = UserStatus.Disabled;
        }

        if (orphanedUsers.Any())
        {
            await SaveChangesAsync(cancellationToken);
        }
    }

    #endregion

    #region 批次操作

    /// <summary>
    /// 批次建立使用者
    /// </summary>
    public async Task<IEnumerable<User>> BatchCreateAsync(IEnumerable<User> users, CancellationToken cancellationToken = default)
    {
        var userList = users.ToList();
        
        // 自動設定租戶 ID
        var currentTenantId = _tenantContextService.GetCurrentTenantId();
        foreach (var user in userList.Where(u => u.TenantId == Guid.Empty))
        {
            if (currentTenantId.HasValue)
            {
                user.TenantId = currentTenantId.Value;
            }
            else if (!_tenantContextService.IsSuperAdminContext())
            {
                throw new InvalidOperationException("無法確定當前租戶，無法批次新增使用者");
            }
        }

        await _dbSet.AddRangeAsync(userList, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        
        return userList;
    }

    /// <summary>
    /// 批次更新使用者
    /// </summary>
    public async Task<IEnumerable<User>> BatchUpdateAsync(IEnumerable<User> users, CancellationToken cancellationToken = default)
    {
        var userList = users.ToList();
        
        // 檢查租戶權限
        foreach (var user in userList)
        {
            if (!_tenantContextService.IsSuperAdminContext() && 
                !_tenantContextService.HasTenantAccess(user.TenantId))
            {
                throw new UnauthorizedAccessException($"沒有權限修改租戶 {user.TenantId} 的使用者資料");
            }
        }

        _dbSet.UpdateRange(userList);
        await SaveChangesAsync(cancellationToken);
        
        return userList;
    }

    /// <summary>
    /// 批次刪除使用者
    /// </summary>
    public async Task<int> BatchDeleteAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var userIdList = userIds.ToList();
        var users = await GetTenantQueryable()
            .Where(u => userIdList.Contains(u.Id))
            .ToListAsync(cancellationToken);

        // 檢查租戶權限
        foreach (var user in users)
        {
            if (!_tenantContextService.IsSuperAdminContext() && 
                !_tenantContextService.HasTenantAccess(user.TenantId))
            {
                throw new UnauthorizedAccessException($"沒有權限刪除租戶 {user.TenantId} 的使用者");
            }
        }

        _dbSet.RemoveRange(users);
        await SaveChangesAsync(cancellationToken);
        
        return users.Count;
    }

    #endregion

    #region 驗證方法

    /// <summary>
    /// 檢查使用者名稱是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .AnyAsync(u => u.Username == username, cancellationToken);
    }

    /// <summary>
    /// 檢查電子郵件是否存在
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .AnyAsync(u => u.Email == email, cancellationToken);
    }

    /// <summary>
    /// 檢查電子郵件是否存在 (排除指定使用者)
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email, Guid excludeUserId, CancellationToken cancellationToken = default)
    {
        return await GetTenantQueryableNoTracking()
            .AnyAsync(u => u.Email == email && u.Id != excludeUserId, cancellationToken);
    }

    #endregion
}