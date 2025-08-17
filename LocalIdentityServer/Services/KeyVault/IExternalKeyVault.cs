namespace LocalIdentityServer.Services.KeyVault;

/// <summary>
/// 外部金鑰保險庫抽象介面 - 遵循開放封閉原則 (OCP)
/// 支援 Azure Key Vault, HashiCorp Vault 等實作
/// </summary>
public interface IExternalKeyVault
{
    /// <summary>
    /// Key Vault 提供者名稱
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 檢查 Key Vault 連線狀態
    /// </summary>
    /// <returns>是否連線成功</returns>
    Task<bool> IsConnectedAsync();

    /// <summary>
    /// 儲存金鑰到外部保險庫
    /// </summary>
    /// <param name="keyId">金鑰識別碼</param>
    /// <param name="keyData">金鑰資料 (Base64 編碼)</param>
    /// <param name="metadata">金鑰元資料</param>
    /// <returns>是否儲存成功</returns>
    Task<bool> StoreKeyAsync(string keyId, string keyData, KeyMetadata metadata);

    /// <summary>
    /// 從外部保險庫取得金鑰
    /// </summary>
    /// <param name="keyId">金鑰識別碼</param>
    /// <returns>金鑰資料，如果不存在則為 null</returns>
    Task<string?> RetrieveKeyAsync(string keyId);

    /// <summary>
    /// 刪除外部保險庫中的金鑰
    /// </summary>
    /// <param name="keyId">金鑰識別碼</param>
    /// <returns>是否刪除成功</returns>
    Task<bool> DeleteKeyAsync(string keyId);

    /// <summary>
    /// 列出所有金鑰識別碼
    /// </summary>
    /// <returns>金鑰識別碼列表</returns>
    Task<IEnumerable<string>> ListKeysAsync();

    /// <summary>
    /// 取得金鑰元資料
    /// </summary>
    /// <param name="keyId">金鑰識別碼</param>
    /// <returns>金鑰元資料，如果不存在則為 null</returns>
    Task<KeyMetadata?> GetKeyMetadataAsync(string keyId);

    /// <summary>
    /// 更新金鑰元資料
    /// </summary>
    /// <param name="keyId">金鑰識別碼</param>
    /// <param name="metadata">新的元資料</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateKeyMetadataAsync(string keyId, KeyMetadata metadata);
}

/// <summary>
/// 金鑰元資料
/// </summary>
public record KeyMetadata
{
    /// <summary>
    /// 金鑰演算法
    /// </summary>
    public string Algorithm { get; init; } = default!;

    /// <summary>
    /// 金鑰用途 (sig=簽章, enc=加密)
    /// </summary>
    public string Use { get; init; } = "sig";

    /// <summary>
    /// 金鑰類型 (RSA, EC)
    /// </summary>
    public string KeyType { get; init; } = default!;

    /// <summary>
    /// 金鑰大小
    /// </summary>
    public int KeySize { get; init; }

    /// <summary>
    /// 創建時間
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 到期時間
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// 是否為主要金鑰
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// 標籤 (用於分類和搜尋)
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = new();

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; init; }
}