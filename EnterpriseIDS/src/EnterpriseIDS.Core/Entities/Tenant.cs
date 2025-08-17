using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EnterpriseIDS.Core.ValueObjects;

namespace EnterpriseIDS.Core.Entities;

/// <summary>
/// 租戶實體 - 表示一個組織或客戶
/// </summary>
public class Tenant : BaseEntityWithSoftDelete
{
    /// <summary>
    /// 租戶名稱
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 租戶標識符（用於 URL 和識別）
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// 租戶描述
    /// </summary>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// 租戶類型
    /// </summary>
    [Required]
    public TenantType TenantType { get; set; } = TenantType.Small;

    /// <summary>
    /// 租戶狀態
    /// </summary>
    [Required]
    public TenantStatus Status { get; set; } = TenantStatus.Pending;

    /// <summary>
    /// 訂閱計劃
    /// </summary>
    [Required]
    public SubscriptionPlan SubscriptionPlan { get; set; } = SubscriptionPlan.Free;

    /// <summary>
    /// 父租戶 ID（支援階層式租戶）
    /// </summary>
    public Guid? ParentTenantId { get; set; }

    /// <summary>
    /// 父租戶導航屬性
    /// </summary>
    public virtual Tenant? ParentTenant { get; set; }

    /// <summary>
    /// 子租戶集合
    /// </summary>
    public virtual ICollection<Tenant> ChildTenants { get; set; } = new List<Tenant>();

    /// <summary>
    /// 主要網域
    /// </summary>
    [StringLength(253)]
    public string? PrimaryDomain { get; set; }

    /// <summary>
    /// 允許的網域列表（JSON 序列化）
    /// </summary>
    public string? AllowedDomainsJson { get; set; }

    /// <summary>
    /// 允許的網域列表
    /// </summary>
    public List<string> AllowedDomains
    {
        get => string.IsNullOrEmpty(AllowedDomainsJson) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(AllowedDomainsJson) ?? new List<string>();
        set => AllowedDomainsJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// 聯絡人電子郵件
    /// </summary>
    [StringLength(320)]
    [EmailAddress]
    public string? ContactEmail { get; set; }

    /// <summary>
    /// 聯絡人電話
    /// </summary>
    [StringLength(20)]
    public string? ContactPhone { get; set; }

    /// <summary>
    /// 公司地址
    /// </summary>
    [StringLength(500)]
    public string? Address { get; set; }

    /// <summary>
    /// 時區
    /// </summary>
    [StringLength(50)]
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// 語言偏好
    /// </summary>
    [StringLength(10)]
    public string Language { get; set; } = "en-US";

    /// <summary>
    /// 租戶品牌設定（JSON 序列化）
    /// </summary>
    public string? BrandingJson { get; set; }

    /// <summary>
    /// 租戶品牌設定
    /// </summary>
    public TenantBranding Branding
    {
        get => string.IsNullOrEmpty(BrandingJson)
            ? new TenantBranding()
            : JsonSerializer.Deserialize<TenantBranding>(BrandingJson) ?? new TenantBranding();
        set => BrandingJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// 租戶配額設定（JSON 序列化）
    /// </summary>
    public string? QuotasJson { get; set; }

    /// <summary>
    /// 租戶配額設定
    /// </summary>
    public TenantQuotas Quotas
    {
        get => string.IsNullOrEmpty(QuotasJson)
            ? new TenantQuotas()
            : JsonSerializer.Deserialize<TenantQuotas>(QuotasJson) ?? new TenantQuotas();
        set => QuotasJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// 安全設定（JSON 序列化）
    /// </summary>
    public string? SecuritySettingsJson { get; set; }

    /// <summary>
    /// 安全設定
    /// </summary>
    public TenantSecuritySettings SecuritySettings
    {
        get => string.IsNullOrEmpty(SecuritySettingsJson)
            ? new TenantSecuritySettings()
            : JsonSerializer.Deserialize<TenantSecuritySettings>(SecuritySettingsJson) ?? new TenantSecuritySettings();
        set => SecuritySettingsJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// 啟用的功能列表（JSON 序列化）
    /// </summary>
    public string? EnabledFeaturesJson { get; set; }

    /// <summary>
    /// 啟用的功能列表
    /// </summary>
    public List<string> EnabledFeatures
    {
        get => string.IsNullOrEmpty(EnabledFeaturesJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(EnabledFeaturesJson) ?? new List<string>();
        set => EnabledFeaturesJson = JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// 訂閱開始日期
    /// </summary>
    public DateTime? SubscriptionStartDate { get; set; }

    /// <summary>
    /// 訂閱結束日期
    /// </summary>
    public DateTime? SubscriptionEndDate { get; set; }

    /// <summary>
    /// 試用結束日期
    /// </summary>
    public DateTime? TrialEndDate { get; set; }

    /// <summary>
    /// 租戶配置集合
    /// </summary>
    public virtual ICollection<TenantConfiguration> Configurations { get; set; } = new List<TenantConfiguration>();

    /// <summary>
    /// 中繼資料（JSON 序列化）
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// 中繼資料
    /// </summary>
    public Dictionary<string, object> Metadata
    {
        get => string.IsNullOrEmpty(MetadataJson)
            ? new Dictionary<string, object>()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson) ?? new Dictionary<string, object>();
        set => MetadataJson = JsonSerializer.Serialize(value);
    }

    // 業務邏輯方法

    /// <summary>
    /// 檢查租戶是否啟用
    /// </summary>
    public bool IsActive()
    {
        return Status == TenantStatus.Active && !IsDeleted;
    }

    /// <summary>
    /// 檢查租戶是否在試用期內
    /// </summary>
    public bool IsInTrial()
    {
        return TrialEndDate.HasValue && TrialEndDate.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// 檢查訂閱是否有效
    /// </summary>
    public bool HasValidSubscription()
    {
        if (SubscriptionPlan == SubscriptionPlan.Free)
            return true;

        return SubscriptionEndDate.HasValue && SubscriptionEndDate.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// 檢查是否支援指定功能
    /// </summary>
    public bool IsFeatureEnabled(string featureName)
    {
        return EnabledFeatures.Contains(featureName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 檢查網域是否被允許
    /// </summary>
    public bool IsDomainAllowed(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            return false;

        if (!string.IsNullOrEmpty(PrimaryDomain) && 
            string.Equals(PrimaryDomain, domain, StringComparison.OrdinalIgnoreCase))
            return true;

        return AllowedDomains.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 啟用租戶
    /// </summary>
    public void Activate()
    {
        Status = TenantStatus.Active;
    }

    /// <summary>
    /// 暫停租戶
    /// </summary>
    public void Suspend()
    {
        Status = TenantStatus.Suspended;
    }

    /// <summary>
    /// 停用租戶
    /// </summary>
    public void Disable()
    {
        Status = TenantStatus.Disabled;
    }

    /// <summary>
    /// 添加允許的網域
    /// </summary>
    public void AddAllowedDomain(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            return;

        var domains = AllowedDomains;
        if (!domains.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            domains.Add(domain.ToLowerInvariant());
            AllowedDomains = domains;
        }
    }

    /// <summary>
    /// 移除允許的網域
    /// </summary>
    public void RemoveAllowedDomain(string domain)
    {
        if (string.IsNullOrEmpty(domain))
            return;

        var domains = AllowedDomains;
        domains.RemoveAll(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase));
        AllowedDomains = domains;
    }

    /// <summary>
    /// 啟用功能
    /// </summary>
    public void EnableFeature(string featureName)
    {
        if (string.IsNullOrEmpty(featureName))
            return;

        var features = EnabledFeatures;
        if (!features.Contains(featureName, StringComparer.OrdinalIgnoreCase))
        {
            features.Add(featureName);
            EnabledFeatures = features;
        }
    }

    /// <summary>
    /// 停用功能
    /// </summary>
    public void DisableFeature(string featureName)
    {
        if (string.IsNullOrEmpty(featureName))
            return;

        var features = EnabledFeatures;
        features.RemoveAll(f => string.Equals(f, featureName, StringComparison.OrdinalIgnoreCase));
        EnabledFeatures = features;
    }
}