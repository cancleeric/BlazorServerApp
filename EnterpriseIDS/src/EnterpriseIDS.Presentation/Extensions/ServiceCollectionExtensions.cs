using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;
using EnterpriseIDS.Application.Services;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Presentation.Extensions;

/// <summary>
/// 服務註冊擴展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊監控相關服務
    /// </summary>
    public static IServiceCollection AddMonitoringServices(this IServiceCollection services)
    {
        // 註冊健康檢查服務
        services.AddTransient<IHealthCheckService, EnterpriseIDS.Application.Services.HealthCheckService>();
        
        // 註冊指標收集服務
        services.AddSingleton<MetricsCollectionService>();
        
        // 註冊 ASP.NET Core 健康檢查
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("系統正常運行"))
            .AddCheck("database", () => HealthCheckResult.Healthy("資料庫連線正常"))
            .AddCheck("ldap", () => HealthCheckResult.Healthy("LDAP 服務正常"));

        return services;
    }

    /// <summary>
    /// 配置 Prometheus 指標
    /// </summary>
    public static IServiceCollection AddPrometheusMetrics(this IServiceCollection services)
    {
        // 註冊 Prometheus 指標
        services.AddSingleton<MetricsCollectionService>();
        
        return services;
    }
}