using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Infrastructure.Data.Configurations;

/// <summary>
/// Tenant 實體配置
/// </summary>
public class TenantEntityConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        // 表格名稱和約束
        builder.ToTable("Tenants", t =>
        {
            t.HasCheckConstraint("CK_Tenants_Slug_Format", 
                "[Slug] NOT LIKE '%[^a-z0-9-]%' AND [Slug] NOT LIKE '-%' AND [Slug] NOT LIKE '%-'");
            t.HasCheckConstraint("CK_Tenants_Email_Format",
                "[ContactEmail] IS NULL OR [ContactEmail] LIKE '%@%.%'");
        });

        // 主鍵
        builder.HasKey(t => t.Id);

        // 基本屬性
        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.TenantType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.SubscriptionPlan)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.PrimaryDomain)
            .HasMaxLength(253);

        builder.Property(t => t.AllowedDomainsJson)
            .HasColumnName("AllowedDomains")
            .HasColumnType("nvarchar(max)");

        builder.Property(t => t.ContactEmail)
            .HasMaxLength(320);

        builder.Property(t => t.ContactPhone)
            .HasMaxLength(20);

        builder.Property(t => t.Address)
            .HasMaxLength(500);

        builder.Property(t => t.TimeZone)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("UTC");

        builder.Property(t => t.Language)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("en-US");

        // JSON 屬性
        builder.Property(t => t.BrandingJson)
            .HasColumnName("Branding")
            .HasColumnType("nvarchar(max)");

        builder.Property(t => t.QuotasJson)
            .HasColumnName("Quotas")
            .HasColumnType("nvarchar(max)");

        builder.Property(t => t.SecuritySettingsJson)
            .HasColumnName("SecuritySettings")
            .HasColumnType("nvarchar(max)");

        builder.Property(t => t.EnabledFeaturesJson)
            .HasColumnName("EnabledFeatures")
            .HasColumnType("nvarchar(max)");

        builder.Property(t => t.MetadataJson)
            .HasColumnName("Metadata")
            .HasColumnType("nvarchar(max)");

        // 忽略計算屬性
        builder.Ignore(t => t.Metadata);
        builder.Ignore(t => t.Branding);
        builder.Ignore(t => t.Quotas);
        builder.Ignore(t => t.SecuritySettings);
        builder.Ignore(t => t.EnabledFeatures);
        builder.Ignore(t => t.AllowedDomains);

        // 日期屬性
        builder.Property(t => t.SubscriptionStartDate)
            .HasColumnType("datetime2");

        builder.Property(t => t.SubscriptionEndDate)
            .HasColumnType("datetime2");

        builder.Property(t => t.TrialEndDate)
            .HasColumnType("datetime2");

        // 繼承的屬性配置
        ConfigureBaseEntity(builder);

        // 自引用關係（階層式租戶）
        builder.HasOne(t => t.ParentTenant)
            .WithMany(t => t.ChildTenants)
            .HasForeignKey(t => t.ParentTenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // 配置關係
        builder.HasMany(t => t.Configurations)
            .WithOne(tc => tc.Tenant)
            .HasForeignKey(tc => tc.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // 索引
        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Slug")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(t => t.PrimaryDomain)
            .HasDatabaseName("IX_Tenants_PrimaryDomain")
            .HasFilter("[PrimaryDomain] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("IX_Tenants_Status");

        builder.HasIndex(t => t.TenantType)
            .HasDatabaseName("IX_Tenants_TenantType");

        builder.HasIndex(t => t.ParentTenantId)
            .HasDatabaseName("IX_Tenants_ParentTenantId")
            .HasFilter("[ParentTenantId] IS NOT NULL");

        builder.HasIndex(t => new { t.Status, t.IsDeleted })
            .HasDatabaseName("IX_Tenants_Status_IsDeleted");

        // 檢查約束 - 移到 ToTable 配置中

        // 預設值
        builder.Property(t => t.TenantType)
            .HasDefaultValue(TenantType.Small);

        builder.Property(t => t.Status)
            .HasDefaultValue(TenantStatus.Pending);

        builder.Property(t => t.SubscriptionPlan)
            .HasDefaultValue(SubscriptionPlan.Free);
    }

    /// <summary>
    /// 配置基礎實體屬性
    /// </summary>
    private static void ConfigureBaseEntity<T>(EntityTypeBuilder<T> builder) where T : BaseEntityWithSoftDelete
    {
        // 基礎實體屬性
        builder.Property(e => e.Id)
            .IsRequired()
            .HasDefaultValueSql("NEWID()");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(e => e.CreatedById)
            .HasMaxLength(450);

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("datetime2");

        builder.Property(e => e.UpdatedById)
            .HasMaxLength(450);

        builder.Property(e => e.RowVersion)
            .IsRowVersion();

        // 軟刪除屬性
        builder.Property(e => e.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnType("datetime2");

        builder.Property(e => e.DeletedById)
            .HasMaxLength(450);

        // 軟刪除索引
        builder.HasIndex(e => e.IsDeleted)
            .HasDatabaseName($"IX_{typeof(T).Name}_IsDeleted");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName($"IX_{typeof(T).Name}_CreatedAt");
    }
}

/// <summary>
/// TenantConfiguration 實體配置
/// </summary>
public class TenantConfigurationEntityConfiguration : IEntityTypeConfiguration<Core.Entities.TenantConfiguration>
{
    public void Configure(EntityTypeBuilder<Core.Entities.TenantConfiguration> builder)
    {
        // 表格名稱和約束
        builder.ToTable("TenantConfigurations", t =>
        {
            t.HasCheckConstraint("CK_TenantConfigurations_ConfigKey_Format",
                "[ConfigKey] NOT LIKE '% %' AND [ConfigKey] NOT LIKE '%[^a-zA-Z0-9._-]%'");
            t.HasCheckConstraint("CK_TenantConfigurations_ConfigType_Valid",
                "[ConfigType] IN ('string', 'int', 'long', 'double', 'decimal', 'bool', 'datetime', 'json')");
            t.HasCheckConstraint("CK_TenantConfigurations_Version_Positive",
                "[Version] > 0");
            t.HasCheckConstraint("CK_TenantConfigurations_SortOrder_NonNegative",
                "[SortOrder] >= 0");
            t.HasCheckConstraint("CK_TenantConfigurations_EffectiveDate_BeforeExpiry",
                "[EffectiveDate] IS NULL OR [ExpiryDate] IS NULL OR [EffectiveDate] < [ExpiryDate]");
        });

        // 主鍵
        builder.HasKey(tc => tc.Id);

        // 基本屬性
        builder.Property(tc => tc.TenantId)
            .IsRequired();

        builder.Property(tc => tc.ConfigKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(tc => tc.ConfigValue)
            .HasColumnType("nvarchar(max)");

        builder.Property(tc => tc.ConfigType)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("string");

        builder.Property(tc => tc.Category)
            .HasMaxLength(100);

        builder.Property(tc => tc.Description)
            .HasMaxLength(500);

        builder.Property(tc => tc.ValidationRules)
            .HasColumnType("nvarchar(max)");

        builder.Property(tc => tc.AllowedValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(tc => tc.DefaultValue)
            .HasColumnType("nvarchar(max)");

        builder.Property(tc => tc.TagsJson)
            .HasColumnName("Tags")
            .HasColumnType("nvarchar(max)");

        // 布林屬性
        builder.Property(tc => tc.IsSensitive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(tc => tc.IsInheritable)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(tc => tc.IsRequired)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(tc => tc.IsReadOnly)
            .IsRequired()
            .HasDefaultValue(false);

        // 數值屬性
        builder.Property(tc => tc.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(tc => tc.Version)
            .IsRequired()
            .HasDefaultValue(1);

        // 日期屬性
        builder.Property(tc => tc.EffectiveDate)
            .HasColumnType("datetime2");

        builder.Property(tc => tc.ExpiryDate)
            .HasColumnType("datetime2");

        // 繼承的屬性配置
        ConfigureBaseEntity(builder);

        // 外鍵關係
        builder.HasOne(tc => tc.Tenant)
            .WithMany(t => t.Configurations)
            .HasForeignKey(tc => tc.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // 索引
        builder.HasIndex(tc => new { tc.TenantId, tc.ConfigKey })
            .IsUnique()
            .HasDatabaseName("IX_TenantConfigurations_TenantId_ConfigKey");

        builder.HasIndex(tc => tc.ConfigKey)
            .HasDatabaseName("IX_TenantConfigurations_ConfigKey");

        builder.HasIndex(tc => tc.Category)
            .HasDatabaseName("IX_TenantConfigurations_Category")
            .HasFilter("[Category] IS NOT NULL");

        builder.HasIndex(tc => tc.ConfigType)
            .HasDatabaseName("IX_TenantConfigurations_ConfigType");

        builder.HasIndex(tc => new { tc.TenantId, tc.Category })
            .HasDatabaseName("IX_TenantConfigurations_TenantId_Category")
            .HasFilter("[Category] IS NOT NULL");

        builder.HasIndex(tc => new { tc.EffectiveDate, tc.ExpiryDate })
            .HasDatabaseName("IX_TenantConfigurations_EffectiveDate_ExpiryDate")
            .HasFilter("[EffectiveDate] IS NOT NULL OR [ExpiryDate] IS NOT NULL");

        builder.HasIndex(tc => tc.IsRequired)
            .HasDatabaseName("IX_TenantConfigurations_IsRequired")
            .HasFilter("[IsRequired] = 1");

        // 檢查約束 - 移到 ToTable 配置中
    }

    /// <summary>
    /// 配置基礎實體屬性
    /// </summary>
    private static void ConfigureBaseEntity<T>(EntityTypeBuilder<T> builder) where T : BaseEntity
    {
        builder.Property(e => e.Id)
            .IsRequired()
            .HasDefaultValueSql("NEWID()");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(e => e.CreatedById)
            .HasMaxLength(450);

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("datetime2");

        builder.Property(e => e.UpdatedById)
            .HasMaxLength(450);

        builder.Property(e => e.RowVersion)
            .IsRowVersion();

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName($"IX_{typeof(T).Name}_CreatedAt");
    }
}