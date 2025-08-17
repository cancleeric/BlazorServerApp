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
    
    // MFA 相關實體
    public DbSet<UserMfaEntity> UserMfaMethods { get; set; } = default!;
    public DbSet<MfaBackupCodeEntity> MfaBackupCodes { get; set; } = default!;
    public DbSet<MfaAuditLogEntity> MfaAuditLogs { get; set; } = default!;

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
        
        // 配置 MFA 相關實體
        ConfigureUserMfaEntity(modelBuilder);
        ConfigureMfaBackupCodeEntity(modelBuilder);
        ConfigureMfaAuditLogEntity(modelBuilder);
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

            // Token 家族索引 - 支援快速查詢同家族 Token
            entity.HasIndex(e => e.TokenFamily)
                .HasDatabaseName("IX_RefreshTokens_TokenFamily");

            // 複合索引 - Token 家族與狀態
            entity.HasIndex(e => new { e.TokenFamily, e.IsRevoked, e.IsUsed })
                .HasDatabaseName("IX_RefreshTokens_Family_Status");

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
            // 唯一索引
            entity.HasIndex(e => e.KeyId)
                .IsUnique()
                .HasDatabaseName("IX_PersistedKeys_KeyId");

            // 查詢效能索引
            entity.HasIndex(e => e.IsPrimary)
                .HasDatabaseName("IX_PersistedKeys_IsPrimary");

            entity.HasIndex(e => e.IsRevoked)
                .HasDatabaseName("IX_PersistedKeys_IsRevoked");

            entity.HasIndex(e => e.IsDeleted)
                .HasDatabaseName("IX_PersistedKeys_IsDeleted");

            // 複合索引 - 金鑰類型與演算法
            entity.HasIndex(e => new { e.Use, e.Algorithm })
                .HasDatabaseName("IX_PersistedKeys_Use_Algorithm");

            // 複合索引 - 狀態與到期時間 (查詢效能優化)
            entity.HasIndex(e => new { e.IsRevoked, e.ExpiresAt })
                .HasDatabaseName("IX_PersistedKeys_Status_ExpiresAt");

            // 複合索引 - 軟刪除與建立時間
            entity.HasIndex(e => new { e.IsDeleted, e.CreatedAt })
                .HasDatabaseName("IX_PersistedKeys_Deleted_Created");

            // 版本管理索引
            entity.HasIndex(e => new { e.KeyId, e.Version })
                .HasDatabaseName("IX_PersistedKeys_KeyId_Version");

            // 屬性配置
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.LastModifiedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.ActivatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.Version)
                .HasDefaultValue(1);

            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false);

            // 軟刪除全域查詢篩選器
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
    }

    /// <summary>
    /// 配置使用者 MFA 實體
    /// </summary>
    private static void ConfigureUserMfaEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserMfaEntity>(entity =>
        {
            // 外鍵關係
            entity.HasOne(e => e.User)
                .WithMany(u => u.MfaMethods)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 索引設計
            entity.HasIndex(e => new { e.UserId, e.Method })
                .IsUnique()
                .HasDatabaseName("IX_UserMfa_User_Method");

            entity.HasIndex(e => e.IsEnabled)
                .HasDatabaseName("IX_UserMfa_IsEnabled");

            entity.HasIndex(e => e.IsPrimary)
                .HasDatabaseName("IX_UserMfa_IsPrimary");

            entity.HasIndex(e => e.LastUsedAt)
                .HasDatabaseName("IX_UserMfa_LastUsed");

            entity.HasIndex(e => e.LockedUntil)
                .HasDatabaseName("IX_UserMfa_LockedUntil");

            // 複合索引 - 查詢效能優化
            entity.HasIndex(e => new { e.UserId, e.IsEnabled, e.IsPrimary })
                .HasDatabaseName("IX_UserMfa_User_Status");

            // 屬性配置
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.FailedAttempts)
                .HasDefaultValue(0);
        });
    }

    /// <summary>
    /// 配置 MFA 備用碼實體
    /// </summary>
    private static void ConfigureMfaBackupCodeEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MfaBackupCodeEntity>(entity =>
        {
            // 外鍵關係
            entity.HasOne(e => e.User)
                .WithMany(u => u.MfaBackupCodes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 索引設計
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_MfaBackupCodes_User");

            entity.HasIndex(e => e.BatchId)
                .HasDatabaseName("IX_MfaBackupCodes_Batch");

            entity.HasIndex(e => e.IsUsed)
                .HasDatabaseName("IX_MfaBackupCodes_IsUsed");

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_MfaBackupCodes_ExpiresAt");

            // 複合索引 - 查詢可用備用碼
            entity.HasIndex(e => new { e.UserId, e.IsUsed, e.ExpiresAt })
                .HasDatabaseName("IX_MfaBackupCodes_User_Available");

            // 屬性配置
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");
        });
    }

    /// <summary>
    /// 配置 MFA 審計日誌實體
    /// </summary>
    private static void ConfigureMfaAuditLogEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MfaAuditLogEntity>(entity =>
        {
            // 外鍵關係
            entity.HasOne(e => e.User)
                .WithMany(u => u.MfaAuditLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MfaMethod)
                .WithMany(m => m.AuditLogs)
                .HasForeignKey(e => e.MfaMethodId)
                .OnDelete(DeleteBehavior.SetNull);

            // 索引設計
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_MfaAuditLogs_User");

            entity.HasIndex(e => e.EventType)
                .HasDatabaseName("IX_MfaAuditLogs_EventType");

            entity.HasIndex(e => e.Method)
                .HasDatabaseName("IX_MfaAuditLogs_Method");

            entity.HasIndex(e => e.Result)
                .HasDatabaseName("IX_MfaAuditLogs_Result");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_MfaAuditLogs_CreatedAt");

            entity.HasIndex(e => e.IpAddress)
                .HasDatabaseName("IX_MfaAuditLogs_IpAddress");

            entity.HasIndex(e => e.RiskScore)
                .HasDatabaseName("IX_MfaAuditLogs_RiskScore");

            // 複合索引 - 安全分析查詢
            entity.HasIndex(e => new { e.UserId, e.Result, e.CreatedAt })
                .HasDatabaseName("IX_MfaAuditLogs_User_Result_Time");

            entity.HasIndex(e => new { e.IpAddress, e.Result, e.CreatedAt })
                .HasDatabaseName("IX_MfaAuditLogs_IP_Result_Time");

            entity.HasIndex(e => new { e.EventType, e.Method, e.CreatedAt })
                .HasDatabaseName("IX_MfaAuditLogs_Event_Method_Time");

            // 屬性配置
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.RiskScore)
                .HasDefaultValue(0);
        });
    }
}
