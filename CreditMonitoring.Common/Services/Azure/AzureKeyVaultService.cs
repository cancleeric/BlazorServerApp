using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

namespace CreditMonitoring.Common.Services.Azure
{
    /// <summary>
    /// Azure Key Vault 服務
    /// 用於安全地管理應用程式密鑰和連接字串
    /// </summary>
    public interface IAzureKeyVaultService
    {
        Task<string> GetSecretAsync(string secretName);
        Task SetSecretAsync(string secretName, string secretValue);
        Task<Dictionary<string, string>> GetMultipleSecretsAsync(params string[] secretNames);
    }

    public class AzureKeyVaultService : IAzureKeyVaultService
    {
        private readonly SecretClient _secretClient;
        private readonly ILogger<AzureKeyVaultService> _logger;

        public AzureKeyVaultService(
            IConfiguration configuration,
            ILogger<AzureKeyVaultService> logger)
        {
            _logger = logger;
            var keyVaultUri = configuration["Azure:KeyVault:VaultUri"]
                ?? throw new InvalidOperationException("Key Vault URI not configured");

            // 在 Azure 中使用 Managed Identity，本地開發使用 Azure CLI 認證
            var credential = new DefaultAzureCredential();
            _secretClient = new SecretClient(new Uri(keyVaultUri), credential);
        }

        /// <summary>
        /// 從 Key Vault 取得密鑰
        /// </summary>
        public async Task<string> GetSecretAsync(string secretName)
        {
            try
            {
                _logger.LogDebug("正在從 Key Vault 取得密鑰: {SecretName}", secretName);
                
                var response = await _secretClient.GetSecretAsync(secretName);
                
                _logger.LogDebug("成功取得密鑰: {SecretName}", secretName);
                return response.Value.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "從 Key Vault 取得密鑰時發生錯誤: {SecretName}", secretName);
                throw;
            }
        }

        /// <summary>
        /// 設定密鑰到 Key Vault
        /// </summary>
        public async Task SetSecretAsync(string secretName, string secretValue)
        {
            try
            {
                _logger.LogDebug("正在設定密鑰到 Key Vault: {SecretName}", secretName);
                
                await _secretClient.SetSecretAsync(secretName, secretValue);
                
                _logger.LogInformation("成功設定密鑰到 Key Vault: {SecretName}", secretName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "設定密鑰到 Key Vault 時發生錯誤: {SecretName}", secretName);
                throw;
            }
        }

        /// <summary>
        /// 批量取得多個密鑰
        /// </summary>
        public async Task<Dictionary<string, string>> GetMultipleSecretsAsync(params string[] secretNames)
        {
            var secrets = new Dictionary<string, string>();
            var tasks = secretNames.Select(async name =>
            {
                try
                {
                    var value = await GetSecretAsync(name);
                    return new KeyValuePair<string, string>(name, value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "無法取得密鑰: {SecretName}", name);
                    return new KeyValuePair<string, string>(name, string.Empty);
                }
            });

            var results = await Task.WhenAll(tasks);
            
            foreach (var result in results.Where(r => !string.IsNullOrEmpty(r.Value)))
            {
                secrets[result.Key] = result.Value;
            }

            return secrets;
        }
    }

    /// <summary>
    /// Key Vault 配置建構器
    /// 用於在應用程式啟動時從 Key Vault 載入配置
    /// </summary>
    public static class KeyVaultConfigurationExtensions
    {
        public static IConfigurationBuilder AddAzureKeyVault(
            this IConfigurationBuilder builder,
            string keyVaultUri,
            bool reloadOnChange = false)
        {
            if (string.IsNullOrEmpty(keyVaultUri))
                throw new ArgumentException("Key Vault URI cannot be null or empty", nameof(keyVaultUri));

            var credential = new DefaultAzureCredential();
            
            return builder.AddAzureKeyVault(
                new Uri(keyVaultUri),
                credential,
                new AzureKeyVaultConfigurationOptions
                {
                    ReloadInterval = reloadOnChange ? TimeSpan.FromMinutes(5) : null
                });
        }
    }
}
