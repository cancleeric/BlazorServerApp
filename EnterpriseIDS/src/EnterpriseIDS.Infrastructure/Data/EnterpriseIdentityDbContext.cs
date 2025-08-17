using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Infrastructure.Data;

/// <summary>
/// Enterprise Identity Server 資料庫上下文
/// </summary>
public class EnterpriseIdentityDbContext : DbContext
{
    private readonly ITenantContextService? _tenantContextService;

    public EnterpriseIdentityDbContext(DbContextOptions<EnterpriseIdentityDbContext> options)
        : base(options)
    {
    }

    public EnterpriseIdentityDbContext(
        DbContextOptions<EnterpriseIdentityDbContext> options,
        ITenantContextService tenantContextService)
        : base(options)
    {
        _tenantContextService = tenantContextService;
    }

    // 租戶相關的 DbSet
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<TenantConfiguration> TenantConfigurations { get; set; } = null!;

    // 使用者和認證相關的 DbSet
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Group> Groups { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<UserGroupMembership> UserGroupMemberships { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<GroupRoleMapping> GroupRoleMappings { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;

    // LDAP 相關的 DbSet
    public DbSet<LdapConfiguration> LdapConfigurations { get; set; } = null!;

    // JWT Token 相關的 DbSet
    public DbSet<JwtToken> JwtTokens { get; set; } = null!;
    public DbSet<TokenBlacklist> TokenBlacklists { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 應用所有配置
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EnterpriseIdentityDbContext).Assembly);

        // 配置全域查詢篩選器
        ConfigureGlobalFilters(modelBuilder);

        // 配置索引
        ConfigureIndexes(modelBuilder);

        // 配置預設值
        ConfigureDefaults(modelBuilder);
    }

    /// <summary>
    /// 配置全域查詢篩選器
    /// </summary>
    private void ConfigureGlobalFilters(ModelBuilder modelBuilder)
    {
        // 軟刪除篩選器
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntityWithSoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(EnterpriseIdentityDbContext)
                    .GetMethod(nameof(GetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?
                    .MakeGenericMethod(entityType.ClrType);
                var filter = method?.Invoke(null, Array.Empty<object>());
                if (filter != null)
                {
                    entityType.SetQueryFilter((System.Linq.Expressions.LambdaExpression)filter);
                }
            }
        }

        // 租戶篩選器（只對 TenantAwareEntity 應用）
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantAwareEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(EnterpriseIdentityDbContext)
                    .GetMethod(nameof(GetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?
                    .MakeGenericMethod(entityType.ClrType);
                var filter = method?.Invoke(null, new object[] { this });
                if (filter != null)
                {
                    var existingFilter = entityType.GetQueryFilter();
                    if (existingFilter != null)
                    {
                        // 合併篩選器
                        var combinedFilter = CombineFilters(existingFilter, (System.Linq.Expressions.LambdaExpression)filter);
                        entityType.SetQueryFilter(combinedFilter);
                    }
                    else
                    {
                        entityType.SetQueryFilter((System.Linq.Expressions.LambdaExpression)filter);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 獲取軟刪除篩選器
    /// </summary>
    private static System.Linq.Expressions.LambdaExpression GetSoftDeleteFilter<TEntity>()
        where TEntity : BaseEntityWithSoftDelete
    {
        System.Linq.Expressions.Expression<Func<TEntity, bool>> filter = x => !x.IsDeleted;
        return filter;
    }

    /// <summary>
    /// 獲取租戶篩選器
    /// </summary>
    private static System.Linq.Expressions.LambdaExpression GetTenantFilter<TEntity>(EnterpriseIdentityDbContext context)
        where TEntity : TenantAwareEntity
    {
        System.Linq.Expressions.Expression<Func<TEntity, bool>> filter = x =>
            context._tenantContextService == null ||
            context._tenantContextService.IsSuperAdminContext() ||
            context._tenantContextService.GetCurrentTenantId() == null ||
            x.TenantId == context._tenantContextService.GetCurrentTenantId();
        return filter;
    }

    /// <summary>
    /// 合併兩個篩選器
    /// </summary>
    private static System.Linq.Expressions.LambdaExpression CombineFilters(
        System.Linq.Expressions.LambdaExpression filter1,
        System.Linq.Expressions.LambdaExpression filter2)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(filter1.Parameters[0].Type);

        var body1 = ReplaceParameter(filter1.Body, filter1.Parameters[0], parameter);
        var body2 = ReplaceParameter(filter2.Body, filter2.Parameters[0], parameter);

        var combinedBody = System.Linq.Expressions.Expression.AndAlso(body1, body2);

        return System.Linq.Expressions.Expression.Lambda(combinedBody, parameter);
    }

    /// <summary>
    /// 替換表達式中的參數
    /// </summary>
    private static System.Linq.Expressions.Expression ReplaceParameter(
        System.Linq.Expressions.Expression expression,
        System.Linq.Expressions.ParameterExpression oldParameter,
        System.Linq.Expressions.ParameterExpression newParameter)
    {
        return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
    }

    /// <summary>
    /// 配置索引
    /// </summary>
    private void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        // Tenant 索引
        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.Slug)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.PrimaryDomain)
            .HasFilter("[PrimaryDomain] IS NOT NULL AND [IsDeleted] = 0");

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.Status);

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.ParentTenantId)
            .HasFilter("[ParentTenantId] IS NOT NULL");

        // TenantConfiguration 索引
        modelBuilder.Entity<TenantConfiguration>()
            .HasIndex(tc => new { tc.TenantId, tc.ConfigKey })
            .IsUnique();

        modelBuilder.Entity<TenantConfiguration>()
            .HasIndex(tc => tc.Category)
            .HasFilter("[Category] IS NOT NULL");

        modelBuilder.Entity<TenantConfiguration>()
            .HasIndex(tc => tc.ConfigKey);

        // JWT Token 索引
        modelBuilder.Entity<JwtToken>()
            .HasIndex(t => t.JwtId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<JwtToken>()
            .HasIndex(t => new { t.UserId, t.TokenType, t.Status })
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<JwtToken>()
            .HasIndex(t => new { t.TenantId, t.TokenType })
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<JwtToken>()
            .HasIndex(t => t.ExpiresAt)
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<JwtToken>()
            .HasIndex(t => t.RefreshTokenId)
            .HasFilter("[RefreshTokenId] IS NOT NULL AND [IsDeleted] = 0");

        modelBuilder.Entity<JwtToken>()
            .HasIndex(t => t.ParentTokenId)
            .HasFilter("[ParentTokenId] IS NOT NULL AND [IsDeleted] = 0");

        // Token Blacklist 索引
        modelBuilder.Entity<TokenBlacklist>()
            .HasIndex(b => b.JwtId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<TokenBlacklist>()
            .HasIndex(b => b.TokenHash)
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<TokenBlacklist>()
            .HasIndex(b => new { b.UserId, b.Type })
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<TokenBlacklist>()
            .HasIndex(b => b.BlacklistExpiresAt)
            .HasFilter("[BlacklistExpiresAt] IS NOT NULL AND [IsDeleted] = 0");

        modelBuilder.Entity<TokenBlacklist>()
            .HasIndex(b => b.OriginalExpiresAt)
            .HasFilter("[IsDeleted] = 0");
    }

    /// <summary>
    /// 配置預設值
    /// </summary>
    private void ConfigureDefaults(ModelBuilder modelBuilder)
    {
        // BaseEntity 預設值
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property("CreatedAt")
                    .HasDefaultValueSql("GETUTCDATE()");
            }
        }
    }

    /// <summary>
    /// 儲存變更前處理
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await OnBeforeSavingAsync();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 儲存變更前處理
    /// </summary>
    public override int SaveChanges()
    {
        OnBeforeSaving();
        return base.SaveChanges();
    }

    /// <summary>
    /// 儲存前的處理邏輯
    /// </summary>
    private async Task OnBeforeSavingAsync()
    {
        var currentTenantId = _tenantContextService?.GetCurrentTenantId();
        var currentTime = DateTime.UtcNow;
        var isSuperAdmin = _tenantContextService?.IsSuperAdminContext() ?? true;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is BaseEntity baseEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        baseEntity.CreatedAt = currentTime;
                        
                        // 自動設定租戶 ID
                        if (baseEntity is TenantAwareEntity tenantAwareEntity && 
                            tenantAwareEntity.TenantId == Guid.Empty && 
                            currentTenantId.HasValue &&
                            !isSuperAdmin)
                        {
                            tenantAwareEntity.TenantId = currentTenantId.Value;
                        }
                        break;

                    case EntityState.Modified:
                        baseEntity.UpdatedAt = currentTime;
                        
                        // 防止跨租戶修改
                        if (baseEntity is TenantAwareEntity modifiedTenantEntity &&
                            !isSuperAdmin &&
                            currentTenantId.HasValue &&
                            modifiedTenantEntity.TenantId != currentTenantId.Value)
                        {
                            throw new UnauthorizedAccessException(
                                $"不允許修改其他租戶的資料: 實體租戶 {modifiedTenantEntity.TenantId} != 當前租戶 {currentTenantId}");
                        }
                        break;
                }
            }

            // 處理軟刪除
            if (entry.State == EntityState.Deleted && entry.Entity is BaseEntityWithSoftDelete softDeleteEntity)
            {
                entry.State = EntityState.Modified;
                softDeleteEntity.IsDeleted = true;
                softDeleteEntity.DeletedAt = currentTime;
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 儲存前的處理邏輯（同步版本）
    /// </summary>
    private void OnBeforeSaving()
    {
        var currentTenantId = _tenantContextService?.GetCurrentTenantId();
        var currentTime = DateTime.UtcNow;
        var isSuperAdmin = _tenantContextService?.IsSuperAdminContext() ?? true;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is BaseEntity baseEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        baseEntity.CreatedAt = currentTime;
                        
                        if (baseEntity is TenantAwareEntity tenantAwareEntity && 
                            tenantAwareEntity.TenantId == Guid.Empty && 
                            currentTenantId.HasValue &&
                            !isSuperAdmin)
                        {
                            tenantAwareEntity.TenantId = currentTenantId.Value;
                        }
                        break;

                    case EntityState.Modified:
                        baseEntity.UpdatedAt = currentTime;
                        
                        if (baseEntity is TenantAwareEntity modifiedTenantEntity &&
                            !isSuperAdmin &&
                            currentTenantId.HasValue &&
                            modifiedTenantEntity.TenantId != currentTenantId.Value)
                        {
                            throw new UnauthorizedAccessException(
                                $"不允許修改其他租戶的資料: 實體租戶 {modifiedTenantEntity.TenantId} != 當前租戶 {currentTenantId}");
                        }
                        break;
                }
            }

            if (entry.State == EntityState.Deleted && entry.Entity is BaseEntityWithSoftDelete softDeleteEntity)
            {
                entry.State = EntityState.Modified;
                softDeleteEntity.IsDeleted = true;
                softDeleteEntity.DeletedAt = currentTime;
            }
        }
    }

    /// <summary>
    /// 繞過租戶篩選執行操作
    /// </summary>
    public async Task<T> WithoutTenantFilterAsync<T>(Func<Task<T>> operation)
    {
        if (_tenantContextService != null)
        {
            return await _tenantContextService.WithoutTenantFilterAsync(operation);
        }
        return await operation();
    }

    /// <summary>
    /// 繞過租戶篩選執行操作
    /// </summary>
    public async Task WithoutTenantFilterAsync(Func<Task> operation)
    {
        if (_tenantContextService != null)
        {
            await _tenantContextService.WithoutTenantFilterAsync(operation);
        }
        else
        {
            await operation();
        }
    }
}

/// <summary>
/// 參數替換訪問器
/// </summary>
internal class ParameterReplacer : System.Linq.Expressions.ExpressionVisitor
{
    private readonly System.Linq.Expressions.ParameterExpression _oldParameter;
    private readonly System.Linq.Expressions.ParameterExpression _newParameter;

    public ParameterReplacer(
        System.Linq.Expressions.ParameterExpression oldParameter,
        System.Linq.Expressions.ParameterExpression newParameter)
    {
        _oldParameter = oldParameter;
        _newParameter = newParameter;
    }

    protected override System.Linq.Expressions.Expression VisitParameter(System.Linq.Expressions.ParameterExpression node)
    {
        return node == _oldParameter ? _newParameter : base.VisitParameter(node);
    }
}