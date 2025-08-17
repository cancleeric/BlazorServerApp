using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// 角色資料存取介面
/// </summary>
public interface IRoleRepository
{
    #region 基本 CRUD 操作

    /// <summary>
    /// 取得角色 (依 ID)
    /// </summary>
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得角色 (依角色名稱)
    /// </summary>
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立角色
    /// </summary>
    Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新角色
    /// </summary>
    Task<Role> UpdateAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除角色
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    #endregion

    #region 查詢操作

    /// <summary>
    /// 搜尋角色
    /// </summary>
    Task<IEnumerable<Role>> SearchAsync(string searchTerm, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有角色
    /// </summary>
    Task<IEnumerable<Role>> GetAllAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的角色
    /// </summary>
    Task<IEnumerable<Role>> GetByTenantAsync(Guid tenantId, int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得系統角色
    /// </summary>
    Task<IEnumerable<Role>> GetSystemRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得預設角色
    /// </summary>
    Task<IEnumerable<Role>> GetDefaultRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得使用者的角色
    /// </summary>
    Task<IEnumerable<Role>> GetUserRolesAsync(Guid userId, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組的角色
    /// </summary>
    Task<IEnumerable<Role>> GetGroupRolesAsync(Guid groupId, bool includeInactive = false, CancellationToken cancellationToken = default);

    #endregion

    #region 權限操作

    /// <summary>
    /// 取得角色的權限
    /// </summary>
    Task<IEnumerable<RolePermission>> GetRolePermissionsAsync(Guid roleId, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得角色的有效權限清單
    /// </summary>
    Task<IEnumerable<string>> GetEffectivePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查角色是否擁有權限
    /// </summary>
    Task<bool> HasPermissionAsync(Guid roleId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增角色權限
    /// </summary>
    Task<RolePermission> AddPermissionAsync(Guid roleId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除角色權限
    /// </summary>
    Task<bool> RemovePermissionAsync(Guid roleId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次新增權限
    /// </summary>
    Task<int> BatchAddPermissionsAsync(Guid roleId, IEnumerable<string> permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次移除權限
    /// </summary>
    Task<int> BatchRemovePermissionsAsync(Guid roleId, IEnumerable<string> permissions, CancellationToken cancellationToken = default);

    #endregion

    #region 使用者角色管理

    /// <summary>
    /// 取得角色的使用者分配
    /// </summary>
    Task<IEnumerable<UserRole>> GetUserRoleAssignmentsAsync(Guid roleId, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得角色的使用者
    /// </summary>
    Task<IEnumerable<User>> GetUsersWithRoleAsync(Guid roleId, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分配角色給使用者
    /// </summary>
    Task<UserRole> AssignRoleToUserAsync(Guid roleId, Guid userId, string? assignmentSource = null, DateTime? expiresAt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除使用者的角色
    /// </summary>
    Task<bool> RemoveRoleFromUserAsync(Guid roleId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次分配角色
    /// </summary>
    Task<int> BatchAssignRoleAsync(Guid roleId, IEnumerable<Guid> userIds, string? assignmentSource = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次移除角色
    /// </summary>
    Task<int> BatchRemoveRoleAsync(Guid roleId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    #endregion

    #region 群組角色映射

    /// <summary>
    /// 取得角色的群組映射
    /// </summary>
    Task<IEnumerable<GroupRoleMapping>> GetGroupRoleMappingsAsync(Guid roleId, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得角色的群組
    /// </summary>
    Task<IEnumerable<Group>> GetGroupsWithRoleAsync(Guid roleId, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 映射角色到群組
    /// </summary>
    Task<GroupRoleMapping> MapRoleToGroupAsync(Guid roleId, Guid groupId, bool inheritToChildGroups = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除群組的角色映射
    /// </summary>
    Task<bool> RemoveRoleFromGroupAsync(Guid roleId, Guid groupId, CancellationToken cancellationToken = default);

    #endregion

    #region 統計與計數

    /// <summary>
    /// 取得角色總數
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的角色數量
    /// </summary>
    Task<int> GetCountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得系統角色數量
    /// </summary>
    Task<int> GetSystemRoleCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得角色的使用者數量
    /// </summary>
    Task<int> GetUserCountAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得角色的群組數量
    /// </summary>
    Task<int> GetGroupCountAsync(Guid roleId, CancellationToken cancellationToken = default);

    #endregion

    #region 批次操作

    /// <summary>
    /// 批次建立角色
    /// </summary>
    Task<IEnumerable<Role>> BatchCreateAsync(IEnumerable<Role> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次更新角色
    /// </summary>
    Task<IEnumerable<Role>> BatchUpdateAsync(IEnumerable<Role> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次刪除角色
    /// </summary>
    Task<int> BatchDeleteAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken = default);

    #endregion

    #region 驗證方法

    /// <summary>
    /// 檢查角色名稱是否存在
    /// </summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查角色名稱是否存在 (排除指定角色)
    /// </summary>
    Task<bool> ExistsAsync(string name, Guid excludeRoleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查角色是否可以刪除
    /// </summary>
    Task<bool> CanDeleteAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查角色是否可以修改
    /// </summary>
    Task<bool> CanModifyAsync(Guid roleId, CancellationToken cancellationToken = default);

    #endregion
}