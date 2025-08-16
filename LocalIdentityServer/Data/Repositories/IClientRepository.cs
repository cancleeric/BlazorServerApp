using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data.Repositories;

/// <summary>
/// 客戶端 Repository 介面 - 遵循介面隔離原則 (ISP)
/// </summary>
public interface IClientRepository
{
    /// <summary>
    /// 根據客戶端 ID 查詢客戶端
    /// </summary>
    Task<ClientEntity?> GetByClientIdAsync(string clientId);

    /// <summary>
    /// 新增客戶端
    /// </summary>
    Task<ClientEntity> AddAsync(ClientEntity client);

    /// <summary>
    /// 更新客戶端
    /// </summary>
    Task UpdateAsync(ClientEntity client);

    /// <summary>
    /// 檢查客戶端是否存在
    /// </summary>
    Task<bool> ExistsByClientIdAsync(string clientId);

    /// <summary>
    /// 取得活躍客戶端列表
    /// </summary>
    Task<IEnumerable<ClientEntity>> GetActiveClientsAsync();

    /// <summary>
    /// 驗證客戶端憑證
    /// </summary>
    Task<bool> ValidateClientCredentialsAsync(string clientId, string clientSecret);
}
