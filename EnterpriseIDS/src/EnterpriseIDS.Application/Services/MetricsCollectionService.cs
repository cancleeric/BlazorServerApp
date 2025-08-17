using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace EnterpriseIDS.Application.Services;

/// <summary>
/// Prometheus 指標收集服務
/// </summary>
public class MetricsCollectionService
{
    private readonly ILogger<MetricsCollectionService> _logger;

    // 系統指標
    private static readonly Gauge SystemMemoryUsage = Metrics
        .CreateGauge("enterprise_ids_memory_usage_bytes", "系統記憶體使用量 (bytes)");

    private static readonly Gauge SystemCpuUsage = Metrics
        .CreateGauge("enterprise_ids_cpu_usage_percent", "CPU 使用率 (%)");

    private static readonly Gauge SystemUptime = Metrics
        .CreateGauge("enterprise_ids_uptime_seconds", "系統運行時間 (秒)");

    // 應用程式指標
    private static readonly Counter HttpRequestsTotal = Metrics
        .CreateCounter("enterprise_ids_http_requests_total", "HTTP 請求總數", new[] { "method", "endpoint", "status_code" });

    private static readonly Histogram HttpRequestDuration = Metrics
        .CreateHistogram("enterprise_ids_http_request_duration_seconds", "HTTP 請求持續時間", new[] { "method", "endpoint" });

    private static readonly Gauge ActiveConnections = Metrics
        .CreateGauge("enterprise_ids_active_connections", "活躍連線數");

    // 業務指標
    private static readonly Counter AuthenticationAttempts = Metrics
        .CreateCounter("enterprise_ids_authentication_attempts_total", "認證嘗試總數", new[] { "result", "tenant_id" });

    private static readonly Counter TokensIssued = Metrics
        .CreateCounter("enterprise_ids_tokens_issued_total", "已發行 Token 總數", new[] { "token_type", "tenant_id" });

    private static readonly Gauge ActiveSessions = Metrics
        .CreateGauge("enterprise_ids_active_sessions", "活躍會話數", new[] { "tenant_id" });

    private static readonly Counter TenantOperations = Metrics
        .CreateCounter("enterprise_ids_tenant_operations_total", "租戶操作總數", new[] { "operation", "tenant_id" });

    // LDAP 指標
    private static readonly Counter LdapOperations = Metrics
        .CreateCounter("enterprise_ids_ldap_operations_total", "LDAP 操作總數", new[] { "operation", "result" });

    private static readonly Histogram LdapOperationDuration = Metrics
        .CreateHistogram("enterprise_ids_ldap_operation_duration_seconds", "LDAP 操作持續時間", new[] { "operation" });

    private static readonly Gauge LdapConnectionsActive = Metrics
        .CreateGauge("enterprise_ids_ldap_connections_active", "活躍 LDAP 連線數");

    // 健康檢查指標
    private static readonly Gauge HealthCheckStatus = Metrics
        .CreateGauge("enterprise_ids_health_check_status", "健康檢查狀態 (0=Unhealthy, 1=Degraded, 2=Healthy)", new[] { "component" });

    private static readonly Histogram HealthCheckDuration = Metrics
        .CreateHistogram("enterprise_ids_health_check_duration_seconds", "健康檢查持續時間", new[] { "component" });

    private static readonly DateTime _startTime = DateTime.UtcNow;

    public MetricsCollectionService(ILogger<MetricsCollectionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 更新系統指標
    /// </summary>
    public void UpdateSystemMetrics()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            
            // 記憶體使用量
            SystemMemoryUsage.Set(process.WorkingSet64);
            
            // 系統運行時間
            var uptime = (DateTime.UtcNow - _startTime).TotalSeconds;
            SystemUptime.Set(uptime);
            
            // CPU 使用率 (需要更複雜的計算，這裡先使用簡化版本)
            var cpuUsage = GetCpuUsage();
            SystemCpuUsage.Set(cpuUsage);
            
            _logger.LogDebug("系統指標已更新");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新系統指標失敗");
        }
    }

    /// <summary>
    /// 記錄 HTTP 請求指標
    /// </summary>
    public void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration)
    {
        try
        {
            HttpRequestsTotal.WithLabels(method, endpoint, statusCode.ToString()).Inc();
            HttpRequestDuration.WithLabels(method, endpoint).Observe(duration.TotalSeconds);
            
            _logger.LogDebug("HTTP 請求指標已記錄: {Method} {Endpoint} {StatusCode} ({Duration}ms)", 
                method, endpoint, statusCode, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "記錄 HTTP 請求指標失敗");
        }
    }

    /// <summary>
    /// 記錄認證嘗試指標
    /// </summary>
    public void RecordAuthenticationAttempt(string result, string tenantId = "unknown")
    {
        try
        {
            AuthenticationAttempts.WithLabels(result, tenantId).Inc();
            _logger.LogDebug("認證嘗試指標已記錄: {Result} for Tenant {TenantId}", result, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "記錄認證嘗試指標失敗");
        }
    }

    /// <summary>
    /// 記錄 Token 發行指標
    /// </summary>
    public void RecordTokenIssued(string tokenType, string tenantId = "unknown")
    {
        try
        {
            TokensIssued.WithLabels(tokenType, tenantId).Inc();
            _logger.LogDebug("Token 發行指標已記錄: {TokenType} for Tenant {TenantId}", tokenType, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "記錄 Token 發行指標失敗");
        }
    }

    /// <summary>
    /// 設定活躍會話數
    /// </summary>
    public void SetActiveSessions(int count, string tenantId = "all")
    {
        try
        {
            ActiveSessions.WithLabels(tenantId).Set(count);
            _logger.LogDebug("活躍會話數已設定: {Count} for Tenant {TenantId}", count, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設定活躍會話數失敗");
        }
    }

    /// <summary>
    /// 記錄租戶操作指標
    /// </summary>
    public void RecordTenantOperation(string operation, string tenantId)
    {
        try
        {
            TenantOperations.WithLabels(operation, tenantId).Inc();
            _logger.LogDebug("租戶操作指標已記錄: {Operation} for Tenant {TenantId}", operation, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "記錄租戶操作指標失敗");
        }
    }

    /// <summary>
    /// 記錄 LDAP 操作指標
    /// </summary>
    public void RecordLdapOperation(string operation, string result, TimeSpan duration)
    {
        try
        {
            LdapOperations.WithLabels(operation, result).Inc();
            LdapOperationDuration.WithLabels(operation).Observe(duration.TotalSeconds);
            
            _logger.LogDebug("LDAP 操作指標已記錄: {Operation} {Result} ({Duration}ms)", 
                operation, result, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "記錄 LDAP 操作指標失敗");
        }
    }

    /// <summary>
    /// 設定 LDAP 活躍連線數
    /// </summary>
    public void SetLdapActiveConnections(int count)
    {
        try
        {
            LdapConnectionsActive.Set(count);
            _logger.LogDebug("LDAP 活躍連線數已設定: {Count}", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設定 LDAP 活躍連線數失敗");
        }
    }

    /// <summary>
    /// 記錄健康檢查指標
    /// </summary>
    public void RecordHealthCheck(string component, int status, TimeSpan duration)
    {
        try
        {
            HealthCheckStatus.WithLabels(component).Set(status);
            HealthCheckDuration.WithLabels(component).Observe(duration.TotalSeconds);
            
            _logger.LogDebug("健康檢查指標已記錄: {Component} Status={Status} ({Duration}ms)", 
                component, status, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "記錄健康檢查指標失敗");
        }
    }

    /// <summary>
    /// 設定活躍連線數
    /// </summary>
    public void SetActiveConnections(int count)
    {
        try
        {
            ActiveConnections.Set(count);
            _logger.LogDebug("活躍連線數已設定: {Count}", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設定活躍連線數失敗");
        }
    }

    /// <summary>
    /// 取得 CPU 使用率 (簡化版本)
    /// </summary>
    private static double GetCpuUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;
            
            Thread.Sleep(500); // 等待一段時間來計算 CPU 使用率
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return cpuUsageTotal * 100;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 取得所有自訂指標的摘要
    /// </summary>
    public Dictionary<string, object> GetMetricsSummary()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            
            return new Dictionary<string, object>
            {
                { "SystemMetrics", new {
                    MemoryUsageBytes = process.WorkingSet64,
                    UptimeSeconds = (DateTime.UtcNow - _startTime).TotalSeconds,
                    CpuUsagePercent = GetCpuUsage()
                }},
                { "ApplicationMetrics", new {
                    ActiveConnections = "Tracked via Prometheus",
                    HttpRequests = "Tracked via Prometheus"
                }},
                { "BusinessMetrics", new {
                    AuthenticationAttempts = "Tracked via Prometheus",
                    TokensIssued = "Tracked via Prometheus",
                    ActiveSessions = "Tracked via Prometheus"
                }},
                { "LdapMetrics", new {
                    Operations = "Tracked via Prometheus",
                    ActiveConnections = "Tracked via Prometheus"
                }},
                { "HealthCheckMetrics", new {
                    ComponentStatus = "Tracked via Prometheus",
                    CheckDuration = "Tracked via Prometheus"
                }}
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得指標摘要失敗");
            return new Dictionary<string, object> { { "error", ex.Message } };
        }
    }
}