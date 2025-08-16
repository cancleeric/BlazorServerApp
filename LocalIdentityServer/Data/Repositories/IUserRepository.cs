using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// 使用者 Repository 介面 - 遵循介面隔離原則 (ISP)
/// 只定義與使用者相關的操作，避免大型介面
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// 根據 ID 查詢使用者
    /// </summary>
    Task<UserEntity?> GetByIdAsync(string id);

    /// <summary>
    /// 根據使用者名稱查詢使用者
    /// </summary>
    Task<UserEntity?> GetByUserNameAsync(string userName);

    /// <summary>
    /// 根據電子郵件查詢使用者
    /// </summary>
    Task<UserEntity?> GetByEmailAsync(string email);

    /// <summary>
    /// 新增使用者
    /// </summary>
    Task<UserEntity> AddAsync(UserEntity user);

    /// <summary>
    /// 更新使用者
    /// </summary>
    Task UpdateAsync(UserEntity user);

    /// <summary>
    /// 更新最後登入時間
    /// </summary>
    Task UpdateLastLoginAsync(string userId);

    /// <summary>
    /// 檢查使用者名稱是否存在
    /// </summary>
    Task<bool> ExistsByUserNameAsync(string userName);

    /// <summary>
    /// 檢查電子郵件是否存在
    /// </summary>
    Task<bool> ExistsByEmailAsync(string email);

    /// <summary>
    /// 取得活躍使用者列表
    /// </summary>
    Task<IEnumerable<UserEntity>> GetActiveUsersAsync();
}
