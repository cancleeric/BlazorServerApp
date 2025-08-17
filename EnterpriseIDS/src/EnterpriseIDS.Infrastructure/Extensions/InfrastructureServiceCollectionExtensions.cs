using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.Services;
using EnterpriseIDS.Infrastructure.Data;
using EnterpriseIDS.Infrastructure.Data.Repositories;

namespace EnterpriseIDS.Infrastructure.Extensions;

/// <summary>
/// Infrastructure 層服務註冊擴展
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 核心服務
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 註冊租戶上下文服務
        services.AddScoped<ITenantContextService, TenantContextService>();
        
        // 註冊資料存取服務
        services.AddScoped<IUserRepository, UserRepository>();
        
        return services;
    }
    
    /// <summary>
    /// 註冊資料庫上下文
    /// </summary>
    public static IServiceCollection AddEnterpriseIdentityDbContext(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "DefaultConnection")
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{connectionStringName}' not found in configuration.");
        }

        services.AddDbContext<EnterpriseIdentityDbContext>(options =>
        {
            // 根據連線字串判斷資料庫類型
            if (connectionString.Contains("Host=") || connectionString.Contains("Server=") && connectionString.Contains("Database="))
            {
                // PostgreSQL
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });
            }
            else if (connectionString.Contains("Data Source=") && connectionString.EndsWith(".db"))
            {
                // SQLite
                options.UseSqlite(connectionString);
            }
            else
            {
                // SQL Server (default)
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });
            }

            // 開發環境啟用敏感資料記錄
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        return services;
    }

    /// <summary>
    /// 註冊所有 Infrastructure 服務（一站式註冊）
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 註冊資料庫上下文
        services.AddEnterpriseIdentityDbContext(configuration);
        
        // 註冊核心服務
        services.AddInfrastructureServices(configuration);
        
        // 註冊 JWT Token 服務（如果需要）
        services.AddJwtTokenServices(configuration);
        services.AddJwtAuthentication(configuration);
        
        return services;
    }
}