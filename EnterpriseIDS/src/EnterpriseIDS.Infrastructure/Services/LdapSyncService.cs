using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.ValueObjects;

namespace EnterpriseIDS.Infrastructure.Services;

/// <summary>
/// LDAP 同步服務實作
/// </summary>
public class LdapSyncService : ILdapSyncService
{
    private readonly ILogger<LdapSyncService> _logger;
    private readonly ILdapConfigurationRepository _configurationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ILdapService _ldapService;
    private readonly ITenantContextService _tenantContextService;

    public LdapSyncService(
        ILogger<LdapSyncService> logger,
        ILdapConfigurationRepository configurationRepository,
        IUserRepository userRepository,
        IGroupRepository groupRepository,
        IRoleRepository roleRepository,
        ILdapService ldapService,
        ITenantContextService tenantContextService)
    {
        _logger = logger;
        _configurationRepository = configurationRepository;
        _userRepository = userRepository;
        _groupRepository = groupRepository;
        _roleRepository = roleRepository;
        _ldapService = ldapService;
        _tenantContextService = tenantContextService;
    }

    public async Task<IEnumerable<LdapConfiguration>> GetConfigurationsRequiringSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得需要同步的 LDAP 配置");
            return await _configurationRepository.GetConfigurationsRequiringSyncAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得需要同步的 LDAP 配置失敗");
            return Enumerable.Empty<LdapConfiguration>();
        }
    }

    public async Task ExecuteScheduledSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("開始執行排程 LDAP 同步");

            var configurations = await GetConfigurationsRequiringSyncAsync(cancellationToken);
            var syncTasks = new List<Task>();

            foreach (var config in configurations)
            {
                syncTasks.Add(ExecuteSyncForConfigurationAsync(config, cancellationToken));
            }

            await Task.WhenAll(syncTasks);

            _logger.LogInformation("完成排程 LDAP 同步，處理了 {Count} 個配置", configurations.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行排程 LDAP 同步失敗");
        }
    }

    public async Task<User> SyncUserToLocalAsync(LdapUserModel ldapUser, Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("同步 LDAP 使用者到本地: {Username}", ldapUser.Username);

            // 使用租戶上下文
            using var contextManager = _tenantContextService.CreateContextManager(tenantId);

            // 檢查使用者是否已存在
            var existingUser = await FindExistingUserAsync(ldapUser, cancellationToken);
            
            if (existingUser != null)
            {
                // 更新現有使用者
                return await UpdateExistingUserAsync(existingUser, ldapUser, cancellationToken);
            }
            else
            {
                // 建立新使用者
                return await CreateNewUserAsync(ldapUser, tenantId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步 LDAP 使用者失敗: {Username}", ldapUser.Username);
            throw;
        }
    }

    public async Task<Group> SyncGroupToLocalAsync(LdapGroupModel ldapGroup, Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("同步 LDAP 群組到本地: {GroupName}", ldapGroup.Name);

            // 使用租戶上下文
            using var contextManager = _tenantContextService.CreateContextManager(tenantId);

            // 檢查群組是否已存在
            var existingGroup = await FindExistingGroupAsync(ldapGroup, cancellationToken);
            
            if (existingGroup != null)
            {
                // 更新現有群組
                return await UpdateExistingGroupAsync(existingGroup, ldapGroup, cancellationToken);
            }
            else
            {
                // 建立新群組
                return await CreateNewGroupAsync(ldapGroup, tenantId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步 LDAP 群組失敗: {GroupName}", ldapGroup.Name);
            throw;
        }
    }

    public async Task SyncGroupMembershipsAsync(LdapGroupModel ldapGroup, Group localGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("同步群組成員資格: {GroupName}", ldapGroup.Name);

            // 取得 LDAP 群組的所有成員
            var ldapMembers = ldapGroup.Members;
            var processedMembers = 0;
            var addedMembers = 0;
            var removedMembers = 0;

            // 同步成員
            foreach (var memberDn in ldapMembers)
            {
                try
                {
                    // 根據 DN 查找本地使用者
                    var localUser = await _userRepository.GetByLdapDnAsync(memberDn, cancellationToken);
                    if (localUser != null)
                    {
                        // 檢查成員資格是否已存在
                        var isMember = await _groupRepository.IsMemberAsync(localGroup.Id, localUser.Id, cancellationToken);
                        if (!isMember)
                        {
                            await _groupRepository.AddMemberAsync(localGroup.Id, localUser.Id, null, cancellationToken);
                            addedMembers++;
                            _logger.LogDebug("新增群組成員: {Username} -> {GroupName}", localUser.Username, localGroup.Name);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("找不到對應的本地使用者: {MemberDn}", memberDn);
                    }
                    
                    processedMembers++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "處理群組成員失敗: {MemberDn}", memberDn);
                }
            }

            // 移除不再存在於 LDAP 的成員
            var currentMembers = await _groupRepository.GetActiveMembersAsync(localGroup.Id, cancellationToken);
            foreach (var currentMember in currentMembers.Where(u => u.IsFromLdap))
            {
                if (!string.IsNullOrEmpty(currentMember.LdapDistinguishedName) && 
                    !ldapMembers.Contains(currentMember.LdapDistinguishedName))
                {
                    await _groupRepository.RemoveMemberAsync(localGroup.Id, currentMember.Id, cancellationToken);
                    removedMembers++;
                    _logger.LogDebug("移除群組成員: {Username} <- {GroupName}", currentMember.Username, localGroup.Name);
                }
            }

            _logger.LogInformation("完成群組成員同步: {GroupName}, 處理 {Processed}, 新增 {Added}, 移除 {Removed}", 
                localGroup.Name, processedMembers, addedMembers, removedMembers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步群組成員資格失敗: {GroupName}", ldapGroup.Name);
            throw;
        }
    }

    public async Task ProcessRoleMappingsAsync(User user, List<string> ldapGroups, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("處理使用者角色映射: {Username}", user.Username);

            var processedMappings = 0;
            var addedRoles = 0;
            var removedRoles = 0;

            // 取得所有群組角色映射
            var allGroupMappings = new List<GroupRoleMapping>();
            foreach (var groupDn in ldapGroups)
            {
                var group = await _groupRepository.GetByLdapDnAsync(groupDn, cancellationToken);
                if (group != null)
                {
                    var mappings = await _groupRepository.GetRoleMappingsAsync(group.Id, false, cancellationToken);
                    allGroupMappings.AddRange(mappings);
                }
            }

            // 處理新的角色分配
            foreach (var mapping in allGroupMappings.Where(m => m.IsValidMapping()))
            {
                try
                {
                    // 檢查使用者是否已有此角色
                    var existingRoles = await _roleRepository.GetUserRolesAsync(user.Id, false, cancellationToken);
                    var hasRole = existingRoles.Any(r => r.Id == mapping.RoleId);

                    if (!hasRole)
                    {
                        await _roleRepository.AssignRoleToUserAsync(
                            mapping.RoleId, 
                            user.Id, 
                            "LDAP", 
                            mapping.ExpiresAt, 
                            cancellationToken);
                        
                        addedRoles++;
                        _logger.LogDebug("新增使用者角色: {Username} -> {RoleId}", user.Username, mapping.RoleId);
                    }
                    
                    processedMappings++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "處理角色映射失敗: {MappingId}", mapping.Id);
                }
            }

            // 移除不再有效的 LDAP 角色
            var currentLdapRoles = await _roleRepository.GetUserRolesAsync(user.Id, false, cancellationToken);
            var ldapRoleIds = allGroupMappings.Select(m => m.RoleId).ToHashSet();

            foreach (var role in currentLdapRoles)
            {
                var userRole = user.UserRoles.FirstOrDefault(ur => ur.RoleId == role.Id && ur.IsFromLdap());
                if (userRole != null && !ldapRoleIds.Contains(role.Id))
                {
                    await _roleRepository.RemoveRoleFromUserAsync(role.Id, user.Id, cancellationToken);
                    removedRoles++;
                    _logger.LogDebug("移除使用者角色: {Username} <- {RoleId}", user.Username, role.Id);
                }
            }

            _logger.LogInformation("完成使用者角色映射: {Username}, 處理 {Processed}, 新增 {Added}, 移除 {Removed}", 
                user.Username, processedMappings, addedRoles, removedRoles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理使用者角色映射失敗: {Username}", user.Username);
            throw;
        }
    }

    public async Task DeactivateOrphanedUsersAsync(Guid configurationId, IEnumerable<string> activeLdapUsers, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("停用孤立的 LDAP 使用者");

            var ldapUsers = await _userRepository.GetLdapUsersAsync(0, int.MaxValue, cancellationToken);
            var deactivatedCount = 0;

            foreach (var user in ldapUsers.Where(u => u.IsActive()))
            {
                if (!string.IsNullOrEmpty(user.LdapObjectGuid) && 
                    !activeLdapUsers.Contains(user.LdapObjectGuid))
                {
                    user.Status = UserStatus.Inactive;
                    await _userRepository.UpdateAsync(user, cancellationToken);
                    deactivatedCount++;
                    
                    _logger.LogDebug("停用孤立使用者: {Username}", user.Username);
                }
            }

            _logger.LogInformation("停用了 {Count} 個孤立的 LDAP 使用者", deactivatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停用孤立 LDAP 使用者失敗");
            throw;
        }
    }

    public async Task DeactivateOrphanedGroupsAsync(Guid configurationId, IEnumerable<string> activeLdapGroups, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("停用孤立的 LDAP 群組");

            var ldapGroups = await _groupRepository.GetLdapGroupsAsync(0, int.MaxValue, cancellationToken);
            var deactivatedCount = 0;

            foreach (var group in ldapGroups.Where(g => !g.IsDeleted))
            {
                if (!string.IsNullOrEmpty(group.LdapObjectGuid) && 
                    !activeLdapGroups.Contains(group.LdapObjectGuid))
                {
                    await _groupRepository.DeleteAsync(group.Id, cancellationToken);
                    deactivatedCount++;
                    
                    _logger.LogDebug("停用孤立群組: {GroupName}", group.Name);
                }
            }

            _logger.LogInformation("停用了 {Count} 個孤立的 LDAP 群組", deactivatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停用孤立 LDAP 群組失敗");
            throw;
        }
    }

    #region 私有輔助方法

    private async Task ExecuteSyncForConfigurationAsync(LdapConfiguration configuration, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("開始同步配置: {ConfigurationName}", configuration.Name);

            var syncOptions = new LdapSyncOptions
            {
                SyncUsers = true,
                SyncGroups = configuration.SyncGroups,
                SyncMemberships = true,
                SyncDisabledUsers = configuration.SyncDisabledUsers,
                SyncNestedGroups = configuration.SyncNestedGroups,
                ConflictResolution = configuration.ConflictResolution,
                BatchSize = 100
            };

            var result = await _ldapService.SynchronizeAsync(configuration.Id, syncOptions, cancellationToken);

            // 更新配置的同步狀態
            await _configurationRepository.UpdateSyncStatusAsync(
                configuration.Id,
                result.Success ? LdapSyncStatus.Success : LdapSyncStatus.Failed,
                result.ErrorMessage,
                SerializeSyncStatistics(result),
                cancellationToken);

            _logger.LogInformation("完成配置同步: {ConfigurationName}, 成功: {Success}", 
                configuration.Name, result.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步配置失敗: {ConfigurationName}", configuration.Name);
            
            await _configurationRepository.UpdateSyncStatusAsync(
                configuration.Id,
                LdapSyncStatus.Failed,
                ex.Message,
                null,
                cancellationToken);
        }
    }

    private async Task<User?> FindExistingUserAsync(LdapUserModel ldapUser, CancellationToken cancellationToken)
    {
        // 優先使用 LDAP Object GUID 查找
        if (!string.IsNullOrEmpty(ldapUser.ObjectGuid))
        {
            var userByGuid = await _userRepository.GetByLdapObjectGuidAsync(ldapUser.ObjectGuid, cancellationToken);
            if (userByGuid != null) return userByGuid;
        }

        // 使用 DN 查找
        if (!string.IsNullOrEmpty(ldapUser.DistinguishedName))
        {
            var userByDn = await _userRepository.GetByLdapDnAsync(ldapUser.DistinguishedName, cancellationToken);
            if (userByDn != null) return userByDn;
        }

        // 使用使用者名稱查找
        return await _userRepository.GetByUsernameAsync(ldapUser.Username, cancellationToken);
    }

    private async Task<User> UpdateExistingUserAsync(User existingUser, LdapUserModel ldapUser, CancellationToken cancellationToken)
    {
        // 更新使用者資訊
        existingUser.Email = ldapUser.Email;
        existingUser.FirstName = ldapUser.FirstName;
        existingUser.LastName = ldapUser.LastName;
        existingUser.DisplayName = ldapUser.DisplayName;
        existingUser.Department = ldapUser.Department;
        existingUser.JobTitle = ldapUser.JobTitle;
        existingUser.PhoneNumber = ldapUser.PhoneNumber;
        existingUser.MobileNumber = ldapUser.MobileNumber;
        existingUser.Office = ldapUser.Office;
        
        // 更新 LDAP 特定資訊
        existingUser.LdapDistinguishedName = ldapUser.DistinguishedName;
        existingUser.LdapObjectGuid = ldapUser.ObjectGuid;
        existingUser.LdapSecurityIdentifier = ldapUser.SecurityIdentifier;
        existingUser.IsFromLdap = true;
        
        // 更新狀態
        existingUser.Status = ldapUser.IsEnabled ? UserStatus.Active : UserStatus.Inactive;
        
        // 更新同步雜湊
        var syncHash = CalculateUserSyncHash(ldapUser);
        existingUser.UpdateLdapSync(syncHash);

        return await _userRepository.UpdateAsync(existingUser, cancellationToken);
    }

    private async Task<User> CreateNewUserAsync(LdapUserModel ldapUser, Guid tenantId, CancellationToken cancellationToken)
    {
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = ldapUser.Username,
            Email = ldapUser.Email,
            FirstName = ldapUser.FirstName,
            LastName = ldapUser.LastName,
            DisplayName = ldapUser.DisplayName,
            Department = ldapUser.Department,
            JobTitle = ldapUser.JobTitle,
            PhoneNumber = ldapUser.PhoneNumber,
            MobileNumber = ldapUser.MobileNumber,
            Office = ldapUser.Office,
            Status = ldapUser.IsEnabled ? UserStatus.Active : UserStatus.Inactive,
            
            // LDAP 特定資訊
            LdapDistinguishedName = ldapUser.DistinguishedName,
            LdapObjectGuid = ldapUser.ObjectGuid,
            LdapSecurityIdentifier = ldapUser.SecurityIdentifier,
            IsFromLdap = true,
            
            CreatedAt = DateTime.UtcNow
        };

        // 設定同步雜湊
        var syncHash = CalculateUserSyncHash(ldapUser);
        newUser.UpdateLdapSync(syncHash);

        return await _userRepository.CreateAsync(newUser, cancellationToken);
    }

    private async Task<Group?> FindExistingGroupAsync(LdapGroupModel ldapGroup, CancellationToken cancellationToken)
    {
        // 優先使用 LDAP Object GUID 查找
        if (!string.IsNullOrEmpty(ldapGroup.ObjectGuid))
        {
            var groupByGuid = await _groupRepository.GetByLdapObjectGuidAsync(ldapGroup.ObjectGuid, cancellationToken);
            if (groupByGuid != null) return groupByGuid;
        }

        // 使用 DN 查找
        if (!string.IsNullOrEmpty(ldapGroup.DistinguishedName))
        {
            var groupByDn = await _groupRepository.GetByLdapDnAsync(ldapGroup.DistinguishedName, cancellationToken);
            if (groupByDn != null) return groupByDn;
        }

        // 使用群組名稱查找
        return await _groupRepository.GetByNameAsync(ldapGroup.Name, cancellationToken);
    }

    private async Task<Group> UpdateExistingGroupAsync(Group existingGroup, LdapGroupModel ldapGroup, CancellationToken cancellationToken)
    {
        // 更新群組資訊
        existingGroup.DisplayName = ldapGroup.DisplayName;
        existingGroup.Description = ldapGroup.Description;
        
        // 更新 LDAP 特定資訊
        existingGroup.LdapDistinguishedName = ldapGroup.DistinguishedName;
        existingGroup.LdapObjectGuid = ldapGroup.ObjectGuid;
        existingGroup.LdapSecurityIdentifier = ldapGroup.SecurityIdentifier;
        existingGroup.IsFromLdap = true;
        
        // 更新同步雜湊
        var syncHash = CalculateGroupSyncHash(ldapGroup);
        existingGroup.UpdateLdapSync(syncHash);

        return await _groupRepository.UpdateAsync(existingGroup, cancellationToken);
    }

    private async Task<Group> CreateNewGroupAsync(LdapGroupModel ldapGroup, Guid tenantId, CancellationToken cancellationToken)
    {
        var newGroup = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = ldapGroup.Name,
            DisplayName = ldapGroup.DisplayName,
            Description = ldapGroup.Description,
            GroupType = GroupType.Security, // 預設為安全群組
            
            // LDAP 特定資訊
            LdapDistinguishedName = ldapGroup.DistinguishedName,
            LdapObjectGuid = ldapGroup.ObjectGuid,
            LdapSecurityIdentifier = ldapGroup.SecurityIdentifier,
            IsFromLdap = true,
            
            CreatedAt = DateTime.UtcNow
        };

        // 設定同步雜湊
        var syncHash = CalculateGroupSyncHash(ldapGroup);
        newGroup.UpdateLdapSync(syncHash);

        return await _groupRepository.CreateAsync(newGroup, cancellationToken);
    }

    private string CalculateUserSyncHash(LdapUserModel user)
    {
        var data = $"{user.Username}|{user.Email}|{user.FirstName}|{user.LastName}|{user.DisplayName}|{user.Department}|{user.JobTitle}|{user.IsEnabled}";
        return CalculateHash(data);
    }

    private string CalculateGroupSyncHash(LdapGroupModel group)
    {
        var data = $"{group.Name}|{group.DisplayName}|{group.Description}|{string.Join(",", group.Members)}";
        return CalculateHash(data);
    }

    private string CalculateHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes);
    }

    private string SerializeSyncStatistics(LdapSyncResult result)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            result.UsersProcessed,
            result.UsersAdded,
            result.UsersUpdated,
            result.UsersDeactivated,
            result.GroupsProcessed,
            result.GroupsAdded,
            result.GroupsUpdated,
            result.GroupsDeactivated,
            result.MembershipsProcessed,
            result.MembershipsAdded,
            result.MembershipsRemoved,
            result.Duration,
            result.StartTime,
            result.EndTime
        });
    }

    #endregion
}