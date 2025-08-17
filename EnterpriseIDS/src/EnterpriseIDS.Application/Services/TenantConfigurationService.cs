using Microsoft.Extensions.Logging;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;
using System.Text.Json;

namespace EnterpriseIDS.Application.Services;

/// <summary>
/// 租戶配置服務實作
/// </summary>
public class TenantConfigurationService : ITenantConfigurationService
{
    private readonly ITenantConfigurationRepository _configurationRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantContextService _tenantContextService;
    private readonly ILogger<TenantConfigurationService> _logger;

    public TenantConfigurationService(
        ITenantConfigurationRepository configurationRepository,
        ITenantRepository tenantRepository,
        ITenantContextService tenantContextService,
        ILogger<TenantConfigurationService> logger)
    {
        _configurationRepository = configurationRepository;
        _tenantRepository = tenantRepository;
        _tenantContextService = tenantContextService;
        _logger = logger;
    }

    /// <summary>
    /// 取得租戶配置
    /// </summary>
    public async Task<TenantConfiguration?> GetConfigurationAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得租戶配置: {TenantId}, {ConfigKey}", tenantId, configKey);
            return await _configurationRepository.GetByKeyAsync(tenantId, configKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得租戶配置時發生錯誤: {TenantId}, {ConfigKey}", tenantId, configKey);
            throw;
        }
    }

    /// <summary>
    /// 取得租戶配置值
    /// </summary>
    public async Task<T?> GetConfigurationValueAsync<T>(Guid tenantId, string configKey, T? defaultValue = default, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await GetEffectiveConfigurationAsync(tenantId, configKey, cancellationToken);
            if (config == null)
                return defaultValue;

            return config.GetTypedValue<T>() ?? defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得租戶配置值時發生錯誤: {TenantId}, {ConfigKey}", tenantId, configKey);
            return defaultValue;
        }
    }

    /// <summary>
    /// 取得租戶所有配置
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> GetAllConfigurationsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得租戶所有配置: {TenantId}", tenantId);
            return await _configurationRepository.GetAllForTenantAsync(tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得租戶所有配置時發生錯誤: {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// 取得分類配置
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> GetConfigurationsByCategoryAsync(Guid tenantId, string category, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得租戶分類配置: {TenantId}, {Category}", tenantId, category);
            return await _configurationRepository.GetByCategoryAsync(tenantId, category, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得租戶分類配置時發生錯誤: {TenantId}, {Category}", tenantId, category);
            throw;
        }
    }

    /// <summary>
    /// 設定租戶配置
    /// </summary>
    public async Task<TenantConfiguration> SetConfigurationAsync(Guid tenantId, string configKey, string? configValue, string configType = "string", CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("設定租戶配置: {TenantId}, {ConfigKey}", tenantId, configKey);

            // 檢查權限
            ValidateTenantAccess(tenantId);

            // 檢查配置是否存在
            var existingConfig = await _configurationRepository.GetByKeyAsync(tenantId, configKey, cancellationToken);

            if (existingConfig != null)
            {
                // 檢查是否為唯讀配置
                if (existingConfig.IsReadOnly)
                {
                    throw new InvalidOperationException($"配置 '{configKey}' 為唯讀，無法修改");
                }

                // 驗證配置值
                var (isValid, errorMessage) = await ValidateConfigurationAsync(tenantId, configKey, configValue, cancellationToken);
                if (!isValid)
                {
                    throw new ArgumentException($"配置值驗證失敗: {errorMessage}");
                }

                // 更新現有配置
                existingConfig.ConfigValue = configValue;
                existingConfig.ConfigType = configType;
                existingConfig.UpdatedAt = DateTime.UtcNow;
                existingConfig.Version++;

                return await _configurationRepository.UpdateAsync(existingConfig, cancellationToken);
            }
            else
            {
                // 建立新配置
                var newConfig = new TenantConfiguration
                {
                    TenantId = tenantId,
                    ConfigKey = configKey,
                    ConfigValue = configValue,
                    ConfigType = configType,
                    CreatedAt = DateTime.UtcNow
                };

                // 驗證配置值
                var (isValid, errorMessage) = newConfig.ValidateValue(configValue);
                if (!isValid)
                {
                    throw new ArgumentException($"配置值驗證失敗: {errorMessage}");
                }

                return await _configurationRepository.AddAsync(newConfig, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設定租戶配置時發生錯誤: {TenantId}, {ConfigKey}", tenantId, configKey);
            throw;
        }
    }

    /// <summary>
    /// 設定租戶配置值
    /// </summary>
    public async Task<TenantConfiguration> SetConfigurationValueAsync<T>(Guid tenantId, string configKey, T value, CancellationToken cancellationToken = default)
    {
        var configType = GetConfigTypeFromValue(value);
        var configValue = SerializeValue(value, configType);

        return await SetConfigurationAsync(tenantId, configKey, configValue, configType, cancellationToken);
    }

    /// <summary>
    /// 批次設定配置
    /// </summary>
    public async Task SetConfigurationsBatchAsync(Guid tenantId, Dictionary<string, object> configurations, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("批次設定租戶配置: {TenantId}, 配置數量: {Count}", tenantId, configurations.Count);

            // 檢查權限
            ValidateTenantAccess(tenantId);

            var configsToUpdate = new List<TenantConfiguration>();
            var configsToAdd = new List<TenantConfiguration>();

            foreach (var kvp in configurations)
            {
                var configKey = kvp.Key;
                var value = kvp.Value;
                var configType = GetConfigTypeFromValue(value);
                var configValue = SerializeValue(value, configType);

                var existingConfig = await _configurationRepository.GetByKeyAsync(tenantId, configKey, cancellationToken);

                if (existingConfig != null)
                {
                    if (existingConfig.IsReadOnly)
                    {
                        _logger.LogWarning("跳過唯讀配置: {ConfigKey}", configKey);
                        continue;
                    }

                    existingConfig.ConfigValue = configValue;
                    existingConfig.ConfigType = configType;
                    existingConfig.UpdatedAt = DateTime.UtcNow;
                    existingConfig.Version++;
                    configsToUpdate.Add(existingConfig);
                }
                else
                {
                    var newConfig = new TenantConfiguration
                    {
                        TenantId = tenantId,
                        ConfigKey = configKey,
                        ConfigValue = configValue,
                        ConfigType = configType,
                        CreatedAt = DateTime.UtcNow
                    };
                    configsToAdd.Add(newConfig);
                }
            }

            // 執行批次更新和新增
            if (configsToUpdate.Any())
            {
                await _configurationRepository.BatchUpdateAsync(configsToUpdate, cancellationToken);
            }

            if (configsToAdd.Any())
            {
                await _configurationRepository.AddRangeAsync(configsToAdd, cancellationToken);
            }

            _logger.LogInformation("批次設定租戶配置完成: {TenantId}, 更新: {UpdateCount}, 新增: {AddCount}",
                tenantId, configsToUpdate.Count, configsToAdd.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批次設定租戶配置時發生錯誤: {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// 刪除配置
    /// </summary>
    public async Task DeleteConfigurationAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("刪除租戶配置: {TenantId}, {ConfigKey}", tenantId, configKey);

            // 檢查權限
            ValidateTenantAccess(tenantId);

            var config = await _configurationRepository.GetByKeyAsync(tenantId, configKey, cancellationToken);
            if (config != null)
            {
                if (config.IsRequired)
                {
                    throw new InvalidOperationException($"配置 '{configKey}' 為必需項目，無法刪除");
                }

                if (config.IsReadOnly)
                {
                    throw new InvalidOperationException($"配置 '{configKey}' 為唯讀，無法刪除");
                }

                await _configurationRepository.DeleteByKeyAsync(tenantId, configKey, cancellationToken);
                _logger.LogInformation("租戶配置刪除成功: {TenantId}, {ConfigKey}", tenantId, configKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除租戶配置時發生錯誤: {TenantId}, {ConfigKey}", tenantId, configKey);
            throw;
        }
    }

    /// <summary>
    /// 複製配置到其他租戶
    /// </summary>
    public async Task CopyConfigurationsAsync(Guid sourceTenantId, Guid targetTenantId, string[]? configKeys = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("複製租戶配置: {SourceTenantId} -> {TargetTenantId}", sourceTenantId, targetTenantId);

            // 檢查權限
            ValidateTenantAccess(sourceTenantId);
            ValidateTenantAccess(targetTenantId);

            await _configurationRepository.CopyConfigurationsAsync(sourceTenantId, targetTenantId, configKeys, cancellationToken);

            _logger.LogInformation("租戶配置複製完成: {SourceTenantId} -> {TargetTenantId}", sourceTenantId, targetTenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "複製租戶配置時發生錯誤: {SourceTenantId} -> {TargetTenantId}", sourceTenantId, targetTenantId);
            throw;
        }
    }

    /// <summary>
    /// 重設配置為預設值
    /// </summary>
    public async Task ResetConfigurationAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("重設租戶配置為預設值: {TenantId}, {ConfigKey}", tenantId, configKey);

            // 檢查權限
            ValidateTenantAccess(tenantId);

            var config = await _configurationRepository.GetByKeyAsync(tenantId, configKey, cancellationToken);
            if (config != null)
            {
                if (config.IsReadOnly)
                {
                    throw new InvalidOperationException($"配置 '{configKey}' 為唯讀，無法重設");
                }

                config.ConfigValue = config.DefaultValue;
                config.UpdatedAt = DateTime.UtcNow;
                config.Version++;

                await _configurationRepository.UpdateAsync(config, cancellationToken);
                _logger.LogInformation("租戶配置重設成功: {TenantId}, {ConfigKey}", tenantId, configKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重設租戶配置時發生錯誤: {TenantId}, {ConfigKey}", tenantId, configKey);
            throw;
        }
    }

    /// <summary>
    /// 驗證配置值
    /// </summary>
    public async Task<(bool IsValid, string? ErrorMessage)> ValidateConfigurationAsync(Guid tenantId, string configKey, string? configValue, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _configurationRepository.GetByKeyAsync(tenantId, configKey, cancellationToken);
            if (config == null)
            {
                return (true, null); // 新配置，基本驗證通過
            }

            return config.ValidateValue(configValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證配置值時發生錯誤: {TenantId}, {ConfigKey}", tenantId, configKey);
            return (false, "驗證過程發生錯誤");
        }
    }

    /// <summary>
    /// 取得配置歷史
    /// </summary>
    public async Task<IEnumerable<TenantConfiguration>> GetConfigurationHistoryAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("取得配置歷史: {TenantId}, {ConfigKey}", tenantId, configKey);
            return await _configurationRepository.GetHistoryAsync(tenantId, configKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得配置歷史時發生錯誤: {TenantId}, {ConfigKey}", tenantId, configKey);
            throw;
        }
    }

    /// <summary>
    /// 檢查配置是否存在
    /// </summary>
    public async Task<bool> ConfigurationExistsAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _configurationRepository.ExistsAsync(tenantId, configKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查配置是否存在時發生錯誤: {TenantId}, {ConfigKey}", tenantId, configKey);
            return false;
        }
    }

    /// <summary>
    /// 取得有效配置（考慮繼承）
    /// </summary>
    public async Task<TenantConfiguration?> GetEffectiveConfigurationAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default)
    {
        try
        {
            // 先檢查租戶自己的配置
            var config = await _configurationRepository.GetByKeyAsync(tenantId, configKey, cancellationToken);
            if (config != null && config.IsValid())
            {
                return config;
            }

            // 如果沒有找到或無效，檢查父租戶的可繼承配置
            var tenantHierarchy = await _tenantRepository.GetTenantHierarchyAsync(tenantId, cancellationToken);
            var parentTenants = tenantHierarchy.Skip(1); // 跳過自己

            foreach (var parentTenant in parentTenants)
            {
                var parentConfig = await _configurationRepository.GetByKeyAsync(parentTenant.Id, configKey, cancellationToken);
                if (parentConfig != null && parentConfig.IsValid() && parentConfig.IsInheritable)
                {
                    return parentConfig;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得有效配置時發生錯誤: {TenantId}, {ConfigKey}", tenantId, configKey);
            throw;
        }
    }

    /// <summary>
    /// 初始化租戶預設配置
    /// </summary>
    public async Task InitializeDefaultConfigurationsAsync(Guid tenantId, TenantType tenantType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("初始化租戶預設配置: {TenantId}, {TenantType}", tenantId, tenantType);

            // 檢查權限
            ValidateTenantAccess(tenantId);

            var defaultConfigs = GetDefaultConfigurationsForTenantType(tenantType, tenantId);

            if (defaultConfigs.Any())
            {
                await _configurationRepository.AddRangeAsync(defaultConfigs, cancellationToken);
                _logger.LogInformation("租戶預設配置初始化完成: {TenantId}, 配置數量: {Count}", tenantId, defaultConfigs.Count());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化租戶預設配置時發生錯誤: {TenantId}, {TenantType}", tenantId, tenantType);
            throw;
        }
    }

    /// <summary>
    /// 根據值類型決定配置類型
    /// </summary>
    private static string GetConfigTypeFromValue<T>(T value)
    {
        return value switch
        {
            int => "int",
            long => "long",
            double => "double",
            decimal => "decimal",
            bool => "bool",
            DateTime => "datetime",
            string => "string",
            _ => "json"
        };
    }

    /// <summary>
    /// 序列化值
    /// </summary>
    private static string? SerializeValue<T>(T value, string configType)
    {
        if (value == null)
            return null;

        return configType switch
        {
            "json" => JsonSerializer.Serialize(value),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// 驗證租戶存取權限
    /// </summary>
    private void ValidateTenantAccess(Guid tenantId)
    {
        if (!_tenantContextService.IsSuperAdminContext() && !_tenantContextService.HasTenantAccess(tenantId))
        {
            throw new UnauthorizedAccessException("沒有權限存取此租戶的配置");
        }
    }

    /// <summary>
    /// 取得租戶類型的預設配置
    /// </summary>
    private static IEnumerable<TenantConfiguration> GetDefaultConfigurationsForTenantType(TenantType tenantType, Guid tenantId)
    {
        var configs = new List<TenantConfiguration>();
        var now = DateTime.UtcNow;

        // 基本安全配置
        configs.AddRange(new[]
        {
            new TenantConfiguration
            {
                TenantId = tenantId,
                ConfigKey = "security.password_min_length",
                ConfigValue = "8",
                ConfigType = "int",
                Category = "security",
                Description = "密碼最小長度",
                IsRequired = true,
                DefaultValue = "8",
                CreatedAt = now
            },
            new TenantConfiguration
            {
                TenantId = tenantId,
                ConfigKey = "security.require_mfa",
                ConfigValue = "false",
                ConfigType = "bool",
                Category = "security",
                Description = "是否強制多因子認證",
                DefaultValue = "false",
                CreatedAt = now
            },
            new TenantConfiguration
            {
                TenantId = tenantId,
                ConfigKey = "security.session_timeout_minutes",
                ConfigValue = "60",
                ConfigType = "int",
                Category = "security",
                Description = "會話逾時時間（分鐘）",
                DefaultValue = "60",
                CreatedAt = now
            }
        });

        // UI 配置
        configs.AddRange(new[]
        {
            new TenantConfiguration
            {
                TenantId = tenantId,
                ConfigKey = "ui.theme",
                ConfigValue = "default",
                ConfigType = "string",
                Category = "ui",
                Description = "UI 主題",
                AllowedValues = JsonSerializer.Serialize(new[] { "default", "dark", "light" }),
                DefaultValue = "default",
                CreatedAt = now
            },
            new TenantConfiguration
            {
                TenantId = tenantId,
                ConfigKey = "ui.logo_url",
                ConfigValue = "",
                ConfigType = "string",
                Category = "ui",
                Description = "Logo URL",
                CreatedAt = now
            }
        });

        // 根據租戶類型添加特定配置
        switch (tenantType)
        {
            case TenantType.Enterprise:
                configs.AddRange(GetEnterpriseConfigs(tenantId, now));
                break;
            case TenantType.Small:
                configs.AddRange(GetSmallBusinessConfigs(tenantId, now));
                break;
        }

        return configs;
    }

    /// <summary>
    /// 取得企業級配置
    /// </summary>
    private static IEnumerable<TenantConfiguration> GetEnterpriseConfigs(Guid tenantId, DateTime now)
    {
        return new[]
        {
            new TenantConfiguration
            {
                TenantId = tenantId,
                ConfigKey = "quota.max_users",
                ConfigValue = "1000",
                ConfigType = "int",
                Category = "quota",
                Description = "最大用戶數量",
                DefaultValue = "1000",
                CreatedAt = now
            },
            new TenantConfiguration
            {
                TenantId = tenantId,
                ConfigKey = "feature.advanced_audit",
                ConfigValue = "true",
                ConfigType = "bool",
                Category = "feature",
                Description = "啟用進階稽核功能",
                DefaultValue = "true",
                CreatedAt = now
            },
            new TenantConfiguration
            {
                TenantId = tenantId,
                ConfigKey = "security.require_mfa",
                ConfigValue = "true",
                ConfigType = "bool",
                Category = "security",
                Description = "強制多因子認證",
                DefaultValue = "true",
                CreatedAt = now
            }
        };
    }

    /// <summary>
    /// 取得小型企業配置
    /// </summary>
    private static IEnumerable<TenantConfiguration> GetSmallBusinessConfigs(Guid tenantId, DateTime now)
    {
        return new[]
        {
            new TenantConfiguration
            {
                TenantId = tenantId,
                ConfigKey = "quota.max_users",
                ConfigValue = "50",
                ConfigType = "int",
                Category = "quota",
                Description = "最大用戶數量",
                DefaultValue = "50",
                CreatedAt = now
            },
            new TenantConfiguration
            {
                TenantId = tenantId,
                ConfigKey = "feature.basic_features_only",
                ConfigValue = "true",
                ConfigType = "bool",
                Category = "feature",
                Description = "僅啟用基本功能",
                DefaultValue = "true",
                CreatedAt = now
            }
        };
    }
}