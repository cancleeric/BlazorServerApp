using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// 群組資料存取介面
/// </summary>
public interface IGroupRepository
{
    #region 基本 CRUD 操作

    /// <summary>
    /// 取得群組 (依 ID)
    /// </summary>
    Task<Group?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組 (依群組名稱)
    /// </summary>
    Task<Group?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組 (依 LDAP DN)
    /// </summary>
    Task<Group?> GetByLdapDnAsync(string ldapDn, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組 (依 LDAP 物件 GUID)
    /// </summary>
    Task<Group?> GetByLdapObjectGuidAsync(string ldapObjectGuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立群組
    /// </summary>
    Task<Group> CreateAsync(Group group, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新群組
    /// </summary>
    Task<Group> UpdateAsync(Group group, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除群組
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    #endregion

    #region 查詢操作

    /// <summary>
    /// 搜尋群組
    /// </summary>
    Task<IEnumerable<Group>> SearchAsync(string searchTerm, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有群組
    /// </summary>
    Task<IEnumerable<Group>> GetAllAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的群組
    /// </summary>
    Task<IEnumerable<Group>> GetByTenantAsync(Guid tenantId, int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得根群組 (沒有父群組)
    /// </summary>
    Task<IEnumerable<Group>> GetRootGroupsAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得子群組
    /// </summary>
    Task<IEnumerable<Group>> GetChildGroupsAsync(Guid parentGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得父群組
    /// </summary>
    Task<Group?> GetParentGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得來自 LDAP 的群組
    /// </summary>
    Task<IEnumerable<Group>> GetLdapGroupsAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得需要 LDAP 同步的群組
    /// </summary>
    Task<IEnumerable<Group>> GetGroupsRequiringSyncAsync(DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得系統群組
    /// </summary>
    Task<IEnumerable<Group>> GetSystemGroupsAsync(CancellationToken cancellationToken = default);

    #endregion

    #region 階層操作

    /// <summary>
    /// 取得群組階層路徑
    /// </summary>
    Task<IEnumerable<Group>> GetHierarchyPathAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有子孫群組 (遞迴)
    /// </summary>
    Task<IEnumerable<Group>> GetAllDescendantsAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有祖先群組 (遞迴)
    /// </summary>
    Task<IEnumerable<Group>> GetAllAncestorsAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查群組是否為另一個群組的子群組
    /// </summary>
    Task<bool> IsChildOfAsync(Guid childGroupId, Guid parentGroupId, CancellationToken cancellationToken = default);

    #endregion

    #region 成員管理

    /// <summary>
    /// 取得群組成員
    /// </summary>
    Task<IEnumerable<User>> GetMembersAsync(Guid groupId, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組有效成員
    /// </summary>
    Task<IEnumerable<User>> GetActiveMembersAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組成員數量
    /// </summary>
    Task<int> GetMemberCountAsync(Guid groupId, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查使用者是否為群組成員
    /// </summary>
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增群組成員
    /// </summary>
    Task<bool> AddMemberAsync(Guid groupId, Guid userId, DateTime? expiresAt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除群組成員
    /// </summary>
    Task<bool> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次新增成員
    /// </summary>
    Task<int> BatchAddMembersAsync(Guid groupId, IEnumerable<Guid> userIds, DateTime? expiresAt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次移除成員
    /// </summary>
    Task<int> BatchRemoveMembersAsync(Guid groupId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    #endregion

    #region 角色映射

    /// <summary>
    /// 取得群組的角色映射
    /// </summary>
    Task<IEnumerable<GroupRoleMapping>> GetRoleMappingsAsync(Guid groupId, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得群組的有效角色
    /// </summary>
    Task<IEnumerable<Role>> GetEffectiveRolesAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增群組角色映射
    /// </summary>
    Task<GroupRoleMapping> AddRoleMappingAsync(Guid groupId, Guid roleId, bool inheritToChildGroups = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除群組角色映射
    /// </summary>
    Task<bool> RemoveRoleMappingAsync(Guid groupId, Guid roleId, CancellationToken cancellationToken = default);

    #endregion

    #region 統計與計數

    /// <summary>
    /// 取得群組總數
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶的群組數量
    /// </summary>
    Task<int> GetCountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得 LDAP 群組數量
    /// </summary>
    Task<int> GetLdapGroupCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得系統群組數量
    /// </summary>
    Task<int> GetSystemGroupCountAsync(CancellationToken cancellationToken = default);

    #endregion

    #region LDAP 同步相關

    /// <summary>
    /// 更新 LDAP 同步資訊
    /// </summary>
    Task UpdateLdapSyncInfoAsync(Guid groupId, string syncHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次更新 LDAP 同步資訊
    /// </summary>
    Task BatchUpdateLdapSyncInfoAsync(IEnumerable<(Guid GroupId, string SyncHash)> updates, CancellationToken cancellationToken = default);

    /// <summary>
    /// 停用不存在於 LDAP 的群組
    /// </summary>
    Task DeactivateOrphanedLdapGroupsAsync(IEnumerable<string> activeLdapObjectGuids, CancellationToken cancellationToken = default);

    #endregion

    #region 批次操作

    /// <summary>
    /// 批次建立群組
    /// </summary>
    Task<IEnumerable<Group>> BatchCreateAsync(IEnumerable<Group> groups, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次更新群組
    /// </summary>
    Task<IEnumerable<Group>> BatchUpdateAsync(IEnumerable<Group> groups, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次刪除群組
    /// </summary>
    Task<int> BatchDeleteAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken = default);

    #endregion

    #region 驗證方法

    /// <summary>
    /// 檢查群組名稱是否存在
    /// </summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查群組名稱是否存在 (排除指定群組)
    /// </summary>
    Task<bool> ExistsAsync(string name, Guid excludeGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證群組階層不會產生循環參考
    /// </summary>
    Task<bool> ValidateHierarchyAsync(Guid groupId, Guid? newParentId, CancellationToken cancellationToken = default);

    #endregion
}