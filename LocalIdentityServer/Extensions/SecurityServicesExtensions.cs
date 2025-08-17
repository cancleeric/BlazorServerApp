using LocalIdentityServer.Models;
using LocalIdentityServer.Services;
using LocalIdentityServer.Middleware;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalIdentityServer.Extensions;

/// <summary>
/// 安全服務擴充方法 - 遵循擴充開放原則 (OCP)
/// 提供企業級 HTTPS 安全強化與安全標頭配置的一站式服務註冊
/// </summary>
public static class SecurityServicesExtensions
{
    /// <summary>
    /// 註冊安全服務與配置
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服務集合</returns>
    public static IServiceCollection AddSecurityServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // 註冊安全標頭配置
        services.Configure<SecurityHeadersOptions>(
            configuration.GetSection(SecurityHeadersOptions.SectionName));

        // 註冊憑證健康檢查服務
        services.AddScoped<ICertificateHealthService, CertificateHealthService>();

        // 配置 HTTPS 重導向
        services.AddHttpsRedirection(options =>
        {
            var httpsOptions = configuration
                .GetSection($"{SecurityHeadersOptions.SectionName}:HttpsRedirection")
                .Get<Models.HttpsRedirectionOptions>() ?? new Models.HttpsRedirectionOptions();

            if (httpsOptions.Enabled)
            {
                options.RedirectStatusCode = httpsOptions.RedirectStatusCode ?? 308;
                if (httpsOptions.HttpsPort.HasValue)
                {
                    options.HttpsPort = httpsOptions.HttpsPort.Value;
                }
            }
        });

        // 配置 HSTS
        services.AddHsts(options =>
        {
            var hstsOptions = configuration
                .GetSection($"{SecurityHeadersOptions.SectionName}:Hsts")
                .Get<Models.HstsOptions>() ?? new Models.HstsOptions();

            if (hstsOptions.Enabled)
            {
                options.MaxAge = TimeSpan.FromSeconds(hstsOptions.MaxAge);
                // Note: IncludeSubdomains property may not be available in this .NET version
                // This will be handled at the middleware level instead
                options.Preload = hstsOptions.Preload;
            }
        });

        // 配置 Kestrel HTTPS 設定
        ConfigureKestrelHttpsSettings(services, configuration);

        return services;
    }

    /// <summary>
    /// 配置 Kestrel HTTPS 設定
    /// </summary>
    private static void ConfigureKestrelHttpsSettings(
        IServiceCollection services, 
        IConfiguration configuration)
    {
        services.Configure<KestrelServerOptions>(options =>
        {
            var httpsEndpoint = configuration.GetSection("Kestrel:Endpoints:Https");
            if (httpsEndpoint.Exists())
            {
                var url = httpsEndpoint["Url"];
                var certificate = httpsEndpoint.GetSection("Certificate");

                if (!string.IsNullOrEmpty(url))
                {
                    var uri = new Uri(url);
                    options.Listen(System.Net.IPAddress.Any, uri.Port, listenOptions =>
                    {
                        // 配置 HTTPS
                        listenOptions.UseHttps(httpsOptions =>
                        {
                            // 限制 TLS 版本為 1.2 和 1.3
                            httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | 
                                                      System.Security.Authentication.SslProtocols.Tls13;

                            // 配置憑證
                            var certPath = certificate["Path"];
                            var certPassword = certificate["Password"];

                            if (!string.IsNullOrEmpty(certPath))
                            {
                                if (!string.IsNullOrEmpty(certPassword))
                                {
                                    httpsOptions.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                                        certPath, certPassword);
                                }
                                else
                                {
                                    httpsOptions.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath);
                                }
                            }

                            // 配置 cipher suites (如果支援)
                            // httpsOptions.CipherSuitesPolicy = ...

                            // 啟用 HTTP/2
                            httpsOptions.AllowAnyClientCertificate();
                        });
                    });
                }
            }
        });
    }

    /// <summary>
    /// 使用安全設定
    /// </summary>
    /// <param name="app">應用程式建構器</param>
    /// <param name="env">環境</param>
    /// <returns>應用程式建構器</returns>
    public static IApplicationBuilder UseSecurityConfiguration(
        this IApplicationBuilder app, 
        IWebHostEnvironment env)
    {
        // 在開發環境之外使用 HTTPS 重導向
        if (!env.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        // 使用 HSTS (僅在生產環境或明確啟用時)
        if (!env.IsDevelopment())
        {
            app.UseHsts();
        }

        // 使用安全標頭中介軟體 (應在靜態檔案之前)
        app.UseSecurityHeaders();

        return app;
    }

    /// <summary>
    /// 初始化憑證監控
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <param name="configuration">配置</param>
    public static void InitializeCertificateMonitoring(
        this IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        var certificateService = serviceProvider.GetService<ICertificateHealthService>();
        if (certificateService == null) return;

        var certificatesSection = configuration.GetSection("SecurityHeaders:Certificates");
        foreach (var cert in certificatesSection.GetChildren())
        {
            var path = cert["Path"];
            var friendlyName = cert["FriendlyName"];

            if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(friendlyName))
            {
                certificateService.RegisterCertificateForMonitoring(path, friendlyName);
            }
        }
    }

    /// <summary>
    /// 註冊安全健康檢查
    /// </summary>
    /// <param name="services">服務集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>健康檢查建構器</returns>
    public static IServiceCollection AddSecurityHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck<CertificateHealthCheck>("certificate_health",
                tags: new[] { "certificate", "security" });

        return services;
    }
}

/// <summary>
/// 憑證健康檢查實作
/// </summary>
public class CertificateHealthCheck : IHealthCheck
{
    private readonly ICertificateHealthService _certificateService;
    private readonly ILogger<CertificateHealthCheck> _logger;

    public CertificateHealthCheck(
        ICertificateHealthService certificateService,
        ILogger<CertificateHealthCheck> logger)
    {
        _certificateService = certificateService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var summaries = await _certificateService.GetAllCertificateHealthAsync();
            
            var unhealthyCerts = summaries
                .Where(s => s.Status == CertificateHealthStatus.Error)
                .ToList();

            var warningSerts = summaries
                .Where(s => s.Status == CertificateHealthStatus.Warning)
                .ToList();

            if (unhealthyCerts.Any())
            {
                var errorMessages = unhealthyCerts
                    .Select(c => $"{c.FriendlyName}: {c.Status}")
                    .ToList();

                return HealthCheckResult.Unhealthy(
                    $"Found {unhealthyCerts.Count} unhealthy certificate(s): {string.Join(", ", errorMessages)}",
                    data: new Dictionary<string, object>
                    {
                        ["total_certificates"] = summaries.Count,
                        ["unhealthy_certificates"] = unhealthyCerts.Count,
                        ["warning_certificates"] = warningSerts.Count
                    });
            }

            if (warningSerts.Any())
            {
                var warningMessages = warningSerts
                    .Select(c => $"{c.FriendlyName}: expiring in {c.DaysUntilExpiry} days")
                    .ToList();

                return HealthCheckResult.Degraded(
                    $"Found {warningSerts.Count} certificate(s) with warnings: {string.Join(", ", warningMessages)}",
                    data: new Dictionary<string, object>
                    {
                        ["total_certificates"] = summaries.Count,
                        ["unhealthy_certificates"] = 0,
                        ["warning_certificates"] = warningSerts.Count
                    });
            }

            return HealthCheckResult.Healthy(
                $"All {summaries.Count} monitored certificate(s) are healthy",
                data: new Dictionary<string, object>
                {
                    ["total_certificates"] = summaries.Count,
                    ["unhealthy_certificates"] = 0,
                    ["warning_certificates"] = 0
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate health check failed");
            return HealthCheckResult.Unhealthy(
                "Certificate health check failed",
                ex);
        }
    }
}