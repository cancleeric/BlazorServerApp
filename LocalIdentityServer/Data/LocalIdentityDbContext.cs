using Microsoft.EntityFrameworkCore;
using LocalIdentityServer.Data.Entities;

namespace LocalIdentityServer.Data;

/// <summary>
/// LocalIdentityServer 資料庫上下文
/// 遵循開放封閉原則 (OCP) - 開放擴展但封閉修改
/// 使用依賴反轉原則 (DIP) - 透過介面抽象資料存取
/// </summary>
public class LocalIdentityDbContext : DbContext
{
    public LocalIdentityDbContext(DbContextOptions<LocalIdentityDbContext> options)
        : base(options)
    {
    }

    // DbSet 屬性 - 每個實體一個 DbSet
    public DbSet<UserEntity> Users { get; set; } = default!;
    public DbSet<ClientEntity> Clients { get; set; } = default!;
    public DbSet<AuthorizationCodeEntity> AuthorizationCodes { get; set; } = default!;
    public DbSet<RefreshTokenEntity> RefreshTokens { get; set; } = default!;
    public DbSet<PersistedKeyEntity> PersistedKeys { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置使用者實體
        ConfigureUserEntity(modelBuilder);
        
        // 配置客戶端實體
        ConfigureClientEntity(modelBuilder);
        
        // 配置授權碼實體
        ConfigureAuthorizationCodeEntity(modelBuilder);
        
        // 配置刷新權杖實體
        ConfigureRefreshTokenEntity(modelBuilder);
        
        // 配置持久化金鑰實體
        ConfigurePersistedKeyEntity(modelBuilder);
    }

    /// <summary>
    /// 配置使用者實體 - 遵循單一責任原則 (SRP)
    /// </summary>
    private static void ConfigureUserEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            // 索引設計
            entity.HasIndex(e => e.UserName)
                .IsUnique()
                .HasDatabaseName("IX_Users_UserName");

            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_Users_IsActive");

            // 屬性配置
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");
        });
    }

    /// <summary>
    /// 配置客戶端實體
    /// </summary>
    private static void ConfigureClientEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientEntity>(entity =>
        {
            // 索引設計
            entity.HasIndex(e => e.ClientName)
                .HasDatabaseName("IX_Clients_ClientName");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_Clients_IsActive");

            // 屬性配置
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");
        });
    }

    /// <summary>
    /// 配置授權碼實體
    /// </summary>
    private static void ConfigureAuthorizationCodeEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthorizationCodeEntity>(entity =>
        {
            // 外鍵關係
            entity.HasOne(e => e.Client)
                .WithMany(c => c.AuthorizationCodes)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.AuthorizationCodes)
                .HasForeignKey(e => e.Subject)
                .OnDelete(DeleteBehavior.Cascade);

            // 索引設計 - 效能優化
            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_AuthorizationCodes_ExpiresAt");

            entity.HasIndex(e => e.IsUsed)
                .HasDatabaseName("IX_AuthorizationCodes_IsUsed");

            entity.HasIndex(e => new { e.ClientId, e.Subject })
                .HasDatabaseName("IX_AuthorizationCodes_Client_Subject");

            // 屬性配置
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");
        });
    }

    /// <summary>
    /// 配置刷新權杖實體
    /// </summary>
    private static void ConfigureRefreshTokenEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            // 外鍵關係
            entity.HasOne(e => e.Client)
                .WithMany(c => c.RefreshTokens)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.Subject)
                .OnDelete(DeleteBehavior.Cascade);

            // 索引設計
            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

            entity.HasIndex(e => e.IsUsed)
                .HasDatabaseName("IX_RefreshTokens_IsUsed");

            entity.HasIndex(e => e.IsRevoked)
                .HasDatabaseName("IX_RefreshTokens_IsRevoked");

            entity.HasIndex(e => new { e.ClientId, e.Subject })
                .HasDatabaseName("IX_RefreshTokens_Client_Subject");

            // 屬性配置
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");
        });
    }

    /// <summary>
    /// 配置持久化金鑰實體
    /// </summary>
    private static void ConfigurePersistedKeyEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersistedKeyEntity>(entity =>
        {
            // 索引設計
            entity.HasIndex(e => e.KeyId)
                .IsUnique()
                .HasDatabaseName("IX_PersistedKeys_KeyId");

            entity.HasIndex(e => e.IsPrimary)
                .HasDatabaseName("IX_PersistedKeys_IsPrimary");

            entity.HasIndex(e => e.IsRevoked)
                .HasDatabaseName("IX_PersistedKeys_IsRevoked");

            entity.HasIndex(e => new { e.Use, e.Algorithm })
                .HasDatabaseName("IX_PersistedKeys_Use_Algorithm");

            // 屬性配置
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.ActivatedAt)
                .HasDefaultValueSql("datetime('now')");
        });
    }
}
