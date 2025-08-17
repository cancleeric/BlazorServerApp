using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Infrastructure.Caching;
using EnterpriseIDS.Infrastructure.Services;
using EnterpriseIDS.Infrastructure.Middleware;

namespace EnterpriseIDS.Infrastructure.Extensions;

/// <summary>
/// 快取服務註冊擴展方法
/// </summary>
public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Redis 分散式快取服務
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服務集合</returns>
    public static IServiceCollection AddRedisDistributedCaching(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // 取得 Redis 連接字串
        var connectionString = configuration.GetConnectionString("Redis") 
            ?? configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis 連接字串未設定");

        // 註冊 Redis 分散式快取
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = configuration["Redis:InstanceName"] ?? "EnterpriseIDS";
            
            // 設定序列化選項
            options.ConfigurationOptions = StackExchange.Redis.ConfigurationOptions.Parse(connectionString);
            options.ConfigurationOptions.AbortOnConnectFail = false;
            options.ConfigurationOptions.ConnectRetry = 3;
            options.ConfigurationOptions.ConnectTimeout = 5000;
            options.ConfigurationOptions.DefaultDatabase = 
                int.Parse(configuration["Redis:Database"] ?? "0");
        });

        // 註冊自定義快取服務
        services.AddSingleton<ITenantContextCache, RedisTenantContextCache>();
        services.AddSingleton<IDistributedSessionService, RedisDistributedSessionService>();

        return services;
    }

    /// <summary>
    /// 註冊快取健康檢查
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服務集合</returns>
    public static IServiceCollection AddCachingHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:ConnectionString"];

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddHealthChecks()
                .AddRedis(connectionString, name: "redis", tags: new[] { "cache", "redis" });
        }

        return services;
    }

    /// <summary>
    /// 註冊記憶體快取（開發/測試環境）
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <returns>服務集合</returns>
    public static IServiceCollection AddInMemoryDistributedCaching(this IServiceCollection services)
    {
        // 註冊記憶體分散式快取
        services.AddDistributedMemoryCache();

        // 註冊自定義快取服務
        services.AddSingleton<ITenantContextCache, RedisTenantContextCache>();
        services.AddSingleton<IDistributedSessionService, RedisDistributedSessionService>();

        return services;
    }

    /// <summary>
    /// 根據環境自動選擇快取提供者
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服務集合</returns>
    public static IServiceCollection AddDistributedCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cacheProvider = configuration["Caching:Provider"]?.ToLowerInvariant() ?? "redis";
        var environment = configuration["ASPNETCORE_ENVIRONMENT"]?.ToLowerInvariant();

        switch (cacheProvider)
        {
            case "redis":
                services.AddRedisDistributedCaching(configuration);
                services.AddCachingHealthChecks(configuration);
                break;
            
            case "memory":
                services.AddInMemoryDistributedCaching();
                break;
            
            default:
                // 在開發環境使用記憶體快取，生產環境使用 Redis
                if (environment == "development" || environment == "testing")
                {
                    services.AddInMemoryDistributedCaching();
                }
                else
                {
                    services.AddRedisDistributedCaching(configuration);
                    services.AddCachingHealthChecks(configuration);
                }
                break;
        }

        return services;
    }

    /// <summary>
    /// 註冊分散式會話管理服務
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服務集合</returns>
    public static IServiceCollection AddDistributedSessionManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 配置會話選項
        services.Configure<DistributedSessionOptions>(options =>
        {
            configuration.GetSection("DistributedSession").Bind(options);
        });

        // 註冊背景清理服務
        services.AddHostedService<SessionCleanupBackgroundService>();

        return services;
    }

    /// <summary>
    /// 註冊完整的分散式快取和會話管理
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服務集合</returns>
    public static IServiceCollection AddEnterpriseDistributedCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 註冊分散式快取
        services.AddDistributedCaching(configuration);

        // 註冊會話管理
        services.AddDistributedSessionManagement(configuration);

        return services;
    }
}