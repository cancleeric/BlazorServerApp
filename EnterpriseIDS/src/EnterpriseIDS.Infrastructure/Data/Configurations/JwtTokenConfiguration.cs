using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EnterpriseIDS.Core.Entities;

namespace EnterpriseIDS.Infrastructure.Data.Configurations;

/// <summary>
/// JWT Token 實體配置
/// </summary>
public class JwtTokenConfiguration : IEntityTypeConfiguration<JwtToken>
{
    public void Configure(EntityTypeBuilder<JwtToken> builder)
    {

        // 主鍵
        builder.HasKey(x => x.Id);

        // 基本屬性
        builder.Property(x => x.JwtId)
            .IsRequired()
            .HasMaxLength(64)
            .HasComment("JWT ID (jti claim)");

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasComment("使用者 ID");

        builder.Property(x => x.TokenType)
            .IsRequired()
            .HasMaxLength(20)
            .HasComment("Token 類型 (access_token, refresh_token, id_token)");

        builder.Property(x => x.TokenValue)
            .IsRequired()
            .HasMaxLength(4000)
            .HasComment("Token 值 (已加密或雜湊)");

        builder.Property(x => x.IssuedAt)
            .IsRequired()
            .HasComment("Token 發行時間");

        builder.Property(x => x.ExpiresAt)
            .IsRequired()
            .HasComment("Token 過期時間");

        builder.Property(x => x.Issuer)
            .HasMaxLength(200)
            .HasComment("發行者 (iss claim)");

        builder.Property(x => x.Audience)
            .HasMaxLength(500)
            .HasComment("接收者 (aud claim)");

        builder.Property(x => x.Subject)
            .HasMaxLength(200)
            .HasComment("主體 (sub claim)");

        builder.Property(x => x.ClientId)
            .HasMaxLength(100)
            .HasComment("客戶端 ID");

        builder.Property(x => x.Scopes)
            .HasMaxLength(1000)
            .HasComment("授權範圍 (space-separated)");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasComment("Token 狀態");

        builder.Property(x => x.RevokedAt)
            .HasComment("撤銷時間");

        builder.Property(x => x.RevokedReason)
            .HasMaxLength(500)
            .HasComment("撤銷原因");

        builder.Property(x => x.RevokedByUserId)
            .HasComment("撤銷者使用者 ID");

        builder.Property(x => x.LastUsedAt)
            .HasComment("最後使用時間");

        builder.Property(x => x.UseCount)
            .IsRequired()
            .HasDefaultValue(0)
            .HasComment("使用次數");

        builder.Property(x => x.SourceIpAddress)
            .HasMaxLength(45)
            .HasComment("來源 IP 地址");

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500)
            .HasComment("User Agent");

        builder.Property(x => x.RefreshTokenId)
            .HasComment("關聯的 Refresh Token ID");

        builder.Property(x => x.ParentTokenId)
            .HasComment("父級 Token ID");

        builder.Property(x => x.Metadata)
            .HasMaxLength(2000)
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
            .HasDatabaseName("IX_JwtTokens_JwtId");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_JwtTokens_UserId");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_JwtTokens_TenantId");

        builder.HasIndex(x => new { x.TokenType, x.Status })
            .HasDatabaseName("IX_JwtTokens_TokenType_Status");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("IX_JwtTokens_ExpiresAt");

        builder.HasIndex(x => x.IssuedAt)
            .HasDatabaseName("IX_JwtTokens_IssuedAt");

        builder.HasIndex(x => x.RefreshTokenId)
            .HasDatabaseName("IX_JwtTokens_RefreshTokenId");

        builder.HasIndex(x => x.ParentTokenId)
            .HasDatabaseName("IX_JwtTokens_ParentTokenId");

        // 複合索引用於查詢效能
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status })
            .HasDatabaseName("IX_JwtTokens_TenantId_UserId_Status");

        builder.HasIndex(x => new { x.TenantId, x.TokenType, x.ExpiresAt })
            .HasDatabaseName("IX_JwtTokens_TenantId_TokenType_ExpiresAt");

        // 外鍵關係
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RefreshToken)
            .WithMany()
            .HasForeignKey(x => x.RefreshTokenId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ParentToken)
            .WithMany(x => x.ChildTokens)
            .HasForeignKey(x => x.ParentTokenId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.RevokedByUser)
            .WithMany()
            .HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // 檢查約束
        builder.ToTable("JwtTokens", t =>
        {
            t.HasCheckConstraint("CK_JwtTokens_TokenType", 
                "[TokenType] IN ('access_token', 'refresh_token', 'id_token')");
            t.HasCheckConstraint("CK_JwtTokens_Status", 
                "[Status] IN (1, 2, 3, 4, 5)");
            t.HasCheckConstraint("CK_JwtTokens_ExpiresAt", 
                "[ExpiresAt] > [IssuedAt]");
            t.HasCheckConstraint("CK_JwtTokens_UseCount", 
                "[UseCount] >= 0");
        });
    }
}