using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalIdentityServer.Services.KeyVault;

/// <summary>
/// Azure Key Vault 服務實作 - 遵循依賴反轉原則 (DIP)
/// </summary>
public class AzureKeyVaultService : IExternalKeyVault
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<AzureKeyVaultService> _logger;
    private readonly AzureKeyVaultOptions _options;

    public string ProviderName => "Azure Key Vault";

    public AzureKeyVaultService(
        ILogger<AzureKeyVaultService> logger,
        IOptions<AzureKeyVaultOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.VaultUri))
        {
            throw new ArgumentException("Azure Key Vault URI is required", nameof(options));
        }

        // 使用預設認證鏈 (Managed Identity, Azure CLI, 環境變數等)
        var credential = new DefaultAzureCredential();
        _secretClient = new SecretClient(new Uri(_options.VaultUri), credential);

        _logger.LogInformation("Azure Key Vault service initialized: {VaultUri}", _options.VaultUri);
    }

    public async Task<bool> IsConnectedAsync()
    {
        try
        {
            _logger.LogDebug("Testing Azure Key Vault connection");

            // 嘗試列出 secrets 來測試連線
            await foreach (var _ in _secretClient.GetPropertiesOfSecretsAsync().Take(1))
            {
                break;
            }

            _logger.LogDebug("Azure Key Vault connection successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Key Vault connection failed");
            return false;
        }
    }

    public async Task<bool> StoreKeyAsync(string keyId, string keyData, KeyMetadata metadata)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));
            }

            if (string.IsNullOrWhiteSpace(keyData))
            {
                throw new ArgumentException("Key data cannot be null or empty", nameof(keyData));
            }

            _logger.LogInformation("Storing key in Azure Key Vault: {KeyId}", keyId);

            var secretName = GetSecretName(keyId);
            var keyVaultSecret = new KeyVaultSecret(secretName, keyData);

            // 設定 metadata 作為 tags
            keyVaultSecret.Properties.Tags.Add("Algorithm", metadata.Algorithm);
            keyVaultSecret.Properties.Tags.Add("Use", metadata.Use);
            keyVaultSecret.Properties.Tags.Add("KeyType", metadata.KeyType);
            keyVaultSecret.Properties.Tags.Add("KeySize", metadata.KeySize.ToString());
            keyVaultSecret.Properties.Tags.Add("CreatedAt", metadata.CreatedAt.ToString("O"));
            keyVaultSecret.Properties.Tags.Add("IsPrimary", metadata.IsPrimary.ToString());

            if (metadata.ExpiresAt.HasValue)
            {
                keyVaultSecret.Properties.ExpiresOn = metadata.ExpiresAt.Value;
                keyVaultSecret.Properties.Tags.Add("ExpiresAt", metadata.ExpiresAt.Value.ToString("O"));
            }

            if (!string.IsNullOrEmpty(metadata.Description))
            {
                keyVaultSecret.Properties.Tags.Add("Description", metadata.Description);
            }

            // 添加自訂標籤
            foreach (var tag in metadata.Tags)
            {
                keyVaultSecret.Properties.Tags.Add($"Custom_{tag.Key}", tag.Value);
            }

            var response = await _secretClient.SetSecretAsync(keyVaultSecret);

            _logger.LogInformation("Key stored successfully in Azure Key Vault: {KeyId}", keyId);
            return response?.Value != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing key in Azure Key Vault: {KeyId}", keyId);
            return false;
        }
    }

    public async Task<string?> RetrieveKeyAsync(string keyId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));
            }

            _logger.LogDebug("Retrieving key from Azure Key Vault: {KeyId}", keyId);

            var secretName = GetSecretName(keyId);
            var response = await _secretClient.GetSecretAsync(secretName);

            if (response?.Value?.Value != null)
            {
                _logger.LogDebug("Key retrieved successfully from Azure Key Vault: {KeyId}", keyId);
                return response.Value.Value;
            }

            _logger.LogWarning("Key not found in Azure Key Vault: {KeyId}", keyId);
            return null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Key not found in Azure Key Vault: {KeyId}", keyId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving key from Azure Key Vault: {KeyId}", keyId);
            throw;
        }
    }

    public async Task<bool> DeleteKeyAsync(string keyId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));
            }

            _logger.LogInformation("Deleting key from Azure Key Vault: {KeyId}", keyId);

            var secretName = GetSecretName(keyId);
            var deleteOperation = await _secretClient.StartDeleteSecretAsync(secretName);

            // 等待刪除完成
            await deleteOperation.WaitForCompletionAsync();

            _logger.LogInformation("Key deleted successfully from Azure Key Vault: {KeyId}", keyId);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Key not found for deletion in Azure Key Vault: {KeyId}", keyId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting key from Azure Key Vault: {KeyId}", keyId);
            return false;
        }
    }

    public async Task<IEnumerable<string>> ListKeysAsync()
    {
        try
        {
            _logger.LogDebug("Listing keys from Azure Key Vault");

            var keyIds = new List<string>();
            var prefix = $"{_options.KeyPrefix}-";

            await foreach (var secretProperties in _secretClient.GetPropertiesOfSecretsAsync())
            {
                if (secretProperties.Name.StartsWith(prefix))
                {
                    var keyId = secretProperties.Name[prefix.Length..];
                    keyIds.Add(keyId);
                }
            }

            _logger.LogDebug("Listed {Count} keys from Azure Key Vault", keyIds.Count);
            return keyIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing keys from Azure Key Vault");
            throw;
        }
    }

    public async Task<KeyMetadata?> GetKeyMetadataAsync(string keyId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));
            }

            _logger.LogDebug("Getting key metadata from Azure Key Vault: {KeyId}", keyId);

            var secretName = GetSecretName(keyId);
            var response = await _secretClient.GetSecretAsync(secretName);

            if (response?.Value?.Properties == null)
            {
                _logger.LogWarning("Key metadata not found in Azure Key Vault: {KeyId}", keyId);
                return null;
            }

            var properties = response.Value.Properties;
            var tags = properties.Tags;

            var metadata = new KeyMetadata
            {
                Algorithm = tags.GetValueOrDefault("Algorithm", "RS256"),
                Use = tags.GetValueOrDefault("Use", "sig"),
                KeyType = tags.GetValueOrDefault("KeyType", "RSA"),
                KeySize = int.TryParse(tags.GetValueOrDefault("KeySize"), out var size) ? size : 2048,
                CreatedAt = DateTime.TryParse(tags.GetValueOrDefault("CreatedAt"), out var createdAt) ? createdAt : DateTime.UtcNow,
                ExpiresAt = properties.ExpiresOn?.DateTime,
                IsPrimary = bool.TryParse(tags.GetValueOrDefault("IsPrimary"), out var isPrimary) && isPrimary,
                Description = tags.GetValueOrDefault("Description"),
                Tags = tags.Where(t => t.Key.StartsWith("Custom_"))
                           .ToDictionary(t => t.Key[7..], t => t.Value) // 移除 "Custom_" 前綴
            };

            _logger.LogDebug("Key metadata retrieved from Azure Key Vault: {KeyId}", keyId);
            return metadata;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Key metadata not found in Azure Key Vault: {KeyId}", keyId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting key metadata from Azure Key Vault: {KeyId}", keyId);
            throw;
        }
    }

    public async Task<bool> UpdateKeyMetadataAsync(string keyId, KeyMetadata metadata)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));
            }

            _logger.LogDebug("Updating key metadata in Azure Key Vault: {KeyId}", keyId);

            // 先取得現有的 secret 值
            var secretName = GetSecretName(keyId);
            var existingSecret = await _secretClient.GetSecretAsync(secretName);

            if (existingSecret?.Value == null)
            {
                _logger.LogWarning("Key not found for metadata update in Azure Key Vault: {KeyId}", keyId);
                return false;
            }

            // 重新儲存 secret 以更新 metadata
            var success = await StoreKeyAsync(keyId, existingSecret.Value.Value, metadata);

            if (success)
            {
                _logger.LogDebug("Key metadata updated in Azure Key Vault: {KeyId}", keyId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating key metadata in Azure Key Vault: {KeyId}", keyId);
            return false;
        }
    }

    #region Private Helper Methods

    private string GetSecretName(string keyId)
    {
        // Azure Key Vault secret 名稱只能包含字母、數字和連字號
        var sanitizedKeyId = keyId.Replace("_", "-").Replace(" ", "-");
        return $"{_options.KeyPrefix}-{sanitizedKeyId}";
    }

    #endregion
}

/// <summary>
/// Azure Key Vault 配置選項
/// </summary>
public class AzureKeyVaultOptions
{
    public const string SectionName = "AzureKeyVault";

    /// <summary>
    /// Key Vault URI (例如: https://myvault.vault.azure.net/)
    /// </summary>
    public string VaultUri { get; set; } = default!;

    /// <summary>
    /// 金鑰前綴 (用於區分不同用途的金鑰)
    /// </summary>
    public string KeyPrefix { get; set; } = "identityserver-key";

    /// <summary>
    /// 是否啟用 Key Vault 整合
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 連線逾時時間 (秒)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}