using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Core.Interfaces;

/// <summary>
/// 租戶配置服務介面
/// </summary>
public interface ITenantConfigurationService
{
    /// <summary>
    /// 取得租戶配置
    /// </summary>
    Task<TenantConfiguration?> GetConfigurationAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶配置值
    /// </summary>
    Task<T?> GetConfigurationValueAsync<T>(Guid tenantId, string configKey, T? defaultValue = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得租戶所有配置
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> GetAllConfigurationsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得分類配置
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> GetConfigurationsByCategoryAsync(Guid tenantId, string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定租戶配置
    /// </summary>
    Task<TenantConfiguration> SetConfigurationAsync(Guid tenantId, string configKey, string? configValue, string configType = "string", CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定租戶配置值
    /// </summary>
    Task<TenantConfiguration> SetConfigurationValueAsync<T>(Guid tenantId, string configKey, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次設定配置
    /// </summary>
    Task SetConfigurationsBatchAsync(Guid tenantId, Dictionary<string, object> configurations, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除配置
    /// </summary>
    Task DeleteConfigurationAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 複製配置到其他租戶
    /// </summary>
    Task CopyConfigurationsAsync(Guid sourceTenantId, Guid targetTenantId, string[]? configKeys = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重設配置為預設值
    /// </summary>
    Task ResetConfigurationAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證配置值
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidateConfigurationAsync(Guid tenantId, string configKey, string? configValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得配置歷史
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> GetConfigurationHistoryAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查配置是否存在
    /// </summary>
    Task<bool> ConfigurationExistsAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得有效配置（考慮繼承）
    /// </summary>
    Task<TenantConfiguration?> GetEffectiveConfigurationAsync(Guid tenantId, string configKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 初始化租戶預設配置
    /// </summary>
    Task InitializeDefaultConfigurationsAsync(Guid tenantId, TenantType tenantType, CancellationToken cancellationToken = default);
}

/// <summary>
/// 配置值結果
/// </summary>
/// <typeparam name="T">配置值類型</typeparam>
public class ConfigurationValue<T>
{
    /// <summary>
    /// 配置值
    /// </summary>
    public T? Value { get; set; }

    /// <summary>
    /// 是否為繼承值
    /// </summary>
    public bool IsInherited { get; set; }

    /// <summary>
    /// 來源租戶 ID（如果是繼承值）
    /// </summary>
    public Guid? SourceTenantId { get; set; }

    /// <summary>
    /// 配置鍵
    /// </summary>
    public string? ConfigKey { get; set; }

    /// <summary>
    /// 配置類型
    /// </summary>
    public string? ConfigType { get; set; }

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime? LastUpdated { get; set; }

    /// <summary>
    /// 是否為敏感配置
    /// </summary>
    public bool IsSensitive { get; set; }
}

/// <summary>
/// 租戶配置範本服務介面
/// </summary>
public interface ITenantConfigurationTemplateService
{
    /// <summary>
    /// 取得租戶類型的預設配置範本
    /// </summary>
    Task<IEnumerable<TenantConfiguration>> GetDefaultConfigurationsAsync(TenantType tenantType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立配置範本
    /// </summary>
    Task<TenantConfiguration> CreateConfigurationTemplateAsync(string configKey, string? defaultValue, string configType, string? category = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 應用配置範本到租戶
    /// </summary>
    Task ApplyConfigurationTemplateAsync(Guid tenantId, string templateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 匯出租戶配置為範本
    /// </summary>
    Task<string> ExportConfigurationTemplateAsync(Guid tenantId, string templateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 匯入配置範本
    /// </summary>
    Task ImportConfigurationTemplateAsync(string templateData, CancellationToken cancellationToken = default);
}