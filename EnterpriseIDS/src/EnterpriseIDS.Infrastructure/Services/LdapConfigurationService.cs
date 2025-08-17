using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Infrastructure.Services;

/// <summary>
/// LDAP 配置服務實作
/// </summary>
public class LdapConfigurationService : ILdapConfigurationService
{
    private readonly ILogger<LdapConfigurationService> _logger;
    private readonly ILdapConfigurationRepository _repository;
    private readonly IDataProtector _dataProtector;
    private readonly ITenantContextService _tenantContextService;

    public LdapConfigurationService(
        ILogger<LdapConfigurationService> logger,
        ILdapConfigurationRepository repository,
        IDataProtectionProvider dataProtectionProvider,
        ITenantContextService tenantContextService)
    {
        _logger = logger;
        _repository = repository;
        _dataProtector = dataProtectionProvider.CreateProtector("LdapConfiguration.SensitiveData");
        _tenantContextService = tenantContextService;
    }

    public async Task<LdapConfiguration?> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得租戶 LDAP 配置: {TenantId}", tenantId);

            var configuration = await _repository.GetByTenantAsync(tenantId, cancellationToken);
            if (configuration != null)
            {
                // 解密敏感資料
                configuration = await DecryptConfigurationAsync(configuration, cancellationToken);
            }

            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得租戶 LDAP 配置失敗: {TenantId}", tenantId);
            return null;
        }
    }

    public async Task<LdapConfiguration?> GetConfigurationByIdAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得 LDAP 配置: {ConfigurationId}", configurationId);

            var configuration = await _repository.GetByIdAsync(configurationId, cancellationToken);
            if (configuration != null)
            {
                // 解密敏感資料
                configuration = await DecryptConfigurationAsync(configuration, cancellationToken);
            }

            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得 LDAP 配置失敗: {ConfigurationId}", configurationId);
            return null;
        }
    }

    public async Task<LdapConfiguration> SaveConfigurationAsync(LdapConfiguration configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("儲存 LDAP 配置: {Name}", configuration.Name);

            // 驗證配置
            if (!ValidateConfiguration(configuration))
            {
                throw new ArgumentException("LDAP 配置驗證失敗");
            }

            // 確保設定租戶 ID
            var currentTenantId = _tenantContextService.GetCurrentTenantId();
            if (currentTenantId.HasValue && configuration.TenantId == Guid.Empty)
            {
                configuration.TenantId = currentTenantId.Value;
            }

            // 加密敏感資料
            var encryptedConfiguration = await EncryptConfigurationAsync(configuration, cancellationToken);

            LdapConfiguration savedConfiguration;
            if (encryptedConfiguration.Id == Guid.Empty)
            {
                // 新建配置
                encryptedConfiguration.Id = Guid.NewGuid();
                encryptedConfiguration.CreatedAt = DateTime.UtcNow;
                savedConfiguration = await _repository.CreateAsync(encryptedConfiguration, cancellationToken);
                _logger.LogInformation("建立新的 LDAP 配置: {Id} - {Name}", savedConfiguration.Id, savedConfiguration.Name);
            }
            else
            {
                // 更新現有配置
                encryptedConfiguration.UpdatedAt = DateTime.UtcNow;
                savedConfiguration = await _repository.UpdateAsync(encryptedConfiguration, cancellationToken);
                _logger.LogInformation("更新 LDAP 配置: {Id} - {Name}", savedConfiguration.Id, savedConfiguration.Name);
            }

            // 回傳解密後的配置
            return await DecryptConfigurationAsync(savedConfiguration, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "儲存 LDAP 配置失敗: {Name}", configuration.Name);
            throw;
        }
    }

    public async Task<bool> DeleteConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("刪除 LDAP 配置: {ConfigurationId}", configurationId);

            var result = await _repository.DeleteAsync(configurationId, cancellationToken);
            if (result)
            {
                _logger.LogInformation("成功刪除 LDAP 配置: {ConfigurationId}", configurationId);
            }
            else
            {
                _logger.LogWarning("LDAP 配置不存在或無法刪除: {ConfigurationId}", configurationId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除 LDAP 配置失敗: {ConfigurationId}", configurationId);
            return false;
        }
    }

    public async Task<IEnumerable<LdapConfiguration>> GetEnabledConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得所有啟用的 LDAP 配置");

            var configurations = await _repository.GetEnabledAsync(cancellationToken);
            var result = new List<LdapConfiguration>();

            foreach (var config in configurations)
            {
                var decryptedConfig = await DecryptConfigurationAsync(config, cancellationToken);
                result.Add(decryptedConfig);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得啟用的 LDAP 配置失敗");
            return Enumerable.Empty<LdapConfiguration>();
        }
    }

    public async Task<string> EncryptSensitiveDataAsync(string data, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            await Task.Delay(0, cancellationToken); // 保持異步介面一致性
            return _dataProtector.Protect(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加密敏感資料失敗");
            throw;
        }
    }

    public async Task<string> DecryptSensitiveDataAsync(string encryptedData, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(encryptedData))
                return string.Empty;

            await Task.Delay(0, cancellationToken); // 保持異步介面一致性
            return _dataProtector.Unprotect(encryptedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解密敏感資料失敗");
            throw;
        }
    }

    #region 私有輔助方法

    private async Task<LdapConfiguration> EncryptConfigurationAsync(LdapConfiguration configuration, CancellationToken cancellationToken)
    {
        var encrypted = new LdapConfiguration
        {
            Id = configuration.Id,
            TenantId = configuration.TenantId,
            Name = configuration.Name,
            Description = configuration.Description,
            ServerType = configuration.ServerType,
            ServerUrl = configuration.ServerUrl,
            Port = configuration.Port,
            UseSsl = configuration.UseSsl,
            UseStartTls = configuration.UseStartTls,
            IgnoreSslErrors = configuration.IgnoreSslErrors,
            BaseDn = configuration.BaseDn,
            UserBaseDn = configuration.UserBaseDn,
            GroupBaseDn = configuration.GroupBaseDn,
            UserSearchFilter = configuration.UserSearchFilter,
            GroupSearchFilter = configuration.GroupSearchFilter,
            UsernameAttribute = configuration.UsernameAttribute,
            EmailAttribute = configuration.EmailAttribute,
            FirstNameAttribute = configuration.FirstNameAttribute,
            LastNameAttribute = configuration.LastNameAttribute,
            DisplayNameAttribute = configuration.DisplayNameAttribute,
            GroupNameAttribute = configuration.GroupNameAttribute,
            GroupMemberAttribute = configuration.GroupMemberAttribute,
            AuthenticationType = configuration.AuthenticationType,
            ServiceAccountDn = configuration.ServiceAccountDn,
            ConnectionTimeout = configuration.ConnectionTimeout,
            SearchTimeout = configuration.SearchTimeout,
            MaxConnections = configuration.MaxConnections,
            IsEnabled = configuration.IsEnabled,
            SyncFrequencyMinutes = configuration.SyncFrequencyMinutes,
            EnableIncrementalSync = configuration.EnableIncrementalSync,
            ConflictResolution = configuration.ConflictResolution,
            SyncDisabledUsers = configuration.SyncDisabledUsers,
            SyncGroups = configuration.SyncGroups,
            SyncNestedGroups = configuration.SyncNestedGroups,
            MaxNestingLevel = configuration.MaxNestingLevel,
            AdditionalAttributeMapping = configuration.AdditionalAttributeMapping,
            LastSyncAt = configuration.LastSyncAt,
            LastSyncStatus = configuration.LastSyncStatus,
            LastSyncError = configuration.LastSyncError,
            SyncStatistics = configuration.SyncStatistics,
            CreatedAt = configuration.CreatedAt,
            UpdatedAt = configuration.UpdatedAt,
            IsDeleted = configuration.IsDeleted,
            DeletedAt = configuration.DeletedAt
        };

        // 加密敏感資料
        if (!string.IsNullOrEmpty(configuration.ServiceAccountPassword))
        {
            encrypted.ServiceAccountPassword = await EncryptSensitiveDataAsync(configuration.ServiceAccountPassword, cancellationToken);
        }

        return encrypted;
    }

    private async Task<LdapConfiguration> DecryptConfigurationAsync(LdapConfiguration configuration, CancellationToken cancellationToken)
    {
        var decrypted = new LdapConfiguration
        {
            Id = configuration.Id,
            TenantId = configuration.TenantId,
            Name = configuration.Name,
            Description = configuration.Description,
            ServerType = configuration.ServerType,
            ServerUrl = configuration.ServerUrl,
            Port = configuration.Port,
            UseSsl = configuration.UseSsl,
            UseStartTls = configuration.UseStartTls,
            IgnoreSslErrors = configuration.IgnoreSslErrors,
            BaseDn = configuration.BaseDn,
            UserBaseDn = configuration.UserBaseDn,
            GroupBaseDn = configuration.GroupBaseDn,
            UserSearchFilter = configuration.UserSearchFilter,
            GroupSearchFilter = configuration.GroupSearchFilter,
            UsernameAttribute = configuration.UsernameAttribute,
            EmailAttribute = configuration.EmailAttribute,
            FirstNameAttribute = configuration.FirstNameAttribute,
            LastNameAttribute = configuration.LastNameAttribute,
            DisplayNameAttribute = configuration.DisplayNameAttribute,
            GroupNameAttribute = configuration.GroupNameAttribute,
            GroupMemberAttribute = configuration.GroupMemberAttribute,
            AuthenticationType = configuration.AuthenticationType,
            ServiceAccountDn = configuration.ServiceAccountDn,
            ConnectionTimeout = configuration.ConnectionTimeout,
            SearchTimeout = configuration.SearchTimeout,
            MaxConnections = configuration.MaxConnections,
            IsEnabled = configuration.IsEnabled,
            SyncFrequencyMinutes = configuration.SyncFrequencyMinutes,
            EnableIncrementalSync = configuration.EnableIncrementalSync,
            ConflictResolution = configuration.ConflictResolution,
            SyncDisabledUsers = configuration.SyncDisabledUsers,
            SyncGroups = configuration.SyncGroups,
            SyncNestedGroups = configuration.SyncNestedGroups,
            MaxNestingLevel = configuration.MaxNestingLevel,
            AdditionalAttributeMapping = configuration.AdditionalAttributeMapping,
            LastSyncAt = configuration.LastSyncAt,
            LastSyncStatus = configuration.LastSyncStatus,
            LastSyncError = configuration.LastSyncError,
            SyncStatistics = configuration.SyncStatistics,
            CreatedAt = configuration.CreatedAt,
            UpdatedAt = configuration.UpdatedAt,
            IsDeleted = configuration.IsDeleted,
            DeletedAt = configuration.DeletedAt
        };

        // 解密敏感資料
        if (!string.IsNullOrEmpty(configuration.ServiceAccountPassword))
        {
            try
            {
                decrypted.ServiceAccountPassword = await DecryptSensitiveDataAsync(configuration.ServiceAccountPassword, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解密服務帳號密碼失敗: {ConfigurationId}", configuration.Id);
                decrypted.ServiceAccountPassword = string.Empty;
            }
        }

        return decrypted;
    }

    private bool ValidateConfiguration(LdapConfiguration configuration)
    {
        var errors = new List<string>();

        // 必要欄位驗證
        if (string.IsNullOrWhiteSpace(configuration.Name))
            errors.Add("配置名稱不能為空");

        if (string.IsNullOrWhiteSpace(configuration.ServerUrl))
            errors.Add("伺服器 URL 不能為空");

        if (string.IsNullOrWhiteSpace(configuration.BaseDn))
            errors.Add("基礎 DN 不能為空");

        if (configuration.Port <= 0 || configuration.Port > 65535)
            errors.Add("連接埠必須在 1-65535 範圍內");

        // 認證驗證
        if (configuration.RequiresAuthentication())
        {
            if (string.IsNullOrWhiteSpace(configuration.ServiceAccountDn))
                errors.Add("服務帳號 DN 不能為空");

            if (string.IsNullOrWhiteSpace(configuration.ServiceAccountPassword))
                errors.Add("服務帳號密碼不能為空");
        }

        // 逾時設定驗證
        if (configuration.ConnectionTimeout <= 0)
            errors.Add("連線逾時必須大於 0");

        if (configuration.SearchTimeout <= 0)
            errors.Add("搜尋逾時必須大於 0");

        // 同步設定驗證
        if (configuration.SyncFrequencyMinutes < 1)
            errors.Add("同步頻率至少 1 分鐘");

        if (configuration.MaxNestingLevel < 0)
            errors.Add("最大嵌套層級不能為負數");

        if (errors.Any())
        {
            var errorMessage = string.Join(", ", errors);
            _logger.LogWarning("LDAP 配置驗證失敗: {Errors}", errorMessage);
            return false;
        }

        return true;
    }

    #endregion
}