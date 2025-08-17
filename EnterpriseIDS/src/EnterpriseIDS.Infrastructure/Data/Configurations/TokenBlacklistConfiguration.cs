using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Infrastructure.Data.Configurations;

/// <summary>
/// Token 黑名單實體配置
/// </summary>
public class TokenBlacklistConfiguration : IEntityTypeConfiguration<TokenBlacklist>
{
    public void Configure(EntityTypeBuilder<TokenBlacklist> builder)
    {

        // 主鍵
        builder.HasKey(x => x.Id);

        // 基本屬性
        builder.Property(x => x.JwtId)
            .IsRequired()
            .HasMaxLength(64)
            .HasComment("JWT ID (jti claim)");

        builder.Property(x => x.TokenHash)
            .IsRequired()
            .HasMaxLength(64)
            .HasComment("Token 雜湊值 (用於快速比對)");

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasComment("使用者 ID");

        builder.Property(x => x.TokenType)
            .IsRequired()
            .HasMaxLength(20)
            .HasComment("Token 類型");

        builder.Property(x => x.BlacklistedAt)
            .IsRequired()
            .HasComment("加入黑名單的時間");

        builder.Property(x => x.OriginalExpiresAt)
            .IsRequired()
            .HasComment("Token 原本的過期時間");

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(500)
            .HasComment("加入黑名單的原因");

        builder.Property(x => x.BlacklistedByUserId)
            .HasComment("加入黑名單的使用者 ID");

        builder.Property(x => x.Type)
            .IsRequired()
            .HasComment("黑名單類型");

        builder.Property(x => x.IsPermanent)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment("是否為永久黑名單");

        builder.Property(x => x.BlacklistExpiresAt)
            .HasComment("黑名單到期時間 (null 表示永久)");

        builder.Property(x => x.Metadata)
            .HasMaxLength(1000)
            .HasComment("額外的 Metadata (JSON 格式)");

        // 租戶相關屬性 (繼承自 TenantAwareEntity)
        builder.Property(x => x.TenantId)
            .IsRequired()
            .HasComment("租戶 ID");

        // 基礎實體屬性 (繼承自 BaseEntity)
        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasComment("建立時間");

        builder.Property(x => x.UpdatedAt)
            .HasComment("更新時間");

        builder.Property(x => x.DeletedAt)
            .HasComment("刪除時間");

        builder.Property(x => x.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment("是否已刪除");

        // 索引
        builder.HasIndex(x => x.JwtId)
            .IsUnique()
            .HasDatabaseName("IX_TokenBlacklists_JwtId");

        builder.HasIndex(x => x.TokenHash)
            .HasDatabaseName("IX_TokenBlacklists_TokenHash");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_TokenBlacklists_UserId");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_TokenBlacklists_TenantId");

        builder.HasIndex(x => x.BlacklistedAt)
            .HasDatabaseName("IX_TokenBlacklists_BlacklistedAt");

        builder.HasIndex(x => x.OriginalExpiresAt)
            .HasDatabaseName("IX_TokenBlacklists_OriginalExpiresAt");

        builder.HasIndex(x => x.BlacklistExpiresAt)
            .HasDatabaseName("IX_TokenBlacklists_BlacklistExpiresAt");

        builder.HasIndex(x => x.Type)
            .HasDatabaseName("IX_TokenBlacklists_Type");

        // 複合索引用於查詢效能
        builder.HasIndex(x => new { x.TenantId, x.JwtId })
            .HasDatabaseName("IX_TokenBlacklists_TenantId_JwtId");

        builder.HasIndex(x => new { x.TenantId, x.TokenHash })
            .HasDatabaseName("IX_TokenBlacklists_TenantId_TokenHash");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Type })
            .HasDatabaseName("IX_TokenBlacklists_TenantId_UserId_Type");

        builder.HasIndex(x => new { x.IsPermanent, x.BlacklistExpiresAt })
            .HasDatabaseName("IX_TokenBlacklists_IsPermanent_BlacklistExpiresAt");

        // 用於清理作業的索引
        builder.HasIndex(x => new { x.OriginalExpiresAt, x.BlacklistExpiresAt, x.IsPermanent })
            .HasDatabaseName("IX_TokenBlacklists_Cleanup");

        // 外鍵關係
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BlacklistedByUser)
            .WithMany()
            .HasForeignKey(x => x.BlacklistedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // 檢查約束
        builder.ToTable("TokenBlacklists", t =>
        {
            t.HasCheckConstraint("CK_TokenBlacklists_TokenType", 
                "[TokenType] IN ('access_token', 'refresh_token', 'id_token')");
            t.HasCheckConstraint("CK_TokenBlacklists_Type", 
                "[Type] IN (1, 2, 3, 4, 5, 6, 7, 8)");
            t.HasCheckConstraint("CK_TokenBlacklists_BlacklistedAt", 
                "[BlacklistedAt] <= [OriginalExpiresAt]");
            t.HasCheckConstraint("CK_TokenBlacklists_BlacklistExpiry", 
                "([IsPermanent] = 1 AND [BlacklistExpiresAt] IS NULL) OR ([IsPermanent] = 0)");
        });
    }
}