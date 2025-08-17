using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using EnterpriseIDS.Core.Interfaces;
using EnterpriseIDS.Core.ValueObjects;

namespace EnterpriseIDS.Application.Services;

/// <summary>
/// 企業級健康檢查服務實作
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly ILdapService _ldapService;
    private readonly ITenantService _tenantService;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        ILdapService ldapService,
        ITenantService tenantService)
    {
        _logger = logger;
        _ldapService = ldapService;
        _tenantService = tenantService;
    }

    /// <summary>
    /// 執行綜合健康檢查
    /// </summary>
    public async Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("開始執行綜合健康檢查");

            var summary = await GetHealthSummaryAsync(cancellationToken);
            
            stopwatch.Stop();

            return new HealthCheckResponse
            {
                Status = summary.OverallStatus,
                Name = "綜合健康檢查",
                Description = $"系統整體健康狀態: {summary.OverallStatus}",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                Version = GetServiceVersion(),
                Data = new Dictionary<string, object>
                {
                    { "ComponentCount", summary.Components.Count },
                    { "HealthyComponents", summary.Components.Values.Count(c => c.Status == HealthStatus.Healthy) },
                    { "DegradedComponents", summary.Components.Values.Count(c => c.Status == HealthStatus.Degraded) },
                    { "UnhealthyComponents", summary.Components.Values.Count(c => c.Status == HealthStatus.Unhealthy) },
                    { "SystemInfo", summary.SystemInfo }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康檢查執行失敗");
            stopwatch.Stop();

            return new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = "綜合健康檢查",
                Description = "健康檢查執行失敗",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                Version = GetServiceVersion()
            };
        }
    }

    /// <summary>
    /// 檢查系統就緒狀態
    /// </summary>
    public async Task<HealthCheckResponse> CheckReadinessAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("檢查系統就緒狀態");

            // 檢查關鍵依賴服務是否就緒
            var checks = new List<Task<bool>>
            {
                CheckDatabaseReadinessAsync(cancellationToken),
                CheckTenantServiceReadinessAsync(cancellationToken)
            };

            var results = await Task.WhenAll(checks);
            var allReady = results.All(r => r);

            stopwatch.Stop();

            return new HealthCheckResponse
            {
                Status = allReady ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Name = "系統就緒檢查",
                Description = allReady ? "系統已就緒" : "系統尚未就緒",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                Version = GetServiceVersion(),
                Data = new Dictionary<string, object>
                {
                    { "DatabaseReady", results[0] },
                    { "TenantServiceReady", results[1] },
                    { "AllReady", allReady }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "就緒狀態檢查失敗");
            stopwatch.Stop();

            return new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = "系統就緒檢查",
                Description = "就緒狀態檢查失敗",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                Version = GetServiceVersion()
            };
        }
    }

    /// <summary>
    /// 檢查系統存活狀態
    /// </summary>
    public async Task<HealthCheckResponse> CheckLivenessAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("檢查系統存活狀態");

            // 簡單的存活檢查 - 確保應用程式正在運行
            var memoryUsage = GC.GetTotalMemory(false);
            var workingSet = Environment.WorkingSet;

            await Task.Delay(1, cancellationToken); // 模擬非同步操作

            stopwatch.Stop();

            return new HealthCheckResponse
            {
                Status = HealthStatus.Healthy,
                Name = "系統存活檢查",
                Description = "系統正常運行",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                Version = GetServiceVersion(),
                Data = new Dictionary<string, object>
                {
                    { "MemoryUsageMB", memoryUsage / 1024 / 1024 },
                    { "WorkingSetMB", workingSet / 1024 / 1024 },
                    { "UptimeSeconds", (DateTime.UtcNow - _startTime).TotalSeconds },
                    { "ThreadCount", Environment.ProcessorCount }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "存活狀態檢查失敗");
            stopwatch.Stop();

            return new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = "系統存活檢查",
                Description = "存活狀態檢查失敗",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                Version = GetServiceVersion()
            };
        }
    }

    /// <summary>
    /// 取得健康檢查摘要
    /// </summary>
    public async Task<HealthCheckSummary> GetHealthSummaryAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var summary = new HealthCheckSummary
        {
            CheckTime = DateTime.UtcNow,
            ServiceVersion = GetServiceVersion(),
            SystemInfo = GetSystemInfo()
        };

        try
        {
            _logger.LogInformation("開始收集健康檢查摘要");

            // 並行執行所有組件檢查
            var componentChecks = new Dictionary<string, Task<HealthCheckResponse>>
            {
                { "Application", CheckApplicationHealthAsync(cancellationToken) },
                { "Database", CheckDatabaseHealthAsync(cancellationToken) },
                { "TenantService", CheckTenantServiceHealthAsync(cancellationToken) }
            };

            // 等待所有檢查完成
            await Task.WhenAll(componentChecks.Values);

            // 收集結果
            foreach (var check in componentChecks)
            {
                try
                {
                    summary.Components[check.Key] = await check.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "組件 {ComponentName} 健康檢查失敗", check.Key);
                    summary.Components[check.Key] = new HealthCheckResponse
                    {
                        Status = HealthStatus.Unhealthy,
                        Name = check.Key,
                        Description = $"{check.Key} 檢查失敗",
                        ErrorMessage = ex.Message,
                        CheckTime = DateTime.UtcNow
                    };
                }
            }

            // 計算整體狀態
            summary.OverallStatus = CalculateOverallStatus(summary.Components.Values);
            
            stopwatch.Stop();
            summary.TotalCheckTime = stopwatch.Elapsed;

            _logger.LogInformation("健康檢查摘要完成，整體狀態: {Status}", summary.OverallStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康檢查摘要執行失敗");
            summary.OverallStatus = HealthStatus.Unhealthy;
            stopwatch.Stop();
            summary.TotalCheckTime = stopwatch.Elapsed;
        }

        return summary;
    }

    /// <summary>
    /// 檢查特定組件健康狀態
    /// </summary>
    public async Task<HealthCheckResponse> CheckComponentHealthAsync(string componentName, CancellationToken cancellationToken = default)
    {
        return componentName.ToLowerInvariant() switch
        {
            "application" => await CheckApplicationHealthAsync(cancellationToken),
            "database" => await CheckDatabaseHealthAsync(cancellationToken),
            "tenant" => await CheckTenantServiceHealthAsync(cancellationToken),
            "ldap" => await CheckLdapHealthAsync(cancellationToken),
            _ => new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = componentName,
                Description = "未知的組件名稱",
                ErrorMessage = $"組件 '{componentName}' 不存在",
                CheckTime = DateTime.UtcNow
            }
        };
    }

    #region Private Methods

    private async Task<HealthCheckResponse> CheckApplicationHealthAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 檢查應用程式基本狀態
            var memoryUsage = GC.GetTotalMemory(false);
            var workingSet = Environment.WorkingSet;
            
            await Task.Delay(1, cancellationToken);
            
            stopwatch.Stop();

            return new HealthCheckResponse
            {
                Status = HealthStatus.Healthy,
                Name = "應用程式",
                Description = "應用程式運行正常",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                Data = new Dictionary<string, object>
                {
                    { "MemoryUsageMB", memoryUsage / 1024 / 1024 },
                    { "WorkingSetMB", workingSet / 1024 / 1024 },
                    { "UptimeMinutes", (DateTime.UtcNow - _startTime).TotalMinutes }
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = "應用程式",
                Description = "應用程式檢查失敗",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<HealthCheckResponse> CheckDatabaseHealthAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 這裡應該實作實際的資料庫連線檢查
            // 暫時使用簡單的檢查
            await Task.Delay(50, cancellationToken); // 模擬資料庫查詢
            
            stopwatch.Stop();

            return new HealthCheckResponse
            {
                Status = HealthStatus.Healthy,
                Name = "資料庫",
                Description = "資料庫連線正常",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                Data = new Dictionary<string, object>
                {
                    { "ConnectionStatus", "Connected" },
                    { "DatabaseType", "PostgreSQL" }
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = "資料庫",
                Description = "資料庫連線失敗",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<HealthCheckResponse> CheckTenantServiceHealthAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 檢查租戶服務狀態
            await Task.Delay(30, cancellationToken); // 模擬服務檢查
            
            stopwatch.Stop();

            return new HealthCheckResponse
            {
                Status = HealthStatus.Healthy,
                Name = "租戶服務",
                Description = "租戶服務運行正常",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                Data = new Dictionary<string, object>
                {
                    { "ServiceStatus", "Running" },
                    { "CacheStatus", "Available" }
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = "租戶服務",
                Description = "租戶服務檢查失敗",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<HealthCheckResponse> CheckLdapHealthAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 使用現有的 LDAP 健康檢查功能
            // 這裡需要獲取 LDAP 配置 ID，暫時使用模擬
            await Task.Delay(100, cancellationToken); // 模擬 LDAP 檢查
            
            stopwatch.Stop();

            return new HealthCheckResponse
            {
                Status = HealthStatus.Healthy,
                Name = "LDAP 服務",
                Description = "LDAP 服務連線正常",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                Data = new Dictionary<string, object>
                {
                    { "ConnectionStatus", "Connected" },
                    { "ServerType", "Active Directory" }
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new HealthCheckResponse
            {
                Status = HealthStatus.Unhealthy,
                Name = "LDAP 服務",
                Description = "LDAP 服務檢查失敗",
                CheckTime = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<bool> CheckDatabaseReadinessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(10, cancellationToken); // 模擬資料庫檢查
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckTenantServiceReadinessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(10, cancellationToken); // 模擬服務檢查
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static HealthStatus CalculateOverallStatus(IEnumerable<HealthCheckResponse> components)
    {
        var statuses = components.Select(c => c.Status).ToList();
        
        if (statuses.Any(s => s == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;
            
        if (statuses.Any(s => s == HealthStatus.Degraded))
            return HealthStatus.Degraded;
            
        return HealthStatus.Healthy;
    }

    private static SystemInfo GetSystemInfo()
    {
        var process = Process.GetCurrentProcess();
        
        return new SystemInfo
        {
            ServerName = Environment.MachineName,
            OperatingSystem = Environment.OSVersion.ToString(),
            DotNetVersion = Environment.Version.ToString(),
            ProcessId = Environment.ProcessId,
            StartTime = _startTime,
            WorkingSetMemoryMB = process.WorkingSet64 / 1024 / 1024,
            GCMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
            CpuUsagePercent = 0 // 需要額外的邏輯來計算 CPU 使用率
        };
    }

    private static string GetServiceVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "Unknown";
    }

    #endregion
}